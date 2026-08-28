import queue
import threading
import numpy as np
import sounddevice as sd
import time
import soundcard as sc
import warnings
from soundcard.mediafoundation import SoundcardRuntimeWarning
from faster_whisper import WhisperModel
from silero_vad import load_silero_vad, get_speech_timestamps
import argparse

parser = argparse.ArgumentParser(add_help=False)
parser.add_argument("--system", action="store_true")
parser.add_argument("--headphone", action="store_true")
args, _ = parser.parse_known_args()

RATE = 16000
BLOCK = 2048
CONTEXT_SEC = 1.5
LAST_TRANSCRIPT = ""
MAX_CONTEXT_CHARS = 200

PARTIAL_INTERVAL = 0.4
partial_buffer = np.zeros(0, dtype=np.float32)
partial_lock = threading.Lock()
last_partial = ""

QUESTION_HOLD_MS = 900

warnings.filterwarnings(
    "ignore",
    category=SoundcardRuntimeWarning
)

audio_queue = queue.Queue()

last_question = ""
last_time = 0

is_processing = False
processing_lock = threading.Lock()
pending_audio = None

pending_text = ""
pending_time = 0
pending_lock = threading.Lock()

question_buffer = ""
question_timer = None
question_lock = threading.Lock()

print("Loading model...", flush=True)

model = WhisperModel(
    "base",
    device="cpu",
    compute_type="int8"
)

vad_model = load_silero_vad()

MIN_SPEECH_MS = 350
MIN_SILENCE_MS = 450

print("Listening...", flush=True)


# ---------------------------------
# Audio Input
# ---------------------------------

def audio_callback(indata, frames, time_info, status):
    if status:
        return
    audio_queue.put(indata.copy())


def clean_text(text):
    words = text.split()
    cleaned = []

    for word in words:
        if not cleaned or cleaned[-1].lower() != word.lower():
            cleaned.append(word)

    return " ".join(cleaned).strip()


# ---------------------------------
# Question Buffer
# ---------------------------------

def flush_question():
    global question_buffer

    with question_lock:
        if question_buffer:
            print(question_buffer, flush=True)
            question_buffer = ""


def emit_question(text):
    global question_buffer, question_timer

    with question_lock:
        question_buffer = text

        if question_timer:
            question_timer.cancel()

        question_timer = threading.Timer(
            QUESTION_HOLD_MS / 1000,
            flush_question
        )
        question_timer.daemon = True
        question_timer.start()


# ---------------------------------
# Smart Merge
# ---------------------------------

def emit_with_merge(text, now):
    global pending_text, pending_time

    merged = False

    with pending_lock:
        if pending_text:
            gap = now - pending_time

            if gap < 1.2:
                previous_complete = pending_text.rstrip().endswith((".", "?", "!"))
                current_starts_lower = text[:1].islower()

                if not previous_complete or current_starts_lower:
                    pending_text = f"{pending_text.rstrip()} {text}"
                    pending_time = now
                    merged = True

        if not merged:
            pending_text = text
            pending_time = now

        expected_time = pending_time

    def delayed_flush(expected_time):
        global pending_text

        time.sleep(0.7)

        with pending_lock:
            if pending_text and pending_time == expected_time:
                emit_question(pending_text)
                pending_text = ""

    threading.Thread(
        target=delayed_flush,
        args=(expected_time,),
        daemon=True
    ).start()


# ---------------------------------
# Whisper
# ---------------------------------

