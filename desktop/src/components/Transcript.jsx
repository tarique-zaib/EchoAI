export default function Transcript({ transcript, isListening }) {
  return (
    <div
      style={{
        marginTop: 25,
        width: "70%",
        maxWidth: 700,
        padding: 20,
        borderRadius: 20,
        background: "rgba(255,255,255,.05)",
        border: "1px solid rgba(255,255,255,.08)",
        color: "white",
        backdropFilter: "blur(15px)"
      }}
    >
      <div
        style={{
          color: "#22D3EE",
          fontWeight: "bold",
          marginBottom: 10
        }}
      >
        {isListening ? "● Listening" : "○ Idle"}
      </div>

      <pre
        style={{
          whiteSpace: "pre-wrap",
          margin: 0,
          fontFamily: "inherit"
        }}
      >
        {transcript || "Waiting for transcript..."}
      </pre>
    </div>
  );
}