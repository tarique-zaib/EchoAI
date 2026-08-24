import { useEffect, useState } from "react";
import "./App.css";

import Orb from "./components/Orb";
import Transcript from "./components/Transcript";
import AIPanel from "./components/AIPanel";
import useMicrophone from "./components/useMicrophone";
import ResumeUpload from "./components/ResumeUpload";
import connection from "./services/signalr";
import ConfidenceMeter from "./components/ConfidenceMeter";
import SessionStats from "./components/SessionStats";
import InterviewLauncher from "./components/InterviewLauncher";
export default function App() {
  const level = useMicrophone();
  const [started, setStarted] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [answer, setAnswer] = useState("");
  const [status, setStatus] = useState("Idle");
  const [questionCount, setQuestionCount] = useState(0);
  const [pulseKey, setPulseKey] = useState(0);

  useEffect(() => {
    let mounted = true;

    // ---------------- Status ----------------
    connection.on("ReceiveStatus", (newStatus) => {
      console.log("Status:", newStatus);

      setStatus(newStatus);

      if (newStatus === "Listening") {
        setIsListening(true);
      } else if (newStatus === "Idle") {
        setIsListening(false);
      }
    });

    // ---------------- Transcript ----------------
    connection.on("ReceiveTranscript", (text) => {
      setTranscript((prev) => (prev ? prev + "\n" + text : text));
      setQuestionCount((prev) => prev + 1);
    });

    connection.on("ClearAnswer", () => {
      setAnswer("");
    });

    // ---------------- AI Streaming ----------------
    connection.on("AnswerStarted", () => {
      console.log("Answer Started");
      setAnswer("");
    });

    connection.on("ReceiveAnswerChunk", (chunk) => {
      console.log("AI chunk received:", chunk);
      setAnswer((prev) => prev + chunk);
      setPulseKey((prev) => prev + 1);
    });

    connection.on("AnswerCompleted", () => {
      console.log("Answer Completed");
    });

    async function connect() {
      try {
        if (connection.state === "Disconnected") {
          await connection.start();

          if (mounted) {
            console.log("SignalR Connected");
          }
        }
      } catch (err) {
        console.error(err);
      }
    }

    connect();

    return () => {
      mounted = false;

      connection.off("ReceiveStatus");
      connection.off("ReceiveTranscript");
      connection.off("ClearAnswer");
      connection.off("AnswerStarted");
      connection.off("ReceiveAnswerChunk");
      connection.off("AnswerCompleted");

      if (connection.state === "Connected") {
        connection.stop();
      }
    };
  }, []);

  const startInterview = async () => {
    setTranscript("");
    setAnswer("");
    setQuestionCount(0);
    setStatus("Loading model...");

    await fetch("http://localhost:5153/api/interview/start", {
      method: "POST",
    });
  };

  const stopInterview = async () => {
    await fetch("http://localhost:5153/api/interview/stop", {
      method: "POST",
    });

    setIsListening(false);
    setStatus("Idle");
  };

  if (!started) {
    return (
      <InterviewLauncher
        onStart={async () => {
          setStarted(true);
          await startInterview();
        }}
      />
    );
  }

  return (
    <div className="app">
      <header className="header">
        <div>
          <h1>EchoPrep AI</h1>
          <p>Interview Copilot</p>
        </div>

        <div className="live">Live</div>
      </header>
      <div
        style={{
          display: "flex",
          gap: "10px",
          justifyContent: "center",
          marginBottom: "20px",
        }}
      >
        <button onClick={startInterview}>Start Interview</button>
        <button onClick={stopInterview}>Stop Interview</button>
      </div>

      <Orb status={status} pulseKey={pulseKey} />
      <ResumeUpload />
      <Transcript transcript={transcript} />

      <AIPanel answer={answer} status={status} />
    </div>
  );
}
