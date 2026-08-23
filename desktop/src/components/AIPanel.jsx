import { useEffect, useRef } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import "./AIPanel.css";

export default function AIPanel({ answer }) {
  const answerRef = useRef(null);

  // Auto-scroll while streaming
  useEffect(() => {
    answerRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "end",
    });
  }, [answer]);

  return (
    <div className="ai-panel">
      <div className="ai-header">🧠 AI Suggested Answer</div>

      {answer ? (
        <>
          <div className="markdown-body">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>
              {answer}
            </ReactMarkdown>
            <div ref={answerRef} />
          </div>

          <span className="typing-cursor">▋</span>
        </>
      ) : (
        <p className="waiting-text">
          Waiting for an interview question...
        </p>
      )}
    </div>
  );
}