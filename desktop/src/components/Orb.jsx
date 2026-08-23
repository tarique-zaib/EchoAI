import { useEffect, useState } from "react";
import "./Orb.css";

export default function Orb({ status, pulseKey }) {
  const state =
    status === "AI Answering"
      ? "answering"
      : status === "Listening"
      ? "listening"
      : "idle";

  // Friendly text shown inside the orb
  const statusText = {
    Idle: "Ready",
    Listening: "Listening...",
    "AI Answering": "Thinking...",
  }[status] || "Ready";

  const [pulse, setPulse] = useState(false);

  useEffect(() => {
    if (status !== "AI Answering") return;

    setPulse(true);

    const t = setTimeout(() => setPulse(false), 180);
    return () => clearTimeout(t);
  }, [pulseKey, status]);

  return (
    <section className="orb-section">
      <div className={`liquid-orb ${state} ${pulse ? "pulse" : ""}`}>
        <div className="thinking-wave"></div>

        <div className="ring ring1"></div>
        <div className="ring ring2"></div>
        <div className="ring ring3"></div>

        <div className="orb-core">
          <div className="mic">🎙️</div>
          <span>{statusText}</span>
        </div>
      </div>
    </section>
  );
}