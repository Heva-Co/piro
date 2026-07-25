interface Props {
  label: string;
  children: React.ReactNode;
}

function AlertField(props: Props) {
  const { label, children } = props;
  return (
    <div className="flex flex-col gap-1">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <div className="text-sm">{children}</div>
    </div>
  );
}

export default AlertField;
