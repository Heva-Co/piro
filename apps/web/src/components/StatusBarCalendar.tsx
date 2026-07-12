"use client";

import { useRef, useEffect, useState, useCallback } from "react";
import type { DailyStatsDto } from "@/src/lib/api";
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

      for (let i = 0; i < data.length; i++) {
        const x = Math.round(i * (barWidth + GAP));
        const nextX = Math.round((i + 1) * (barWidth + GAP));
        const bw = Math.max(0, nextX - x - Math.round(GAP));
        const item = data[i];
        const total = item.countUp + item.countDown + item.countDegraded + item.countMaintenance;

        let scale = 1,
          opacity = 1;
        if (highlightIndex !== null) {
          if (i === highlightIndex) {
            scale = 1.15;
            opacity = 1;
          } else if (i === highlightIndex - 1 || i === highlightIndex + 1) {
            scale = 1.08;
            opacity = 0.9;
          } else {
            opacity = 0.5;
          }
        }

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
        drawBars(canvasWidth, found);
      }
    } else {
      setHoveredBar(null);
      if (hoveredIndexRef.current !== null) {
        hoveredIndexRef.current = null;
        drawBars(canvasWidth, null);
      }
    }
  }

  function handleMouseLeave() {
    setHoveredBar(null);
    if (hoveredIndexRef.current !== null) {
      hoveredIndexRef.current = null;
      drawBars(canvasWidth, null);
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
