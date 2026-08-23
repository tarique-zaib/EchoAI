import "./InterviewLauncher.css";

export default function InterviewLauncher({ onStart }) {
  return (
    <div className="launcher">
      <div className="launcher-orb">
        <div className="launcher-core">🎙️</div>
      </div>

      <h1>EchoPrep AI</h1>

      <p>
        Your invisible interview copilot for Zoom, Meet and Teams.
      </p>

      <button onClick={onStart}>
        Start Interview
      </button>

      <span className="launcher-note">
        Local • Private • Real-time
      </span>
    </div>
  );
}