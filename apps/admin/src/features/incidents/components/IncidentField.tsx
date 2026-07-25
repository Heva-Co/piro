interface Props {
  label: React.ReactNode;
  hint?: React.ReactNode;
  children: React.ReactNode;
}

function IncidentField(props: Props) {
  const { label, hint, children } = props;
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-sm font-semibold text-foreground">{label}</label>
      {children}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

export default IncidentField;
