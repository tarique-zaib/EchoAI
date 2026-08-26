import { useEffect, useState, useRef } from "react";
import ReactMarkdown from "react-markdown";
import "./AIPanel.css";

export default function AIPanel({ answer, status }) {
  const [displayed, setDisplayed] = useState("");
  const markdownRef = useRef(null);

  useEffect(() => {
    if (answer.startsWith(displayed)) {
      const timer = setTimeout(() => {
        setDisplayed(answer);
      }, 25);

      return () => clearTimeout(timer);
    } else {
      setDisplayed(answer);
    }
  }, [answer, displayed]);

  // Auto-scroll while the answer streams
  useEffect(() => {
    if (markdownRef.current) {
      markdownRef.current.scrollTop = markdownRef.current.scrollHeight;
    }
  }, [displayed]);

  return (
    <section className="answer-panel">
      <div className="answer-header">
        <div className="answer-icon">🧠</div>
        <h2>AI Answer</h2>
      </div>

      <div ref={markdownRef} className="markdown-body">
        {displayed ? (
          <>
            <ReactMarkdown>{displayed}</ReactMarkdown>

            {status === "AI Answering" && (
              <span className="streaming-cursor"></span>
            )}
          </>
        ) : (
          <ReactMarkdown>
            {`Start speaking. I'll generate a structured interview answer with:

- **30-Second Answer**
- **Detailed Answer**
- **Practical Example**
- **Interview Tip**`}
          </ReactMarkdown>
        )}
      </div>
    </section>
  );
}