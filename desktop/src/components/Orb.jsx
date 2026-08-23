import "./Orb.css";

export default function Orb({
  level,
  isListening,
  status,
  startListening,
  stopListening,
}) {
  const scale =
    status === "Listening"
      ? 1 + Math.min(level / 180, 0.18)
      : status === "AI Answering"
      ? 1.05
      : 1;

  const handleClick = () => {
    if (isListening) stopListening();
    else startListening();
  };

  return (
    <div className={`orb-wrapper ${status.toLowerCase().replace(/\s+/g, "-")}`}>
      <div
        className="orb"
        onClick={handleClick}
        style={{ transform: `scale(${scale})` }}
      >
        <span className="mic-icon">
          {status === "AI Answering" ? "🧠" : "🎙️"}
        </span>
      </div>
    </div>
  );
}