import { useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { ChevronLeft, ChevronRight, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SHOWCASE_SLIDES } from "@/features/showcase/data/showcase-slides";
import { ShowcaseIcon } from "@/features/showcase/components/ShowcaseIcon";

interface Props {
  onClose: () => void;
}

/**
 * Full-screen, one-time feature tour shown after setup completes. Manual navigation only (no
 * auto-advance) — the user controls the pace via arrows or by clicking a progress segment.
 * Skippable at any point; reaching the end or skipping both dismiss it the same way.
 */
export function ShowcaseOverlay(props: Props) {
  const { onClose } = props;
  const [index, setIndex] = useState(0);
  const [direction, setDirection] = useState(1);

  const slide = SHOWCASE_SLIDES[index];
  const isLast = index === SHOWCASE_SLIDES.length - 1;

  function goTo(next: number) {
    setDirection(next > index ? 1 : -1);
    setIndex(Math.max(0, Math.min(SHOWCASE_SLIDES.length - 1, next)));
  }

  function handleNext() {
    if (isLast) {
      onClose();
    } else {
      goTo(index + 1);
    }
  }

  return (
    <div className="fixed inset-0 z-100 flex flex-col bg-background">
      {/* Progress bar */}
      <div className="flex gap-1.5 p-4 pt-6 max-w-2xl mx-auto w-full">
        {SHOWCASE_SLIDES.map((s, i) => (
          <button
            key={s.title}
            onClick={() => goTo(i)}
            className={cn(
              "h-1 flex-1 rounded-full transition-colors",
              i <= index ? "bg-primary" : "bg-muted"
            )}
            aria-label={`Go to slide ${i + 1}`}
          />
        ))}
      </div>

      {/* Skip */}
      <button
        onClick={onClose}
        className="absolute top-4 right-4 sm:top-6 sm:right-6 flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
      >
        Skip tour
        <X size={14} />
      </button>

      {/* Slide content */}
      <div className="flex-1 flex items-center justify-center overflow-hidden px-6">
        <AnimatePresence mode="wait" custom={direction}>
          <motion.div
            key={index}
            custom={direction}
            initial={{ opacity: 0, x: direction * 40 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: direction * -40 }}
            transition={{ duration: 0.3, ease: "easeInOut" }}
            className="flex flex-col items-center text-center gap-6 max-w-md"
          >
            <ShowcaseIcon icon={slide.icon} color={slide.color} />
            <div className="flex flex-col gap-2">
              <h2 className="text-2xl font-bold">{slide.title}</h2>
              <p className="text-muted-foreground">{slide.description}</p>
            </div>
            {slide.example && (
              <motion.div
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3, delay: 0.15 }}
                className="rounded-lg border px-4 py-2 text-sm font-mono"
                style={{ borderColor: `${slide.color}40`, color: slide.color }}
              >
                {slide.example}
              </motion.div>
            )}
          </motion.div>
        </AnimatePresence>
      </div>

      {/* Navigation */}
      <div className="flex items-center justify-center gap-3 p-6 pb-10">
        <Button
          type="button"
          variant="outline"
          size="icon"
          onClick={() => goTo(index - 1)}
          disabled={index === 0}
        >
          <ChevronLeft size={16} />
        </Button>
        <Button type="button" onClick={handleNext} className="px-8">
          {isLast ? "Go to dashboard" : "Next"}
        </Button>
        {!isLast && (
          <Button type="button" variant="outline" size="icon" onClick={() => goTo(index + 1)}>
            <ChevronRight size={16} />
          </Button>
        )}
      </div>
    </div>
  );
}
