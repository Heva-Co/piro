<script lang="ts">
  import { onMount } from "svelte";
  import type { DailyStatsDto } from "$lib/api.js";

  interface Props {
    data: DailyStatsDto[];
    barHeight?: number;
    radius?: number;
    class?: string;
    onDayClick?: (day: DailyStatsDto) => void;
  }

  let { data, barHeight = 40, radius = 8, class: className = "", onDayClick }: Props = $props();

  let canvas = $state<HTMLCanvasElement | null>(null);
  let container = $state<HTMLDivElement | null>(null);
  let tooltipEl = $state<HTMLDivElement | null>(null);
  let canvasWidth = $state(0);
  let hoveredBar = $state<{ index: number; x: number; data: DailyStatsDto } | null>(null);
  let mounted = $state(false);
  let dpr = $state(1);
  let resizeObserver: ResizeObserver | null = null;
  let hoveredIndex = $state<number | null>(null);

  const colorUp = "#22c55e";
  const colorDown = "#ef4444";
  const colorDegraded = "#eab308";
  const colorMaintenance = "#3b82f6";
  const gap = 0;

  let tooltipStyle = $derived.by(() => {
    if (!hoveredBar || !tooltipEl) return `left: 0px; bottom: ${barHeight + 16}px; opacity: 0;`;
    const tooltipWidth = tooltipEl.offsetWidth;
    const half = tooltipWidth / 2;
    let left = hoveredBar.x;
    if (left < half + 4) left = half + 4;
    if (left > canvasWidth - half - 4) left = canvasWidth - half - 4;
    return `left: ${left}px; bottom: ${barHeight + 16}px;`;
  });

  function calculateBarWidth(): number {
    if (!data || data.length === 0 || canvasWidth === 0) return 0;
    return Math.max(1, (canvasWidth - (data.length - 1) * gap) / data.length);
  }

  function roundedRectPath(
    ctx: CanvasRenderingContext2D,
    x: number, y: number, width: number, height: number,
    r: number, roundLeft: boolean, roundRight: boolean
  ) {
    const tl = roundLeft ? r : 0, bl = roundLeft ? r : 0;
    const tr = roundRight ? r : 0, br = roundRight ? r : 0;
    ctx.beginPath();
    ctx.moveTo(x + tl, y);
    ctx.lineTo(x + width - tr, y);
    if (tr) ctx.arcTo(x + width, y, x + width, y + tr, tr); else ctx.lineTo(x + width, y);
    ctx.lineTo(x + width, y + height - br);
    if (br) ctx.arcTo(x + width, y + height, x + width - br, y + height, br); else ctx.lineTo(x + width, y + height);
    ctx.lineTo(x + bl, y + height);
    if (bl) ctx.arcTo(x, y + height, x, y + height - bl, bl); else ctx.lineTo(x, y + height);
    ctx.lineTo(x, y + tl);
    if (tl) ctx.arcTo(x, y, x + tl, y, tl); else ctx.lineTo(x, y);
    ctx.closePath();
  }

  function drawBars(highlightIndex: number | null = null) {
    if (!canvas || canvasWidth === 0 || !mounted || !data || data.length === 0) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const padding = 4;
    const totalHeight = barHeight + padding * 2;
    canvas.width = Math.floor(canvasWidth * dpr);
    canvas.height = Math.floor(totalHeight * dpr);
    ctx.scale(dpr, dpr);

    const barWidth = calculateBarWidth();
    const noDataColor = "#e4e4e7";
    ctx.clearRect(0, 0, canvasWidth, totalHeight);

    for (let i = 0; i < data.length; i++) {
      const x = Math.round(i * (barWidth + gap));
      const nextX = Math.round((i + 1) * (barWidth + gap));
      const bw = Math.max(0, nextX - x - Math.round(gap));
      const item = data[i];
      const total = item.countUp + item.countDown + item.countDegraded + item.countMaintenance;

      let scale = 1, opacity = 1;
      if (highlightIndex !== null) {
        if (i === highlightIndex) { scale = 1.15; opacity = 1; }
        else if (i === highlightIndex - 1 || i === highlightIndex + 1) { scale = 1.08; opacity = 0.9; }
        else { opacity = 0.5; }
      }

      ctx.globalAlpha = opacity;
      const scaledH = barHeight * scale;
      const yOffset = padding + (barHeight - scaledH) / 2;
      const isFirst = i === 0, isLast = i === data.length - 1;

      ctx.save();
      if (isFirst || isLast) {
        roundedRectPath(ctx, x, yOffset, bw, scaledH, radius * scale, isFirst, isLast);
        ctx.clip();
      }

      if (total === 0) {
        ctx.fillStyle = noDataColor;
        ctx.fillRect(x, yOffset, bw, scaledH);
        ctx.restore();
        continue;
      }

      let currentY = yOffset + scaledH;
      // Use exact proportional height. Only enforce a 2px minimum for statuses
      // that represent ≥5% of the day, so single-minute blips don't show.
      const getH = (count: number) => {
        if (count === 0) return 0;
        const raw = Math.round((count / total) * scaledH);
        return count / total >= 0.05 ? Math.max(2, raw) : raw;
      };

      const maintenanceH = getH(item.countMaintenance);
      const downH = getH(item.countDown);
      const degradedH = getH(item.countDegraded);
      const upH = item.countUp > 0 ? Math.max(0, scaledH - maintenanceH - downH - degradedH) : 0;

      if (maintenanceH > 0) { currentY -= maintenanceH; ctx.fillStyle = colorMaintenance; ctx.fillRect(x, currentY, bw, maintenanceH); }
      if (downH > 0) { currentY -= downH; ctx.fillStyle = colorDown; ctx.fillRect(x, currentY, bw, downH); }
      if (degradedH > 0) { currentY -= degradedH; ctx.fillStyle = colorDegraded; ctx.fillRect(x, currentY, bw, degradedH); }
      if (upH > 0) { ctx.fillStyle = colorUp; ctx.fillRect(x, yOffset, bw, upH); }

      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  function handleMouseMove(event: MouseEvent) {
    if (!canvas || !data || data.length === 0 || canvasWidth === 0) return;
    const rect = canvas.getBoundingClientRect();
    const mouseX = event.clientX - rect.left;
    const barWidth = calculateBarWidth();
    const totalBW = barWidth + gap;

    let found = -1;
    for (let i = 0; i < data.length; i++) {
      const start = Math.round(i * totalBW);
      const end = Math.round((i + 1) * totalBW) - Math.round(gap);
      if (mouseX >= start && mouseX < end) { found = i; break; }
    }

    if (found >= 0) {
      const barX = Math.round(found * totalBW) + Math.round(barWidth) / 2;
      hoveredBar = { index: found, x: barX, data: data[found] };
      if (hoveredIndex !== found) { hoveredIndex = found; drawBars(found); }
    } else {
      hoveredBar = null;
      if (hoveredIndex !== null) { hoveredIndex = null; drawBars(null); }
    }
  }

  function handleMouseLeave() {
    hoveredBar = null;
    if (hoveredIndex !== null) { hoveredIndex = null; drawBars(null); }
  }

  function handleClick(event: MouseEvent) {
    if (!onDayClick || !canvas || !data || data.length === 0 || canvasWidth === 0) return;
    const rect = canvas.getBoundingClientRect();
    const mouseX = event.clientX - rect.left;
    const barWidth = calculateBarWidth();
    const totalBW = barWidth + gap;
    for (let i = 0; i < data.length; i++) {
      const start = Math.round(i * totalBW);
      const end = Math.round((i + 1) * totalBW) - Math.round(gap);
      if (mouseX >= start && mouseX < end) { onDayClick(data[i]); break; }
    }
  }

  function formatDay(ts: number): string {
    return new Date(ts * 1000).toLocaleDateString(undefined, { month: "short", day: "numeric" });
  }

  function formatLatency(ms: number | null): string {
    if (ms === null) return "";
    if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
    return `${Math.round(ms)}ms`;
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

  onMount(() => {
    mounted = true;
    dpr = window.devicePixelRatio || 1;
    return () => resizeObserver?.disconnect();
  });

  $effect(() => {
    if (container && !resizeObserver) {
      canvasWidth = container.clientWidth;
      resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) canvasWidth = entry.contentRect.width;
      });
      resizeObserver.observe(container);
    }
  });

  $effect(() => {
    const _w = canvasWidth, _d = data, _m = mounted;
    if (_w > 0 && _d && _d.length > 0 && _m) drawBars(hoveredIndex);
  });
