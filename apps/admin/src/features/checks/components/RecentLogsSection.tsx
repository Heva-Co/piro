import { ClipboardList } from "lucide-react";
import { StatusPill } from "@/components/StatusBadge";
import { useCheckLogs } from "@/hooks/useChecks";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import { type CheckDimension, dimensionLabel, isNumericDimension } from "@/types/checks";

interface Props {
    serviceSlug: string;
    checkSlug: string;
    /** The check's declared dimensions — drives one column per numeric measurement (RFC 0016). */
    dimensions: readonly CheckDimension[];
}

function RecentLogsSection(props: Props) {
    const { serviceSlug, checkSlug, dimensions } = props;
    const { data: logs, isLoading } = useCheckLogs(serviceSlug, checkSlug);
    const { formatTimestamp } = useFormattedDate();

    // A column per numeric dimension the check reports (Status is a category, shown in its own column).
    const metricDimensions = dimensions.filter(isNumericDimension);

    if (isLoading) return <div className="text-sm text-muted-foreground py-2">Loading…</div>;

    if (!logs || logs.length === 0) {
        return (
            <Empty className="py-10">
                <EmptyHeader>
                    <EmptyMedia variant="icon">
                        <ClipboardList />
                    </EmptyMedia>
                    <EmptyTitle>No logs yet</EmptyTitle>
                    <EmptyDescription>Execution results appear here after this check runs.</EmptyDescription>
                </EmptyHeader>
            </Empty>
        );
    }

    return (
        <div className="rounded-xl border bg-card overflow-hidden">
            <table className="min-w-full text-sm">
                <thead>
                    <tr className="border-b bg-muted/40">
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Time</th>
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                        {metricDimensions.map((d) => (
                            <th key={d.name} className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">
                                {dimensionLabel(d.name)}{d.unit ? ` (${d.unit})` : ""}
                            </th>
                        ))}
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Region</th>
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Message</th>
                    </tr>
                </thead>
                <tbody className="divide-y">
                    {logs.map((log) => (
                        <tr key={log.timestamp} className="hover:bg-muted/30 transition-colors">
                            <td className="px-5 py-2.5 text-xs text-muted-foreground">
                                {formatTimestamp(log.timestamp)}
                            </td>
                            <td className="px-5 py-2.5">
                                <StatusPill status={log.status} dataType={log.dataType} />
                            </td>
                            {metricDimensions.map((d) => {
                                const value = log.dimensions?.[d.name];
                                return (
                                    <td key={d.name} className="px-5 py-2.5 text-sm text-muted-foreground">
                                        {value != null ? Math.round(value * 100) / 100 : "—"}
                                    </td>
                                );
                            })}
                            <td className="px-5 py-2.5 text-xs text-muted-foreground">
                                {log.workerRegion}
                            </td>
                            <td className="px-5 py-2.5 text-xs text-muted-foreground">
                                {log.errorMessage ?? ""}
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

export default RecentLogsSection;
