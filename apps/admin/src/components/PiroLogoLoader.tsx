import { motion } from "motion/react";

interface Props {
  size?: number;
}

const FLAME_PATH =
  "M12.832 21.801c3.126-.626 7.168-2.875 7.168-8.69c0-5.291-3.873-8.815-6.658-10.434c-.619-.36-1.342.113-1.342.828v1.828c0 1.442-.606 4.074-2.29 5.169c-.86.559-1.79-.278-1.894-1.298l-.086-.838c-.1-.974-1.092-1.565-1.87-.971C4.461 8.46 3 10.33 3 13.11C3 20.221 8.289 22 10.933 22q.232 0 .484-.015C10.111 21.874 8 21.064 8 18.444c0-2.05 1.495-3.435 2.631-4.11c.306-.18.663.055.663.41v.59c0 .45.175 1.155.59 1.637c.47.546 1.159-.026 1.214-.744c.018-.226.246-.37.442-.256c.641.375 1.46 1.175 1.46 2.473c0 2.048-1.129 2.99-2.168 3.357";

/**
 * Piro logo shown in place of a spinner for full-screen loading states — animated like a live
 * flame: a soft pulsing glow behind it, plus a subtle asymmetric sway/flicker on the mark itself
 * (bottom-anchored scale + skew, since real flames flicker from the base, not the tip).
 */
export function PiroLogoLoader(props: Props) {
  const { size = 40 } = props;

  return (
    <div className="relative flex items-center justify-center" style={{ width: size, height: size }}>
      <motion.div
        className="absolute inset-0 rounded-full"
        style={{ background: "#3d96fe", filter: "blur(10px)" }}
        animate={{ opacity: [0.25, 0.5, 0.3, 0.55, 0.25], scale: [0.8, 1, 0.9, 1.05, 0.8] }}
        transition={{ duration: 2.2, repeat: Infinity, ease: "easeInOut" }}
      />
      <motion.svg
        xmlns="http://www.w3.org/2000/svg"
        width={size}
        height={size}
        viewBox="0 0 24 24"
        className="relative"
        style={{ transformOrigin: "50% 100%" }}
        animate={{
          scaleY: [1, 1.05, 0.98, 1.04, 1],
          scaleX: [1, 0.97, 1.02, 0.98, 1],
          skewX: [0, 1.5, -1, 0.8, 0],
          rotate: [0, 0.8, -0.6, 0.4, 0],
        }}
        transition={{ duration: 2.2, repeat: Infinity, ease: "easeInOut" }}
      >
        <path fill="#3d96fe" d={FLAME_PATH} />
      </motion.svg>
    </div>
  );
}
