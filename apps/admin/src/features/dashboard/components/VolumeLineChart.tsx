import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";

interface Props {
  title: string;
  data: { date: string; count: number }[];
  emptyLabel: string;
  seriesName: string;
}

function VolumeLineChart(props: Props) {
  const { title, data, emptyLabel, seriesName } = props;

  return (
    <div className="flex-1 bg-card rounded-lg border border-border shadow-sm p-5">
      <h3 className="text-sm font-medium text-foreground mb-3">{title}</h3>
      {data.length === 0 ? (
        <p className="text-sm text-muted-foreground py-8 text-center">{emptyLabel}</p>
      ) : (
        <ResponsiveContainer width="100%" height={220}>
          <LineChart data={data} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="date" tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} />
            <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} width={28} />
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
