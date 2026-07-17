import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
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

  return (
    <div className="flex-1 bg-card rounded-lg border border-border shadow-sm p-5">
      <div className="flex items-center gap-1.5 mb-3">
        <h3 className="text-sm font-medium text-foreground">{title}</h3>
        {info && <MetricInfo text={info} />}
      </div>
      {data.length === 0 ? (
        <p className="text-sm text-muted-foreground py-8 text-center">{emptyLabel}</p>
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
