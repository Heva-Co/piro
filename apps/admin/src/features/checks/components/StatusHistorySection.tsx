import { CalendarClock } from "lucide-react";
import { useCheckHistory } from "@/hooks/useChecks";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import { StatusHistoryBar } from "./StatusHistoryBar";

function StatusHistorySection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data, isLoading } = useCheckHistory(serviceSlug, checkSlug, 14);

  if (isLoading) return <div className="text-sm text-muted-foreground py-4">Loading…</div>;

  // Nothing reported yet (new check, or it hasn't run) — show an empty state instead of a bar of
  // all-grey "no data" days, which reads as if there were data.
  if (!data || data.length === 0) {
    return (
      <Empty className="py-10">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <CalendarClock />
          </EmptyMedia>
          <EmptyTitle>No status history yet</EmptyTitle>
          <EmptyDescription>Once this check runs, its daily up/down history shows here.</EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  return <StatusHistoryBar data={data} days={14} />;
}

export default StatusHistorySection;
