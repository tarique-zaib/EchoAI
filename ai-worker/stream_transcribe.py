import queue
import threading
import re
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
CONTEXT_SEC = 1.5     # Keep 1 second before speech

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

# ---- New: merge helper state ----
pending_text = ""
pending_time = 0
pending_lock = threading.Lock()

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


# ---------------------------------------------------
# New: merge/ignore tiny repeated fragments
# ---------------------------------------------------
def emit_with_merge(text, now):
    global pending_text, pending_time

    with pending_lock:

        # Ignore tiny echoes like:
        # "Any other", "Okay", "Right"
        if pending_text and now - pending_time < 0.8:
            if len(text.split()) <= 3:
                return

        # Flush previous pending sentence
        pending_text = text
        pending_time = now

    def delayed_flush(expected_time):
        global pending_text

        time.sleep(0.7)

        with pending_lock:
            if pending_text and pending_time == expected_time:
                print(pending_text, flush=True)
                pending_text = ""

    threading.Thread(
        target=delayed_flush,
        args=(pending_time,),
        daemon=True
    ).start()


def transcribe(audio):
    global last_question, last_time, is_processing, pending_audio
    with processing_lock:
        if is_processing:
            pending_audio = audio
            return
        
        is_processing = True
        
     # Normalize audio volume
    peak = np.max(np.abs(audio))
    if peak > 1e-6:
        audio = audio / peak * 0.95

    try:
        segments, _ = model.transcribe(
            audio,
            language="en",
            beam_size=3,
            best_of=3,
            temperature=0,
            word_timestamps=True,
            vad_filter=False,
            condition_on_previous_text=False,            
        )
        
        segments = list(segments)
        
        text = clean_text(
            " ".join(s.text.strip() for s in segments).strip()
        )

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
            transcribe(next_audio)

def worker():
    buffer = np.zeros(0, dtype=np.float32)

    while True:
        chunk = audio_queue.get()

        if chunk.ndim == 2:
            chunk = chunk.mean(axis=1)

        chunk = chunk.astype(np.float32)

        buffer = np.concatenate([buffer, chunk])

        # Keep at most 8 seconds in memory
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

            # Wait until the speaker has been silent for ~200 ms
            if len(buffer) - latest["end"] < int(RATE * 0.45):
                continue

            last_start = latest["start"]
            last_end = latest["end"]

            context_start = max(0, last_start - int(RATE * CONTEXT_SEC))
            context_end = min(len(buffer), last_end + int(RATE * 0.2))

            audio = buffer[context_start:context_end]

            buffer = buffer[last_end:]

            threading.Thread(
                target=transcribe,
                args=(audio,),
                daemon=True
            ).start()

        else:
            # Keep a rolling 2-second history instead of clearing everything
            if len(buffer) > RATE * 2:
                buffer = buffer[-RATE * 2:]

threading.Thread(target=worker, daemon=True).start()

# ---------------------------------------------
# Windows System Audio Capture (WASAPI Loopback)
# ---------------------------------------------

USE_SYSTEM_AUDIO = args.system

if USE_SYSTEM_AUDIO:
    speaker = sc.default_speaker()

    print(f"Using Windows output: {speaker.name}", flush=True)

    loopback = sc.get_microphone(
        id=str(speaker.id),
        include_loopback=True
    )

    with loopback.recorder(
        samplerate=RATE,
        blocksize=BLOCK
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