import { useEffect, useState } from "react";

export default function useMicrophone() {
  const [level, setLevel] = useState(0);

  useEffect(() => {
    let animationId;

    async function init() {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: true
      });

      const ctx = new AudioContext();
      const analyser = ctx.createAnalyser();
      analyser.fftSize = 256;

      const source = ctx.createMediaStreamSource(stream);
      source.connect(analyser);

      const data = new Uint8Array(analyser.frequencyBinCount);

      const update = () => {
        analyser.getByteFrequencyData(data);

        let sum = 0;
        for (let i = 0; i < data.length; i++) sum += data[i];

        setLevel(sum / data.length / 255);

        animationId = requestAnimationFrame(update);
      };

      update();
    }

    init();

    return () => cancelAnimationFrame(animationId);
  }, []);

  return level;
}