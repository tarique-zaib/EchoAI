import sounddevice as sd
import soundfile as sf
import numpy as np
import tempfile
import os
from faster_whisper import WhisperModel

DEVICE = 14
RATE = 48000
CHUNK_SECONDS = 3          # More context
OVERLAP_SECONDS = 1        # Keep previous second

print("Loading model...", flush=True)

model = WhisperModel(
    "base.en",
    device="cpu",
    compute_type="int8"
)

print("Listening...", flush=True)

buffer = np.zeros(int(OVERLAP_SECONDS * RATE), dtype=np.float32)
last_text = ""

while True:
    audio = sd.rec(
        int(CHUNK_SECONDS * RATE),
        samplerate=RATE,
        channels=1,
        dtype="float32",
        device=DEVICE
    )

    sd.wait()

    audio = np.squeeze(audio)

    # Skip silence
    if np.max(np.abs(audio)) < 0.02:
        continue

    # Rolling buffer (1 sec overlap)
    combined = np.concatenate([buffer, audio])

    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as f:
        sf.write(f.name, combined, RATE)

        segments, info = model.transcribe(
            f.name,
            language="en",
            beam_size=3,
            best_of=3,
            temperature=0,
            vad_filter=True,
            condition_on_previous_text=False,
        )

        text = " ".join(s.text.strip() for s in segments).strip()

        # Don't repeat the same sentence
        if text and text != last_text:
            print(text, flush=True)
            last_text = text

    os.unlink(f.name)

    # Keep last second for context
    buffer = combined[-int(OVERLAP_SECONDS * RATE):]