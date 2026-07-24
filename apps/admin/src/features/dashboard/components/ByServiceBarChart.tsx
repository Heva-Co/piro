import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { ChartColumnBig } from "lucide-react";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import MetricInfo from "@/features/dashboard/components/MetricInfo";

interface Props {
  title: string;
  data: { serviceName: string; count: number }[];
  emptyLabel: string;
  seriesName: string;
  /** Optional explanation of how the metric is generated/calculated, shown via a "?" tooltip. */
  info?: string;
}

function ByServiceBarChart(props: Props) {
  const { title, data, emptyLabel, seriesName, info } = props;

  return (
    <div className="flex-1 bg-card rounded-lg border border-border shadow-sm p-5">
      <div className="flex items-center gap-1.5 mb-3">
        <h3 className="text-sm font-medium text-foreground">{title}</h3>
        {info && <MetricInfo text={info} />}
      </div>
      {data.length === 0 ? (
        <Empty className="py-8">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <ChartColumnBig />
            </EmptyMedia>
            <EmptyTitle>{emptyLabel}</EmptyTitle>
            <EmptyDescription>Data appears here once there is activity to show.</EmptyDescription>
          </EmptyHeader>
        </Empty>
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
            {/* Cap thickness so a chart with only one or two services doesn't render huge bars. */}
            <Bar dataKey="count" name={seriesName} fill="var(--color-chart-2)" radius={[0, 4, 4, 0]} maxBarSize={32} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </div>
  );
}

export default ByServiceBarChart;
