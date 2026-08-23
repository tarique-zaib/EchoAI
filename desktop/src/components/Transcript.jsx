import "./Transcript.css";

export default function Transcript({ transcript }) {
  return (
    <section className="question-card">
      <div className="question-label">● Current Question</div>

      <h2>{transcript || "Waiting for your first question..."}</h2>
    </section>
  );
}