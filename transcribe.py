import sounddevice as sd
import soundfile as sf
import numpy as np
from faster_whisper import WhisperModel

DEVICE = 14      # Intel Microphone Array (WASAPI)
SECONDS = 5

print("====================================")
print(" EchoPrep AI - Local Transcription")
print("====================================")

# Get microphone information
device_info = sd.query_devices(DEVICE, "input")
RATE = int(device_info["default_samplerate"])
CHANNELS = 1

print(f"Microphone: {device_info['name']}")
print(f"Sample Rate: {RATE}")
print(f"Channels: {CHANNELS}\n")

print("Loading Whisper model...")
model = WhisperModel("small.en", device="cpu", compute_type="int8")

print("Recording for 5 seconds...")
print("Speak now...\n")

audio = sd.rec(
    int(SECONDS * RATE),
    samplerate=RATE,
    channels=CHANNELS,
    dtype="float32",
    device=DEVICE
)

sd.wait()

audio = np.squeeze(audio)

sf.write("temp.wav", audio, RATE)

peak = float(np.max(np.abs(audio)))
print(f"Peak Volume: {peak:.3f}")

if peak < 0.01:
    print("⚠ Very low microphone volume detected.")
    exit()

print("\nTranscribing...\n")

segments, info = model.transcribe(
    "temp.wav",
    beam_size=5,
    vad_filter=True
)

segments = list(segments)

print(f"Language: {info.language}")
print(f"Segments: {len(segments)}")
print("-" * 40)

full_text = ""

for segment in segments:
    print(f"[{segment.start:.1f}s - {segment.end:.1f}s]")
    print(segment.text)
    print()
    full_text += segment.text + " "

print("-" * 40)
print("Final Transcript:")
print(full_text.strip())