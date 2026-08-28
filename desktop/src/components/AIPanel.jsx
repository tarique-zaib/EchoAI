import { useEffect, useState, useRef } from "react";
import ReactMarkdown from "react-markdown";
import "./AIPanel.css";

export default function AIPanel({ answer, status }) {
  const [displayed, setDisplayed] = useState("");
  const markdownRef = useRef(null);

  // Smooth typing effect
  useEffect(() => {
    if (answer.startsWith(displayed)) {
      const timer = setTimeout(() => {
        setDisplayed(answer);
      }, 15); // Faster typing
      return () => clearTimeout(timer);
    }

    setDisplayed(answer);
  }, [answer, displayed]);

  // Auto-scroll
  useEffect(() => {
    if (markdownRef.current) {
      markdownRef.current.scrollTop = markdownRef.current.scrollHeight;
    }
  }, [displayed]);

  const isStreaming =
    status === "AI Answering" ||
    status === "Generating Answer" ||
    status === "Thinking";

  return (
    <section className="answer-panel">
      <div className="answer-header">
        <div className="answer-icon">🧠</div>
        <h2>AI Answer</h2>
      </div>

      <div ref={markdownRef} className="markdown-body">
        {displayed ? (
          isStreaming ? (
            <>
              <pre className="streaming-answer">{displayed}</pre>
              <span className="streaming-cursor"></span>
            </>
          ) : (
            <ReactMarkdown>{displayed}</ReactMarkdown>
          )
        ) : (
          <ReactMarkdown>{`Start speaking. I'll generate a structured interview answer with:

- **30-Second Answer**
- **Detailed Answer**
- **Practical Example**
- **Interview Tip**`}</ReactMarkdown>
        )}
      </div>
    </section>
  );
}