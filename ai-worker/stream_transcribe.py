import queue
import threading
import re
import numpy as np
import sounddevice as sd
import time
import soundcard as sc

from faster_whisper import WhisperModel
from silero_vad import load_silero_vad, get_speech_timestamps

RATE = 16000
BLOCK = 2048

audio_queue = queue.Queue()

last_question = ""
last_time = 0
is_processing = False
processing_lock = threading.Lock()

print("Loading model...", flush=True)

# Faster model for CPU
model = WhisperModel(
    "tiny",
    device="cpu",
    compute_type="int8"
)

vad_model = load_silero_vad()

print("Listening...", flush=True)


def audio_callback(indata, frames, time_info, status):
    if status:
        return
    audio_queue.put(indata.copy())


def clean_text(text):
    replacements = {
        r"\byour service bus\b": "Azure Service Bus",
        r"\bzero service bus\b": "Azure Service Bus",
        r"\bazure service\b": "Azure Service Bus",
        r"\bc sharp\b": "C#",
        r"\bdot net\b": ".NET",
        r"\bentity framework\b": "Entity Framework",
    }

    for pattern, value in replacements.items():
        text = re.sub(pattern, value, text, flags=re.IGNORECASE)

    text = re.sub(
        r"Azure Service Bus\s+Bus\b",
        "Azure Service Bus",
        text,
        flags=re.IGNORECASE,
    )

    # Remove consecutive duplicate words
    words = text.split()
    cleaned = []

    for word in words:
        if not cleaned or cleaned[-1].lower() != word.lower():
            cleaned.append(word)

    return " ".join(cleaned).strip()


def transcribe(audio):
    global last_question, last_time, is_processing

    with processing_lock:
        if is_processing:
            return
        is_processing = True

    try:
        segments, _ = model.transcribe(
            audio,
            language="en",
            beam_size=1,
            best_of=1,
            temperature=0,
            vad_filter=False,
            condition_on_previous_text=False,
            initial_prompt=(
                "Software engineering interview. "
                "Technical terms: Azure, Azure Service Bus, "
                ".NET, C#, Entity Framework, React, SQL Server."
            )
        )

        text = clean_text(
            " ".join(s.text.strip() for s in segments).strip()
        )

        if not text:
            return

        now = time.time()

        # Ignore duplicate question within 2 seconds
        if text.lower() == last_question.lower() and now - last_time < 2:
            return

        last_question = text
        last_time = now

        print(text, flush=True)

    finally:
        with processing_lock:
            is_processing = False


def worker():
    buffer = np.zeros(0, dtype=np.float32)

    while True:
        chunk = audio_queue.get()

        if chunk.ndim == 2:
            chunk = chunk.mean(axis=1)

        chunk = chunk.astype(np.float32)

        buffer = np.concatenate([buffer, chunk])

        # Wait until at least 1 second of audio
        if len(buffer) < RATE:
            continue

        speech = get_speech_timestamps(
            buffer,
            vad_model,
            sampling_rate=RATE
        )

        if speech:
            last_end = speech[-1]["end"]

            # Trigger after ~120 ms silence
            if len(buffer) - last_end >= RATE // 8:
                audio = buffer[:last_end]

                # Keep 50 ms tail so next question isn't clipped
                tail = int(RATE * 0.05)
                buffer = buffer[max(0, last_end - tail):]

                threading.Thread(
                    target=transcribe,
                    args=(audio,),
                    daemon=True
                ).start()

        else:
            # Prevent infinite buffer growth
            if len(buffer) > RATE * 2:
                buffer = np.zeros(0, dtype=np.float32)


threading.Thread(target=worker, daemon=True).start()

# ---------------------------------------------
# Windows System Audio Capture (WASAPI Loopback)
# ---------------------------------------------

USE_SYSTEM_AUDIO = True   # False = Microphone

if USE_SYSTEM_AUDIO:
    speaker = sc.default_speaker()

    print(f"Using Windows output: {speaker.name}", flush=True)

    # Open the speaker's loopback device
    loopback = sc.get_microphone(
        id=str(speaker.id),
        include_loopback=True
    )

    with loopback.recorder(samplerate=RATE) as recorder:
        while True:
            data = recorder.record(numframes=2048)

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