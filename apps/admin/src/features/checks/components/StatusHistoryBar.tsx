import { useRef, useEffect, useState, useCallback, useMemo } from "react";
import type { CheckDailyStats } from "@/lib/api";

// ── Types ────────────────────────────────────────────────────────────────────

type HoveredBar = {
  index: number;
  day: CheckDailyStats & { date: string };
  clientX: number;
  clientY: number;
};

type DaySlot = {
  timestamp: number; // day-aligned unix seconds
  date: string;
  stats: CheckDailyStats | null;
};

// ── Colors ───────────────────────────────────────────────────────────────────

const COLOR_UP       = "#22c55e";
const COLOR_DOWN     = "#ef4444";
const COLOR_DEGRADED = "#eab308";
const GAP = 2;

function noDataColor() {
  return document.documentElement.classList.contains("dark") ? "#3f3f46" : "#e4e4e7";
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildSlots(data: CheckDailyStats[], days: number): DaySlot[] {
  const now = Math.floor(Date.now() / 1000);
  const todayStart = Math.floor(now / 86400) * 86400;

  // index by region+day
  const byDay = new Map<number, CheckDailyStats>();
  for (const d of data) {
    const key = Math.floor(d.timestamp / 86400) * 86400;
    const existing = byDay.get(key);
    if (!existing) {
      byDay.set(key, d);
    } else {
      // merge regions: sum counts
      byDay.set(key, {
        ...existing,
        countUp: existing.countUp + d.countUp,
        countDown: existing.countDown + d.countDown,
        countDegraded: existing.countDegraded + d.countDegraded,
      });
    }
  }

  const slots: DaySlot[] = [];
  for (let i = days - 1; i >= 0; i--) {
    const ts = todayStart - i * 86400;
    slots.push({
      timestamp: ts,
      date: new Date(ts * 1000).toLocaleDateString(undefined, { month: "short", day: "numeric" }),
      stats: byDay.get(ts) ?? null,
    });
  }
  return slots;
}

function buildRegionSlots(data: CheckDailyStats[], region: string, days: number): DaySlot[] {
  const now = Math.floor(Date.now() / 1000);
  const todayStart = Math.floor(now / 86400) * 86400;
  const regionData = data.filter((d) => d.region === region);
  const byDay = new Map(regionData.map((d) => [Math.floor(d.timestamp / 86400) * 86400, d]));

  const slots: DaySlot[] = [];
  for (let i = days - 1; i >= 0; i--) {
    const ts = todayStart - i * 86400;
    slots.push({
      timestamp: ts,
      date: new Date(ts * 1000).toLocaleDateString(undefined, { month: "short", day: "numeric" }),
      stats: byDay.get(ts) ?? null,
    });
  }
  return slots;
}

function uptimePct(slots: DaySlot[]): string | null {
  const total = slots.reduce((s, d) => s + (d.stats ? d.stats.countUp + d.stats.countDown + d.stats.countDegraded : 0), 0);
  const up    = slots.reduce((s, d) => s + (d.stats?.countUp ?? 0), 0);
  return total === 0 ? null : ((up / total) * 100).toFixed(2) + "%";
}

// ── Canvas component ──────────────────────────────────────────────────────────

function UptimeCanvas({
  slots,
  barHeight = 36,
  radius = 5,
  onHover,
}: {
  slots: DaySlot[];
  barHeight?: number;
  radius?: number;
  onHover: (bar: Omit<HoveredBar, "day"> & { day: DaySlot } | null) => void;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef    = useRef<HTMLCanvasElement>(null);
  const [width, setWidth] = useState(0);
  const hoveredRef = useRef<number | null>(null);
  const dpr = typeof window !== "undefined" ? (window.devicePixelRatio || 1) : 1;

  const barWidth = useCallback(
    (w: number) => slots.length === 0 ? 0 : Math.max(1, (w - (slots.length - 1) * GAP) / slots.length),
    [slots.length]
  );

  const draw = useCallback(
    (w: number, hi: number | null = null) => {
      const canvas = canvasRef.current;
      if (!canvas || w === 0 || slots.length === 0) return;
      const ctx = canvas.getContext("2d");
      if (!ctx) return;

      const pad = 4;
      const totalH = barHeight + pad * 2;
      canvas.width  = Math.floor(w * dpr);
      canvas.height = Math.floor(totalH * dpr);
      canvas.style.width  = `${w}px`;
      canvas.style.height = `${totalH}px`;
      ctx.scale(dpr, dpr);
      ctx.clearRect(0, 0, w, totalH);

      const bw = barWidth(w);

      for (let i = 0; i < slots.length; i++) {
        const x = Math.round(i * (bw + GAP));
        const actualBW = Math.max(1, Math.round((i + 1) * (bw + GAP)) - x - GAP);
        const slot = slots[i];

        const alpha  = hi === null ? 1 : i === hi ? 1 : i === hi - 1 || i === hi + 1 ? 0.85 : 0.4;
        const scaleY = hi === null ? 1 : i === hi ? 1.12 : 1;
        const scaledH = barHeight * scaleY;
        const y = pad + (barHeight - scaledH) / 2;

        const isFirst = i === 0, isLast = i === slots.length - 1;
        ctx.globalAlpha = alpha;

        if (isFirst || isLast) {
          ctx.save();
          const tl = isFirst ? radius * scaleY : 0;
          const bl = isFirst ? radius * scaleY : 0;
          const tr = isLast  ? radius * scaleY : 0;
          const br = isLast  ? radius * scaleY : 0;
          ctx.beginPath();
          ctx.moveTo(x + tl, y);
          ctx.lineTo(x + actualBW - tr, y);
          if (tr) ctx.arcTo(x + actualBW, y, x + actualBW, y + tr, tr);
          ctx.lineTo(x + actualBW, y + scaledH - br);
          if (br) ctx.arcTo(x + actualBW, y + scaledH, x + actualBW - br, y + scaledH, br);
          ctx.lineTo(x + bl, y + scaledH);
          if (bl) ctx.arcTo(x, y + scaledH, x, y + scaledH - bl, bl);
          ctx.lineTo(x, y + tl);
          if (tl) ctx.arcTo(x, y, x + tl, y, tl);
          ctx.closePath();
          ctx.clip();
        }

        if (!slot.stats) {
          ctx.fillStyle = noDataColor();
          ctx.fillRect(x, y, actualBW, scaledH);
        } else {
          const { countUp, countDown, countDegraded } = slot.stats;
          const total = countUp + countDown + countDegraded;
          const getH = (c: number) => c === 0 ? 0 : Math.max(2, Math.round((c / total) * scaledH));
          const downH = getH(countDown);
          const degH  = getH(countDegraded);
          const upH   = countUp > 0 ? Math.max(0, scaledH - downH - degH) : 0;

          let cy = y + scaledH;
          if (downH > 0) { cy -= downH; ctx.fillStyle = COLOR_DOWN;     ctx.fillRect(x, cy, actualBW, downH); }
          if (degH  > 0) { cy -= degH;  ctx.fillStyle = COLOR_DEGRADED; ctx.fillRect(x, cy, actualBW, degH);  }
          if (upH   > 0) {              ctx.fillStyle = COLOR_UP;       ctx.fillRect(x, y,  actualBW, upH);   }
        }

        if (isFirst || isLast) ctx.restore();
      }
      ctx.globalAlpha = 1;
    },
    [slots, barHeight, radius, dpr, barWidth]
  );

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const ro = new ResizeObserver((entries) => setWidth(entries[0].contentRect.width));
    ro.observe(el);
    setWidth(el.clientWidth);
    return () => ro.disconnect();
  }, []);

  useEffect(() => {
    const obs = new MutationObserver(() => { if (width > 0) draw(width, hoveredRef.current); });
    obs.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
    return () => obs.disconnect();
  }, [width, draw]);

  useEffect(() => { if (width > 0) draw(width, hoveredRef.current); }, [width, slots, draw]);

  function findBar(mouseX: number) {
    const bw = barWidth(width);
    for (let i = 0; i < slots.length; i++) {
      const start = Math.round(i * (bw + GAP));
      const end   = Math.round((i + 1) * (bw + GAP)) - GAP;
      if (mouseX >= start && mouseX < end) return i;
    }
    return -1;
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    const rect = canvasRef.current!.getBoundingClientRect();
    const i    = findBar(e.clientX - rect.left);
    if (i >= 0) {
      onHover({ index: i, day: slots[i], clientX: e.clientX, clientY: e.clientY });
      if (hoveredRef.current !== i) { hoveredRef.current = i; draw(width, i); }
    } else {
      onHover(null);
      if (hoveredRef.current !== null) { hoveredRef.current = null; draw(width, null); }
    }
  }

  function handleMouseLeave() {
    onHover(null);
    if (hoveredRef.current !== null) { hoveredRef.current = null; draw(width, null); }
  }

  return (
    <div ref={containerRef} className="w-full" style={{ borderRadius: radius, overflow: "hidden" }}>
      <canvas
        ref={canvasRef}
        style={{ width: "100%", height: barHeight + 8, imageRendering: "pixelated" }}
        className="cursor-default"
        onMouseMove={handleMouseMove}
        onMouseLeave={handleMouseLeave}
      />
    </div>
  );
}

