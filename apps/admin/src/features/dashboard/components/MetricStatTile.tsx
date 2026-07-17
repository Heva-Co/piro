import MetricInfo from "@/features/dashboard/components/MetricInfo";

interface Props {
  label: string;
  value: string;
  hint: string;
  /** Optional explanation of how the metric is generated/calculated, shown via a "?" tooltip. */
  info?: string;
}

function MetricStatTile(props: Props) {
  const { label, value, hint, info } = props;

  return (
    <div className="bg-card rounded-lg border border-border p-5 shadow-sm">
      <div className="flex items-center gap-1.5 mb-1">
        <p className="text-sm text-muted-foreground">{label}</p>
        {info && <MetricInfo text={info} />}
      </div>
      <p className="text-3xl font-bold text-foreground">{value}</p>
      <p className="text-xs text-muted-foreground/70 mt-1">{hint}</p>
    </div>
  );
}

export default MetricStatTile;
