import "./Transcript.css";

export default function Transcript({ transcript, partialTranscript }) {
  return (
    <div className="transcript-panel">
      <div className="confirmed">{transcript}</div>

      {partialTranscript && (
        <div className="partial">🎙 {partialTranscript}</div>
      )}
    </div>
  );
}