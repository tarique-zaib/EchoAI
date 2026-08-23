export default function AIPanel({ answer }) {
  return (
    <div className="ai-panel">
      <h2>🧠 AI Suggested Answer</h2>

      {answer ? (
        <pre
          style={{
            whiteSpace: "pre-wrap",
            wordBreak: "break-word",
            lineHeight: 1.7,
            margin: 0
          }}
        >
          {answer}
        </pre>
      ) : (
        <p>Waiting for an interview question...</p>
      )}
    </div>
  );
}