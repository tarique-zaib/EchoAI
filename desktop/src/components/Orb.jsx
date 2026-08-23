import { motion } from "framer-motion";
import OrbRing from "./OrbRing";

export default function Orb({
  level,
  isListening,
  startListening,
  stopListening,
}) {
  const scale = isListening ? 1.05 + level * 0.4 : 1;
  const glow = isListening ? 120 + level * 150 : 70;

  return (
    <div
      style={{
        position: "relative",
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        cursor: "pointer",
      }}
      onClick={() =>
        isListening ? stopListening() : startListening()
      }
    >
      <OrbRing level={level} />

      <motion.div
        animate={{
          scale,
          boxShadow: `0 0 ${glow}px rgba(34,211,238,.9),
                      0 0 ${glow * 1.5}px rgba(79,70,229,.5)`,
        }}
        transition={{ duration: 0.08 }}
        style={{
          width: 220,
          height: 220,
          borderRadius: "50%",
          background:
            "radial-gradient(circle,#22D3EE,#4F46E5,#050816)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 42,
          color: "white",
        }}
      >
        {isListening ? "🎙️" : "◎"}
      </motion.div>
    </div>
  );
}