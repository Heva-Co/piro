<script lang="ts">
  import { AreaChart } from "layerchart";
  import { curveCatmullRom } from "d3-shape";
  import { scaleTime } from "d3-scale";
  import type { DailyStatsDto } from "$lib/api.js";

  interface Props {
    data: DailyStatsDto[];
    metric?: "avg" | "min" | "max";
    height?: number;
    class?: string;
  }

  let { data, metric = "avg", height = 128, class: className = "" }: Props = $props();

  const chartData = $derived(
    data
      .map((d) => ({
        date: new Date(d.timestamp * 1000),
        value:
          metric === "avg" ? d.avgLatencyMs
          : metric === "min" ? d.minLatencyMs
          : d.maxLatencyMs,
      }))
      .filter((d) => d.value !== null && d.value > 0) as { date: Date; value: number }[]
  );
</script>

<div class={className} style="height: {height}px;">
  {#if chartData.length >= 2}
    <AreaChart
      data={chartData}
      x="date"
      xScale={scaleTime()}
      y="value"
      yDomain={[0, null]}
      yNice={true}
      axis="x"
      grid={false}
      series={[{ key: "value", label: "Latency", color: "hsl(var(--chart-1))" }]}
      props={{
        area: { curve: curveCatmullRom, "fill-opacity": 0.3 },
        xAxis: {
          format: (d: Date) =>
            d.toLocaleDateString(undefined, { month: "short", day: "numeric" }),
        },
      }}
    />
  {:else}
    <div class="flex items-center justify-center h-full text-muted-foreground text-sm">
      {chartData.length === 1 ? "Not enough data for trend (need at least 2 days)" : "No latency data available"}
    </div>
  {/if}
</div>
