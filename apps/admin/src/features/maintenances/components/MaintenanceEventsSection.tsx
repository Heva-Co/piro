import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { maintenancesApi, type Maintenance, type MaintenanceEvent } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { useFormattedDate } from "@/hooks/useFormattedDate";

const EVENT_STATUS_BADGE: Record<string, string> = {
  Scheduled: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  Ongoing: "bg-green-500/15 text-green-600 dark:text-green-400",
  Completed: "bg-indigo-100 text-indigo-700",
  Cancelled: "bg-muted text-muted-foreground",
};

const DATE_OPTS: Intl.DateTimeFormatOptions = { month: "short", day: "numeric", year: "numeric" };
const TIME_OPTS: Intl.DateTimeFormatOptions = { hour: "numeric", minute: "2-digit" };

interface Props {
  maintenance: Maintenance;
}

export default function MaintenanceEventsSection({ maintenance }: Props) {
  const qc = useQueryClient();
  const [error, setError] = useState("");
  const { formatDate, formatTime } = useFormattedDate();

  function formatEventRange(event: MaintenanceEvent) {
    const startMs = event.startDateTime * 1000;
    const endMs = event.endDateTime * 1000;
    return `${formatDate(startMs, DATE_OPTS)} · ${formatTime(startMs, TIME_OPTS)} – ${formatTime(endMs, TIME_OPTS)}`;
  }

  const cancelEventMutation = useMutation({
    mutationFn: (eventId: number) => maintenancesApi.cancelEvent(maintenance.id, eventId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCE(maintenance.id) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
      setError("");
    },
    onError: () => setError("Failed to cancel event."),
  });

  if (maintenance.upcomingEvents.length === 0) {
    return (
      <div className="rounded-xl border bg-card p-6 text-sm text-muted-foreground text-center">
        No upcoming events.
      </div>
    );
  }

  return (
    <div className="rounded-xl border bg-card overflow-hidden">
      {error && (
        <div className="px-5 py-3 text-sm text-destructive border-b">{error}</div>
      )}
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/40">
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Window</th>
            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
            <th className="px-5 py-2.5" />
          </tr>
        </thead>
        <tbody className="divide-y">
          {maintenance.upcomingEvents.map((event) => {
            const cancellable = event.status === "Scheduled" || event.status === "Ongoing";
            return (
              <tr key={event.id} className="hover:bg-muted/30 transition-colors">
                <td className="px-5 py-3">{formatEventRange(event)}</td>
                <td className="px-5 py-3">
                  <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${EVENT_STATUS_BADGE[event.status] ?? "bg-muted text-muted-foreground"}`}>
                    {event.status}
                  </span>
                </td>
                <td className="px-5 py-3 text-right">
                  {cancellable && (
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={cancelEventMutation.isPending}
                      onClick={() => cancelEventMutation.mutate(event.id)}
                      className="text-destructive hover:text-destructive"
                    >
                      Cancel
                    </Button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
