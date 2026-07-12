interface Props {
  label: string;
  value: string;
  hint: string;
}

function MetricStatTile(props: Props) {
  const { label, value, hint } = props;

  return (
    <div className="bg-card rounded-lg border border-border p-5 shadow-sm">
      <p className="text-sm text-muted-foreground mb-1">{label}</p>
      <p className="text-3xl font-bold text-foreground">{value}</p>
      <p className="text-xs text-muted-foreground/70 mt-1">{hint}</p>
    </div>
  );
}

export default MetricStatTile;
