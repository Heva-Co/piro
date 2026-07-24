import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { ChartLine } from "lucide-react";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import MetricInfo from "@/features/dashboard/components/MetricInfo";

interface Props {
  title: string;
  data: { date: string; count: number }[];
  emptyLabel: string;
  seriesName: string;
  /** Optional explanation of how the metric is generated/calculated, shown via a "?" tooltip. */
  info?: string;
}

function VolumeLineChart(props: Props) {
  const { title, data, emptyLabel, seriesName, info } = props;

  // The backend returns one bucket per day in the range (so the x-axis is continuous when there IS
  // data), which means an all-zero series still has length > 0. Treat "every day is zero" as empty so a
  // period with no incidents shows the empty state instead of a flat line pinned to zero.
  const hasData = data.some((d) => d.count > 0);

  return (
    <div className="flex-1 bg-card rounded-lg border border-border shadow-sm p-5">
      <div className="flex items-center gap-1.5 mb-3">
        <h3 className="text-sm font-medium text-foreground">{title}</h3>
        {info && <MetricInfo text={info} />}
      </div>
      {!hasData ? (
        <Empty className="py-8">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <ChartLine />
            </EmptyMedia>
            <EmptyTitle>{emptyLabel}</EmptyTitle>
            <EmptyDescription>Data appears here once there is activity to show.</EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <ResponsiveContainer width="100%" height={220}>
          <LineChart data={data} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="date" tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} />
            <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} width={44} />
            <Tooltip
              contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--color-border)" }}
              labelStyle={{ color: "var(--color-foreground)" }}
            />
            <Line type="monotone" dataKey="count" name={seriesName} stroke="var(--color-chart-1)" strokeWidth={2} dot={{ r: 3 }} />
          </LineChart>
        </ResponsiveContainer>
      )}
    </div>
  );
}

export default VolumeLineChart;