</script>

<div class="relative w-full {className}" bind:this={container}>
  <div class="overflow-hidden" style="border-radius: {radius}px;">
    <canvas
      bind:this={canvas}
      style="width: 100%; height: {barHeight + 8}px;"
      class={onDayClick ? "cursor-pointer" : "cursor-default"}
      onmousemove={handleMouseMove}
      onmouseleave={handleMouseLeave}
      onclick={handleClick}
      aria-label="Status history bar chart"
    ></canvas>
  </div>

  {#if hoveredBar}
    <div
      bind:this={tooltipEl}
      class="bg-popover text-popover-foreground pointer-events-none absolute z-20 w-max -translate-x-1/2 rounded-md border px-2 py-1 text-xs font-medium whitespace-nowrap"
      style={tooltipStyle}
    >
      <span class={getStatusClass(hoveredBar.data)}>{getStatusLabel(hoveredBar.data)}</span>
      <span class="text-muted-foreground"> · </span>
      <span>{formatDay(hoveredBar.data.timestamp)}</span>
      {#if hoveredBar.data.avgLatencyMs !== null}
        <span class="text-muted-foreground"> · </span>
        <span>{formatLatency(hoveredBar.data.avgLatencyMs)}</span>
      {/if}
    </div>
  {/if}
</div>

<style>
  canvas {
    image-rendering: -webkit-optimize-contrast;
    image-rendering: crisp-edges;
    image-rendering: pixelated;
  }
</style>
