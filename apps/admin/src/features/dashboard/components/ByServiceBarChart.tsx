import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";

interface Props {
  title: string;
  data: { serviceName: string; count: number }[];
  emptyLabel: string;
  seriesName: string;
}

function ByServiceBarChart(props: Props) {
  const { title, data, emptyLabel, seriesName } = props;

  return (
    <div className="flex-1 bg-card rounded-lg border border-border shadow-sm p-5">
      <h3 className="text-sm font-medium text-foreground mb-3">{title}</h3>
      {data.length === 0 ? (
        <p className="text-sm text-muted-foreground py-8 text-center">{emptyLabel}</p>
      ) : (
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={data} layout="vertical" margin={{ top: 4, right: 16, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="var(--color-border)" />
            <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }} axisLine={false} tickLine={false} />
            <YAxis
              type="category"
              dataKey="serviceName"
              tick={{ fontSize: 11, fill: "var(--color-muted-foreground)" }}
              axisLine={false}
              tickLine={false}
              width={90}
            />
            <Tooltip
              contentStyle={{ fontSize: 12, borderRadius: 8, border: "1px solid var(--color-border)" }}
              labelStyle={{ color: "var(--color-foreground)" }}
            />
            <Bar dataKey="count" name={seriesName} fill="var(--color-chart-2)" radius={[0, 4, 4, 0]} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </div>
  );
}

export default ByServiceBarChart;
