import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import { useAllChecks, useCheckTypeLabel } from "@/hooks/useChecks";
import type { CheckSummary } from "@/lib/actions/checks";
import { ROUTES } from "@/constants/routes";
import ChecksStatsBar from "../components/list/ChecksStatsBar";
import ChecksStatsBarSkeleton from "../components/list/ChecksStatsBarSkeleton";
import ChecksSearchBar from "../components/list/ChecksSearchBar";
import ChecksTable from "../components/list/ChecksTable";
import ChecksTableSkeleton from "../components/list/ChecksTableSkeleton";

function ChecksPage() {
  const navigate = useNavigate();
  const { data: checks, isLoading, refetch } = useAllChecks();
  const typeLabel = useCheckTypeLabel();
  const [search, setSearch] = useState("");

  function handleViewLogs(check: CheckSummary) {
    navigate(ROUTES.CHECKS.LOGS(check.serviceSlug, check.slug));
  }

  function handleConfigure(check: CheckSummary) {
    navigate(ROUTES.CHECKS.DETAIL(check.serviceSlug, check.slug));
  }

  function handleNavigateService(check: CheckSummary) {
    navigate(ROUTES.SERVICES.DETAIL(check.serviceSlug));
  }

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[{ label: "Checks" }]}
        subheader="All monitoring checks across every service."
      />

      <div className="flex flex-col gap-6">
        {/* Stats */}
        {isLoading ? <ChecksStatsBarSkeleton /> : <ChecksStatsBar checks={checks ?? []} />}

        {/* Table card */}
        <div className="rounded-xl border bg-card overflow-hidden">
        <ChecksSearchBar search={search} onSearchChange={setSearch} onRefetch={refetch} />

        {isLoading ? (
          <ChecksTableSkeleton />
        )  : (
          <ChecksTable
            checks={checks}
            typeLabel={typeLabel}
            onViewLogs={handleViewLogs}
            onConfigure={handleConfigure}
            onNavigateService={handleNavigateService}
          />
        )}
        </div>
      </div>
    </PageContainer>
  );
}

export default ChecksPage;
