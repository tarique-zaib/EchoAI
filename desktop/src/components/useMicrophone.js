import { useEffect, useState } from "react";

export default function useMicrophone() {
  const [level, setLevel] = useState(0);

  useEffect(() => {
    let audioContext;
    let analyser;
    let source;
    let animationId;
    let stream;

    const start = async () => {
      try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: true });

        audioContext = new (window.AudioContext || window.webkitAudioContext)();

        analyser = audioContext.createAnalyser();
        analyser.fftSize = 512;
        analyser.smoothingTimeConstant = 0.8;

        source = audioContext.createMediaStreamSource(stream);
        source.connect(analyser);

        const data = new Uint8Array(analyser.fftSize);

        const update = () => {
          analyser.getByteTimeDomainData(data);

          let sum = 0;

          for (let i = 0; i < data.length; i++) {
            const sample = (data[i] - 128) / 128;
            sum += sample * sample;
          }

          const rms = Math.sqrt(sum / data.length);

          // Convert RMS to a stable 0–100 confidence value
          const confidence = Math.min(100, Math.round(rms * 400));

          setLevel(confidence);

          animationId = requestAnimationFrame(update);
        };

        update();
      } catch (err) {
        console.error("Microphone error:", err);
      }
    };

    start();

    return () => {
      if (animationId) cancelAnimationFrame(animationId);

      if (stream) {
        stream.getTracks().forEach((track) => track.stop());
      }

      if (audioContext) {
        audioContext.close();
      }
    };
  }, []);

  return level;
}