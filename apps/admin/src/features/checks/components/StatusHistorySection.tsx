import { useCheckHistory } from "@/hooks/useChecks";
import { StatusHistoryBar } from "./StatusHistoryBar";

function StatusHistorySection({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const { data, isLoading } = useCheckHistory(serviceSlug, checkSlug, 14);

  if (isLoading) return <div className="text-sm text-muted-foreground py-4">Loading…</div>;

  return <StatusHistoryBar data={data ?? []} days={14} />;
}

export default StatusHistorySection;
