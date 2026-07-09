import { StatusPill } from "@/components/StatusBadge";
import { useCheckLogs } from "@/hooks/useChecks";
import { formatTimestamp } from "@/utils/date";

function RecentLogsSection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
    const { data: logs, isLoading } = useCheckLogs(serviceSlug, checkSlug);

    if (isLoading) return <div className="text-sm text-muted-foreground py-2">Loading…</div>;

    if (!logs || logs.length === 0) {
        return <div className="text-sm text-muted-foreground text-center py-6">No logs yet.</div>;
    }

    return (
        <div className="rounded-xl border bg-card overflow-hidden">
            <table className="min-w-full text-sm">
                <thead>
                    <tr className="border-b bg-muted/40">
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Time</th>
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                        <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Latency</th>
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
                            <td className="px-5 py-2.5 text-sm text-muted-foreground">
                                {log.latencyMs != null ? `${Math.round(log.latencyMs)} ms` : "N/A"}
                            </td>
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