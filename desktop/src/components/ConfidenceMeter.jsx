import "./ConfidenceMeter.css";

export default function ConfidenceMeter({ level, isListening }) {
  const score = level;

  const label =
    score >= 80 ? "Excellent" :
    score >= 60 ? "Good" :
    score >= 40 ? "Fair" :
    "Quiet";

  return (
    <div className="confidence-card">
      <div className="confidence-header">
        <span>🎤 Speaking Confidence</span>
        <span className="confidence-score">{score}%</span>
      </div>

      <div className="confidence-bar">
        <div
          className="confidence-fill"
          style={{ width: `${score}%` }}
        />
      </div>

      <div className="confidence-label">
        {isListening ? label : "Waiting..."}
      </div>

      <div className="confidence-metrics">
        <div>
          <strong>{score >= 60 ? "Good" : "Improve"}</strong>
          <span>Pace</span>
        </div>

        <div>
          <strong>{score >= 70 ? "High" : "Medium"}</strong>
          <span>Energy</span>
        </div>

        <div>
          <strong>{score >= 50 ? "Clear" : "Low"}</strong>
          <span>Volume</span>
        </div>
      </div>
    </div>
  );
}