def transcribe(audio):
    global last_question, last_time, is_processing, pending_audio, LAST_TRANSCRIPT

    with processing_lock:
        if is_processing:
            pending_audio = audio
            return

        is_processing = True

    peak = np.percentile(np.abs(audio), 99)

    if peak > 1e-4:
        audio = np.clip(audio / peak * 0.9, -1.0, 1.0)

    try:
        initial_prompt = (
            LAST_TRANSCRIPT[-MAX_CONTEXT_CHARS:]
            if LAST_TRANSCRIPT
            else None
        )

        segments, _ = model.transcribe(
            audio,
            language="en",
            beam_size=5,
            best_of=5,
            temperature=0,
            word_timestamps=True,
            vad_filter=False,
            condition_on_previous_text=False,
            initial_prompt=initial_prompt,
        )

        segments = list(segments)

        text = clean_text(
            " ".join(s.text.strip() for s in segments).strip()
        )

        if text and len(text.split()) >= 4:
            LAST_TRANSCRIPT = (
                (LAST_TRANSCRIPT + " " + text).strip()
            )[-MAX_CONTEXT_CHARS:]

        if not text:
            return

        if len(text.split()) < 2:
            return

        now = time.time()

        if text.lower() == last_question.lower() and now - last_time < 2:
            return

        last_question = text
        last_time = now

        emit_with_merge(text, now)

    finally:
        next_audio = None

        with processing_lock:
            if pending_audio is not None:
                next_audio = pending_audio
                pending_audio = None

            is_processing = False

        if next_audio is not None:
            threading.Thread(
                target=transcribe,
                args=(next_audio,),
                daemon=True,
            ).start()

def partial_worker():
    global partial_buffer, last_partial

    while True:
        time.sleep(PARTIAL_INTERVAL)

        with partial_lock:
            audio = partial_buffer.copy()

        if len(audio) < RATE:
            continue

        try:
            segments, _ = model.transcribe(
                audio[-RATE * 2:],
                language="en",
                beam_size=1,
                best_of=1,
                temperature=0,
                vad_filter=False,
                condition_on_previous_text=False
            )

            text = clean_text(
                " ".join(s.text.strip() for s in segments).strip()
            )

            if len(text.split()) >= 2 and text != last_partial:
                last_partial = text
                print(f"PARTIAL:{text}", flush=True)

        except Exception:
            pass
# ---------------------------------
# Worker
# ---------------------------------

def worker():
    global partial_buffer
    buffer = np.zeros(0, dtype=np.float32)

    while True:
        chunk = audio_queue.get()

        if chunk.ndim == 2:
            chunk = chunk.mean(axis=1)

        chunk = chunk.astype(np.float32)

        buffer = np.concatenate([buffer, chunk])
        with partial_lock:
            partial_buffer = np.concatenate([partial_buffer, chunk])

            if len(partial_buffer) > RATE * 3:
                partial_buffer = partial_buffer[-RATE * 3:]

        if len(buffer) > RATE * 8:
            buffer = buffer[-RATE * 8:]

        if len(buffer) < RATE:
            continue

        speech = get_speech_timestamps(
            buffer,
            vad_model,
            sampling_rate=RATE,
            min_speech_duration_ms=MIN_SPEECH_MS,
            min_silence_duration_ms=MIN_SILENCE_MS,
        )

        if speech:
            latest = speech[-1]

            if len(buffer) - latest["end"] < int(RATE * 0.45):
                continue

            last_start = latest["start"]
            last_end = latest["end"]

            context_start = max(
                0,
                last_start - int(RATE * CONTEXT_SEC)
            )

            context_end = min(
                len(buffer),
                last_end + int(RATE * 0.35)
            )

            audio = buffer[context_start:context_end]

            buffer = buffer[last_end:]

            threading.Thread(
                target=transcribe,
                args=(audio,),
                daemon=True,
            ).start()

        else:
            if len(buffer) > RATE * 2:
                buffer = buffer[-RATE * 2:]


threading.Thread(
    target=worker,
    daemon=True
).start()
threading.Thread(target=partial_worker, daemon=True).start()


# ---------------------------------
# Capture Source
# ---------------------------------

USE_SYSTEM_AUDIO = args.system

if USE_SYSTEM_AUDIO:

    speaker = sc.default_speaker()

    print(
        f"Using Windows output: {speaker.name}",
        flush=True,
    )

    loopback = sc.get_microphone(
        id=str(speaker.id),
        include_loopback=True,
    )

    with loopback.recorder(
        samplerate=RATE,
        blocksize=BLOCK,
    ) as recorder:

        while True:
            data = recorder.record(numframes=BLOCK)

            if data.size == 0:
                continue

            audio_queue.put(data.astype(np.float32))

else:

    print("Using microphone input", flush=True)

    with sd.InputStream(
        samplerate=RATE,
        channels=1,
        dtype="float32",
        blocksize=BLOCK,
        callback=audio_callback,
    ):
        while True:
            threading.Event().wait(1)