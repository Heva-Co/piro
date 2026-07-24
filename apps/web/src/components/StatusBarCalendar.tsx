"use client";

import { useRef, useEffect, useState, useCallback } from "react";
import type { DailyStatsDto } from "@/src/lib/actions/services";
import { formatLatency, formatUtcDate, cn } from "@/src/lib/utils";
import { TooltipProvider, TooltipRoot, TooltipTrigger, TooltipContent } from "@/src/components/ui/tooltip";

const COLOR_UP = "#22c55e";
const COLOR_DOWN = "#ef4444";
const COLOR_DEGRADED = "#eab308";
const COLOR_MAINTENANCE = "#3b82f6";
const GAP = 0;

function getNoDataColor(): string {
  if (typeof window === "undefined") return "#e4e4e7";
  const isDark = document.documentElement.classList.contains("dark");
  return isDark ? "#3f3f46" : "#e4e4e7";
}

interface HoveredBar {
  index: number;
  x: number;
  data: DailyStatsDto;
}

interface Props {
  data: DailyStatsDto[];
  barHeight?: number;
  radius?: number;
  className?: string;
  onDayClick?: (day: DailyStatsDto) => void;
}

function getStatusLabel(item: DailyStatsDto): string {
  const total = item.countUp + item.countDown + item.countDegraded + item.countMaintenance;
  if (total === 0) return "No data";
  if (item.countMaintenance > 0) return "Maintenance";
  if (item.countDown > 0) return "Outage";
  if (item.countDegraded > 0) return "Degraded";
  return "Operational";
}

function getStatusClass(item: DailyStatsDto): string {
  const total = item.countUp + item.countDown + item.countDegraded + item.countMaintenance;
  if (total === 0) return "text-muted-foreground";
  if (item.countMaintenance > 0) return "text-blue-500";
  if (item.countDown > 0) return "text-red-500";
  if (item.countDegraded > 0) return "text-yellow-500";
  return "text-green-500";
}


