import { useEffect, useState } from "react";
import "./SessionStats.css";

export default function SessionStats({ isListening, questionCount }) {
  const [seconds, setSeconds] = useState(0);

  useEffect(() => {
    if (!isListening) return;

    const timer = setInterval(() => {
      setSeconds((prev) => prev + 1);
    }, 1000);

    return () => clearInterval(timer);
  }, [isListening]);

  const minutes = String(Math.floor(seconds / 60)).padStart(2, "0");
  const secs = String(seconds % 60).padStart(2, "0");

  return (
    <div className="session-card">
      <div className="session-item">
        <span className="session-label">⏱ Elapsed</span>
        <h2>{minutes}:{secs}</h2>
      </div>

      <div className="divider" />

      <div className="session-item">
        <span className="session-label">❓ Questions</span>
        <h2>{questionCount}</h2>
      </div>
    </div>
  );
}