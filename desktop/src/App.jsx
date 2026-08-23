import { useEffect, useState } from "react";
import "./App.css";

import Orb from "./components/Orb";
import Transcript from "./components/Transcript";
import AIPanel from "./components/AIPanel";
import useMicrophone from "./components/useMicrophone";

import connection from "./services/signalr";

export default function App() {
  const level = useMicrophone();

  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [answer, setAnswer] = useState("");
  const [status, setStatus] = useState("Idle");

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
      console.log("Transcript:", text);

      setTranscript((prev) => (prev ? prev + "\n" + text : text));
    });

    // ---------------- AI Streaming ----------------
    connection.on("AnswerStarted", () => {
      console.log("Answer Started");
      setAnswer("");
    });

    connection.on("ReceiveAnswerChunk", (chunk) => {
      console.log("AI chunk received:", chunk);
      setAnswer((prev) => prev + chunk);
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

  return (
    <div className="app">
      <h1>EchoPrep AI</h1>
      <p>Practice interviews like you're already hired.</p>

      <div className="orb-container">
        <Orb
          level={level}
          isListening={isListening}
          status={status}
          startListening={startInterview}
          stopListening={stopInterview}
        />
      </div>

      <Transcript
        transcript={transcript}
        isListening={isListening}
        status={status}
      />

      <AIPanel answer={answer} />
    </div>
  );
}