export function StatusBarCalendar({
  data,
  barHeight = 40,
  radius = 8,
  className = "",
  onDayClick,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [canvasWidth, setCanvasWidth] = useState(0);
  const [hoveredBar, setHoveredBar] = useState<HoveredBar | null>(null);
  const hoveredIndexRef = useRef<number | null>(null);
  const dpr = typeof window !== "undefined" ? window.devicePixelRatio || 1 : 1;

  const calcBarWidth = useCallback(
    (width: number) => {
      if (!data || data.length === 0 || width === 0) return 0;
      return Math.max(1, (width - (data.length - 1) * GAP) / data.length);
    },
    [data]
  );

  // The CURRENT (animated) scale/opacity of each bar. The rAF loop eases these toward each bar's
  // target for the hovered index every frame, so hover in/out AND moving the focus between bars all
  // transition smoothly instead of snapping. drawBars reads these live values.
  const highlightRef = useRef<number | null>(null);
  const barStateRef = useRef<{ scale: number; opacity: number }[]>([]);
  const rafRef = useRef<number | null>(null);

  /** The steady-state scale/opacity a bar should have given the hovered index. */
  const targetFor = useCallback(
    (i: number, highlightIndex: number | null): { scale: number; opacity: number } => {
      if (highlightIndex === null) return { scale: 1, opacity: 1 };
      if (i === highlightIndex) return { scale: 1.15, opacity: 1 };
      if (i === highlightIndex - 1 || i === highlightIndex + 1) return { scale: 1.08, opacity: 0.9 };
      return { scale: 1, opacity: 0.5 };
    },
    []
  );

  const drawBars = useCallback(
    (width: number, highlightIndex: number | null = null) => {
      const canvas = canvasRef.current;
      if (!canvas || width === 0 || !data || data.length === 0) return;
      const ctx = canvas.getContext("2d");
      if (!ctx) return;

      const padding = 4;
      const totalHeight = barHeight + padding * 2;
      canvas.width = Math.floor(width * dpr);
      canvas.height = Math.floor(totalHeight * dpr);
      ctx.scale(dpr, dpr);

      const barWidth = calcBarWidth(width);
      ctx.clearRect(0, 0, width, totalHeight);

      const barState = barStateRef.current;

      for (let i = 0; i < data.length; i++) {
        const x = Math.round(i * (barWidth + GAP));
        const nextX = Math.round((i + 1) * (barWidth + GAP));
        const bw = Math.max(0, nextX - x - Math.round(GAP));
        const item = data[i];
        const total = item.countUp + item.countDown + item.countDegraded + item.countMaintenance;

        // Read the bar's live animated scale/opacity (eased toward its target by the rAF loop).
        // Falls back to the exact target if the animation state isn't seeded yet (e.g. static redraws).
        const s = barState[i] ?? targetFor(i, highlightIndex);
        const scale = s.scale;
        const opacity = s.opacity;

        ctx.globalAlpha = opacity;
        const scaledH = barHeight * scale;
        const yOffset = padding + (barHeight - scaledH) / 2;
        const isFirst = i === 0,
          isLast = i === data.length - 1;

        if (isFirst || isLast) {
          ctx.save();
          const tl = isFirst ? radius * scale : 0,
            bl = isFirst ? radius * scale : 0;
          const tr = isLast ? radius * scale : 0,
            br = isLast ? radius * scale : 0;
          ctx.beginPath();
          ctx.moveTo(x + tl, yOffset);
          ctx.lineTo(x + bw - tr, yOffset);
          if (tr) ctx.arcTo(x + bw, yOffset, x + bw, yOffset + tr, tr);
          else ctx.lineTo(x + bw, yOffset);
          ctx.lineTo(x + bw, yOffset + scaledH - br);
          if (br) ctx.arcTo(x + bw, yOffset + scaledH, x + bw - br, yOffset + scaledH, br);
          else ctx.lineTo(x + bw, yOffset + scaledH);
          ctx.lineTo(x + bl, yOffset + scaledH);
          if (bl) ctx.arcTo(x, yOffset + scaledH, x, yOffset + scaledH - bl, bl);
          else ctx.lineTo(x, yOffset + scaledH);
          ctx.lineTo(x, yOffset + tl);
          if (tl) ctx.arcTo(x, yOffset, x + tl, yOffset, tl);
          else ctx.lineTo(x, yOffset);
          ctx.closePath();
          ctx.clip();
        }

        if (total === 0) {
          ctx.fillStyle = getNoDataColor();
          ctx.fillRect(x, yOffset, bw, scaledH);
          if (isFirst || isLast) ctx.restore();
          continue;
        }

        const getH = (count: number) => {
          if (count === 0) return 0;
          const raw = Math.round((count / total) * scaledH);
          return count / total >= 0.05 ? Math.max(2, raw) : raw;
        };

        const maintenanceH = getH(item.countMaintenance);
        const downH = getH(item.countDown);
        const degradedH = getH(item.countDegraded);
        const upH = item.countUp > 0 ? Math.max(0, scaledH - maintenanceH - downH - degradedH) : 0;

        let currentY = yOffset + scaledH;
        if (maintenanceH > 0) {
          currentY -= maintenanceH;
          ctx.fillStyle = COLOR_MAINTENANCE;
          ctx.fillRect(x, currentY, bw, maintenanceH);
        }
        if (downH > 0) {
          currentY -= downH;
          ctx.fillStyle = COLOR_DOWN;
          ctx.fillRect(x, currentY, bw, downH);
        }
        if (degradedH > 0) {
          currentY -= degradedH;
          ctx.fillStyle = COLOR_DEGRADED;
          ctx.fillRect(x, currentY, bw, degradedH);
        }
        if (upH > 0) {
          ctx.fillStyle = COLOR_UP;
          ctx.fillRect(x, yOffset, bw, upH);
        }

        if (isFirst || isLast) ctx.restore();
      }
      ctx.globalAlpha = 1;
    },
    [data, barHeight, radius, dpr, calcBarWidth]
  );

  // Animate each bar's progress toward its target (1 when affected by the hovered index, 0 at rest)
  // and redraw per frame until every bar has settled. Cheap linear-ish ease; ~150ms feel.
  const animateHighlight = useCallback(
    (highlightIndex: number | null) => {
      highlightRef.current = highlightIndex;
      if (rafRef.current !== null) return; // a loop is already running; it reads highlightRef

      const step = () => {
        const n = data?.length ?? 0;
        if (barStateRef.current.length !== n)
          barStateRef.current = Array.from({ length: n }, () => ({ scale: 1, opacity: 1 }));
        const barState = barStateRef.current;
        const hi = highlightRef.current;
        let settled = true;

        const EASE = 0.2; // fraction of the remaining distance closed each frame (~150ms feel)
        for (let i = 0; i < n; i++) {
          const target = targetFor(i, hi);
          const s = barState[i];
          s.scale += (target.scale - s.scale) * EASE;
          s.opacity += (target.opacity - s.opacity) * EASE;
          // Snap when close enough so the loop can settle and stop.
          if (Math.abs(target.scale - s.scale) < 0.001) s.scale = target.scale;
          if (Math.abs(target.opacity - s.opacity) < 0.001) s.opacity = target.opacity;
          if (s.scale !== target.scale || s.opacity !== target.opacity) settled = false;
        }

        drawBars(canvasWidth, hi);

        if (settled) {
          rafRef.current = null;
        } else {
          rafRef.current = requestAnimationFrame(step);
        }
      };

      rafRef.current = requestAnimationFrame(step);
    },
    [data, canvasWidth, drawBars, targetFor]
  );

  // Stop any in-flight animation frame on unmount.
  useEffect(() => () => { if (rafRef.current !== null) cancelAnimationFrame(rafRef.current); }, []);

  // Observe container width
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const ro = new ResizeObserver((entries) => {
      setCanvasWidth(entries[0].contentRect.width);
    });
    ro.observe(el);
    setCanvasWidth(el.clientWidth);
    return () => ro.disconnect();
  }, []);

  // Redraw when theme changes (dark class toggled on <html>)
  useEffect(() => {
    const observer = new MutationObserver(() => {
      if (canvasWidth > 0) drawBars(canvasWidth, hoveredIndexRef.current);
    });
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
    return () => observer.disconnect();
  }, [canvasWidth, drawBars]);

  // Redraw on width/data change
  useEffect(() => {
    if (canvasWidth > 0) drawBars(canvasWidth, hoveredIndexRef.current);
  }, [canvasWidth, data, drawBars]);

  function findBarIndex(mouseX: number, width: number): number {
    if (!data || data.length === 0 || width === 0) return -1;
    const barWidth = calcBarWidth(width);
    const totalBW = barWidth + GAP;
    for (let i = 0; i < data.length; i++) {
      const start = Math.round(i * totalBW);
      const end = Math.round((i + 1) * totalBW) - Math.round(GAP);
      if (mouseX >= start && mouseX < end) return i;
    }
    return -1;
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const mouseX = e.clientX - rect.left;
    const found = findBarIndex(mouseX, canvasWidth);
    if (found >= 0) {
      const barWidth = calcBarWidth(canvasWidth);
      const barX = Math.round(found * (barWidth + GAP)) + barWidth / 2;
      setHoveredBar({ index: found, x: barX, data: data[found] });
      if (hoveredIndexRef.current !== found) {
        hoveredIndexRef.current = found;
        animateHighlight(found);
      }
    } else {
      setHoveredBar(null);
      if (hoveredIndexRef.current !== null) {
        hoveredIndexRef.current = null;
        animateHighlight(null);
      }
    }
  }

  function handleMouseLeave() {
    setHoveredBar(null);
    if (hoveredIndexRef.current !== null) {
      hoveredIndexRef.current = null;
      animateHighlight(null);
    }
  }

  function handleClick(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!onDayClick) return;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const mouseX = e.clientX - rect.left;
    const found = findBarIndex(mouseX, canvasWidth);
    if (found >= 0) onDayClick(data[found]);
  }


  const triggerLeft = hoveredBar
    ? Math.max(0, Math.min(hoveredBar.x, canvasWidth))
    : 0;

  return (
    <TooltipProvider delayDuration={0}>
      <TooltipRoot open={!!hoveredBar}>
        <div className={cn("relative w-full", className)} ref={containerRef}>
          <div className="overflow-hidden" style={{ borderRadius: radius }}>
            <canvas
              ref={canvasRef}
              style={{ width: "100%", height: barHeight + 8, imageRendering: "pixelated" }}
              className={onDayClick ? "cursor-pointer" : "cursor-default"}
              onMouseMove={handleMouseMove}
              onMouseLeave={handleMouseLeave}
              onClick={handleClick}
              aria-label="Status history bar chart"
            />
          </div>

          {/* Invisible anchor positioned over the hovered bar — Radix uses this for placement */}
          <TooltipTrigger asChild>
            <span
              aria-hidden
              className="pointer-events-none absolute bottom-0"
              style={{ left: triggerLeft, width: 1, height: barHeight }}
            />
          </TooltipTrigger>

          {hoveredBar && (
            <TooltipContent side="top" className="whitespace-nowrap">
              <span className={getStatusClass(hoveredBar.data)}>{getStatusLabel(hoveredBar.data)}</span>
              <span className="text-muted-foreground"> · </span>
              <span>{formatUtcDate(hoveredBar.data.timestamp)}</span>
              {hoveredBar.data.avgLatencyMs !== null && (
                <>
                  <span className="text-muted-foreground"> · </span>
                  <span>{formatLatency(hoveredBar.data.avgLatencyMs)}</span>
                </>
              )}
            </TooltipContent>
          )}
        </div>
      </TooltipRoot>
    </TooltipProvider>
  );
}
