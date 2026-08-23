import { motion } from "framer-motion";

export default function OrbRing({ level }) {
  return (
    <motion.div
      animate={{
        scale: 1.1 + level * 0.2,
        rotate: 360,
        opacity: 0.4 + level * 0.5
      }}
      transition={{
        rotate: {
          duration: 8,
          repeat: Infinity,
          ease: "linear"
        },
        scale: {
          duration: 0.1
        }
      }}
      style={{
        position: "absolute",
        width: 260,
        height: 260,
        borderRadius: "50%",
        border: "3px solid rgba(34,211,238,.5)"
      }}
    />
  );
}