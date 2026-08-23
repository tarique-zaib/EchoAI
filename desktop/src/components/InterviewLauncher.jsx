import { useEffect, useState } from "react";
import "./InterviewLauncher.css";

export default function InterviewLauncher({ onStart }) {
  const [meeting, setMeeting] = useState({
    detected: false,
    app: "",
    checking: true
  });

  useEffect(() => {
    fetch("http://localhost:5000/api/meeting/detect")
      .then(r => r.json())
      .then(data => setMeeting({ ...data, checking: false }))
      .catch(() =>
        setMeeting({
          detected: false,
          app: "",
          checking: false
        })
      );
  }, []);

  return (
    <div className="launcher">
      <div className="launcher-orb">
        <div className="launcher-core">🎙️</div>
      </div>

      <h1>EchoPrep AI</h1>

      <p>
        Your invisible interview copilot for Zoom, Meet and Teams.
      </p>

      <div className="meeting-status">
        {meeting.checking ? (
          <span>Checking for meeting...</span>
        ) : meeting.detected ? (
          <span className="success">
            ✓ {meeting.app} detected
          </span>
        ) : (
          <span className="warning">
            No meeting detected
          </span>
        )}
      </div>

      <button onClick={onStart}>
        Start Interview
      </button>

      <span className="launcher-note">
        Local • Private • Real-time
      </span>
    </div>
  );
}