// ── Public component ──────────────────────────────────────────────────────────

export function StatusHistoryBar({ data, days = 14 }: { data: CheckDailyStats[]; days?: number }) {
  const [hovered, setHovered] = useState<{ day: DaySlot; clientX: number; clientY: number } | null>(null);

  const regions = useMemo(() => Array.from(new Set(data.map((d) => d.region))).sort(), [data]);
  const isMultiRegion = regions.length > 1;

  const rows = useMemo(() => {
    if (!isMultiRegion) return [{ label: null, slots: buildSlots(data, days) }];
    return regions.map((r) => ({ label: r, slots: buildRegionSlots(data, r, days) }));
  }, [data, days, regions, isMultiRegion]);

  const firstSlots = rows[0]?.slots ?? [];
  const fromLabel = firstSlots[0]?.date ?? "";
  const toLabel   = firstSlots[firstSlots.length - 1]?.date ?? "Today";

  return (
    <div className="flex flex-col gap-4">
      {rows.map(({ label, slots }) => (
        <div key={label ?? "default"} className="flex flex-col gap-1.5">
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            {label ? <span className="font-medium">{label}</span> : <span />}
            <span>{uptimePct(slots) ?? "—"} uptime</span>
          </div>
          <UptimeCanvas slots={slots} onHover={(b) => setHovered(b ? { day: b.day, clientX: b.clientX, clientY: b.clientY } : null)} />
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>{fromLabel}</span>
            <span>{toLabel}</span>
          </div>
        </div>
      ))}

      <div className="flex items-center gap-4 text-xs text-muted-foreground">
        {[
          { color: COLOR_UP,       label: "Up"       },
          { color: COLOR_DEGRADED, label: "Degraded" },
          { color: COLOR_DOWN,     label: "Down / Failure" },
        ].map(({ color, label }) => (
          <span key={label} className="flex items-center gap-1.5">
            <span className="inline-block size-2.5 rounded-sm" style={{ background: color }} />
            {label}
          </span>
        ))}
        <span className="flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded-sm" style={{ background: noDataColor() }} />
          No data
        </span>
      </div>

      {hovered && (
        <div
          className="fixed z-50 pointer-events-none bg-popover border border-border rounded-lg shadow-md px-3 py-2 text-xs"
          style={{ left: hovered.clientX + 12, top: hovered.clientY - 80 }}
        >
          <p className="font-semibold">{hovered.day.date}</p>
          {hovered.day.stats ? (
            <>
              {hovered.day.stats.countDown > 0   && <p className="text-red-500">{hovered.day.stats.countDown} down/failure</p>}
              {hovered.day.stats.countDegraded > 0 && <p className="text-amber-500">{hovered.day.stats.countDegraded} degraded</p>}
              <p className="text-green-500">{hovered.day.stats.countUp} up</p>
              {hovered.day.stats.avgLatencyMs != null && (
                <p className="text-muted-foreground">{Math.round(hovered.day.stats.avgLatencyMs)} ms avg</p>
              )}
            </>
          ) : (
            <p className="text-muted-foreground">No data</p>
          )}
        </div>
      )}
    </div>
  );
}
