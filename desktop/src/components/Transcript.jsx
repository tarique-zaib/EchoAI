import "./Transcript.css";

export default function Transcript({ transcript, partialTranscript }) {
  return (
    <>
      {partialTranscript && (
        <div className="live-subtitle">
          <span className="subtitle-dot"></span>
          {partialTranscript}
        </div>
      )}

      <section className="transcript-panel">
        <div className="transcript-header">
          <h2>Interview Transcript</h2>
        </div>

        <div className="transcript-body">
          {transcript || "Waiting for interview question..."}
        </div>
      </section>
    </>
  );
}