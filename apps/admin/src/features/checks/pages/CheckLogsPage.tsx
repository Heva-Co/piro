import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Settings } from "lucide-react";
import { AutoRefreshButton } from "@/components/AutoRefreshButton";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import { StatusPill } from "@/components/StatusBadge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { useCheck, useCheckTypeLabel } from "@/hooks/useChecks";
import { checksApi } from "@/lib/actions/checks";
import { ROUTES } from "@/constants/routes";
import LogsFilters from "../components/logs/LogsFilters";
import LogsTable from "../components/logs/LogsTable";
import LogsTableSkeleton from "../components/logs/LogsTableSkeleton";
import LogsPagination from "../components/logs/LogsPagination";

const PAGE_SIZE = 20;

function CheckLogsPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{ slug: string; checkSlug: string }>();
  const navigate = useNavigate();

  const { data: check } = useCheck(serviceSlug!, checkSlug!);
  const typeLabel = useCheckTypeLabel();

  const [limit, setLimit] = useState(50);
  const [region, setRegion] = useState("");
  const [statusFilter, setStatusFilter] = useState<"" | "UP" | "DOWN">("");
  const [page, setPage] = useState(1);

  const { data: logs, isLoading, refetch } = useQuery({
    queryKey: ["check-logs-full", serviceSlug, checkSlug, limit, region],
    queryFn: () => checksApi.logs(serviceSlug!, checkSlug!, {
      limit,
      region: region || undefined,
    }),
    enabled: !!serviceSlug && !!checkSlug,
  });

  // Client-side status filter + pagination
  const filtered = (logs ?? []).filter((l) => {
    if (statusFilter && l.status.toUpperCase() !== statusFilter) return false;
    return true;
  });

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paginated = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  // Collect unique regions from data. "monitor" is the sentinel WorkerRegion for datapoints that never
  // ran on a worker (MONITOR_OUTAGE / UNSCHEDULABLE), not a real region — exclude it from the filter.
  const regions = Array.from(new Set((logs ?? []).map((l) => l.workerRegion).filter((r) => r && r !== "monitor")));

  function handleLimitChange(newLimit: number) {
    setLimit(newLimit);
    setPage(1);
  }

  function handleRegionChange(r: string) {
    setRegion(r);
    setPage(1);
  }

  function handleStatusChange(s: "" | "UP" | "DOWN") {
    setStatusFilter(s);
    setPage(1);
  }

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "Services", onClick: () => navigate(ROUTES.SERVICES.LIST) },
          { label: serviceSlug!, onClick: () => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!)) },
          { label: check?.name ?? checkSlug!, onClick: () => navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, checkSlug!)) },
          { label: "Logs" },
        ]}
        actions={
          <>
            <Button variant="outline" onClick={() => navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, checkSlug!))}>
              <Settings size={14} />
              Configure
            </Button>
            <AutoRefreshButton onRefetch={refetch} />
          </>
        }
      />

      <div className="flex flex-col gap-6">
        {/* Check info */}
        {check && (
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-bold">{check.name}</h1>
            <Badge variant="outline">{typeLabel(check.type)}</Badge>
            <StatusPill status={check.currentStatus} />
          </div>
        )}

        <LogsFilters
          statusFilter={statusFilter}
          region={region}
          limit={limit}
          regions={regions}
          onStatusChange={handleStatusChange}
          onRegionChange={handleRegionChange}
          onLimitChange={handleLimitChange}
        />

        {/* Table */}
        <div className="rounded-xl border bg-card overflow-hidden">
          {isLoading ? (
            <LogsTableSkeleton />
          ) : paginated.length === 0 ? (
            <Empty className="border-0">
              <EmptyHeader>
                <EmptyTitle className="text-muted-foreground font-normal">No logs match your filters.</EmptyTitle>
              </EmptyHeader>
            </Empty>
          ) : (
            <LogsTable logs={paginated} />
          )}
        </div>

        {/* Pagination */}
        {!isLoading && totalPages > 1 && (
          <LogsPagination
            page={page}
            totalPages={totalPages}
            totalEntries={filtered.length}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
          />
        )}
      </div>
    </PageContainer>
  );
}

export default CheckLogsPage;
