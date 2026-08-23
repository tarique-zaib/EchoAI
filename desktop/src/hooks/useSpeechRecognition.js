import { useEffect, useRef, useState } from "react";

export default function useSpeechRecognition() {
  const [transcript, setTranscript] = useState("");
  const [isListening, setIsListening] = useState(false);
  const recognitionRef = useRef(null);

  useEffect(() => {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;

    if (!SR) {
      console.error("SpeechRecognition not supported");
      return;
    }

    const recognition = new SR();
    recognition.lang = "en-US";
    recognition.continuous = false;
    recognition.interimResults = true;
    recognition.maxAlternatives = 1;

    recognition.onstart = () => console.log("Recognition started");
    recognition.onaudiostart = () => console.log("Audio detected");
    recognition.onsoundstart = () => console.log("Sound detected");
    recognition.onspeechstart = () => console.log("Speech detected");

    recognition.onresult = (e) => {
      console.log("RESULT", e);
      let text = "";
      for (let i = e.resultIndex; i < e.results.length; i++) {
        text += e.results[i][0].transcript + " ";
      }
      setTranscript(text.trim());
    };

    recognition.onerror = (e) => console.log("ERROR", e.error);
    recognition.onend = () => {
      console.log("Recognition ended");
      setIsListening(false);
    };

    recognitionRef.current = recognition;
  }, []);

  const startListening = () => {
    setTranscript("");
    setIsListening(true);
    recognitionRef.current.start();
  };

  return {
    transcript,
    isListening,
    startListening,
    stopListening: () => recognitionRef.current.stop(),
  };
}