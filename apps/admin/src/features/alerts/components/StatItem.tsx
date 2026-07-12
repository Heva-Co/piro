function StatItem({
    icon,
    label,
    value,
    color,
}: {
    icon: React.ReactNode;
    label: string;
    value: number;
    color?: string;
}) {
    return (
        <div className="flex items-center gap-3 px-5 py-3">
            <div className={`shrink-0 ${color ?? "text-muted-foreground"}`}>{icon}</div>
            <div className="flex flex-col">
                <span className="text-xs text-muted-foreground">{label}</span>
                <span className={`text-lg font-bold leading-tight ${color ?? ""}`}>{value}</span>
            </div>
        </div>
    );
}

export default StatItem;