import { motion } from "motion/react";
import type { LucideIcon } from "lucide-react";

interface Props {
  icon: LucideIcon;
  color: string;
}

/** Large animated icon used on each showcase slide — a soft pulsing glow behind a gently floating icon. */
export function ShowcaseIcon(props: Props) {
  const { icon: Icon, color } = props;

  return (
    <div className="relative flex items-center justify-center size-28">
      <motion.div
        className="absolute inset-0 rounded-full"
        style={{ background: color, filter: "blur(24px)" }}
        animate={{ opacity: [0.25, 0.45, 0.25], scale: [0.85, 1, 0.85] }}
        transition={{ duration: 2.4, repeat: Infinity, ease: "easeInOut" }}
      />
      <motion.div
        initial={{ y: 0 }}
        animate={{ y: [0, -6, 0] }}
        transition={{ duration: 2.4, repeat: Infinity, ease: "easeInOut" }}
        className="relative"
      >
        <Icon size={56} color={color} strokeWidth={1.5} />
      </motion.div>
    </div>
  );
}
