import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/PageHeader";
import { integrationsApi, integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import IntegrationsTable from "../components/IntegrationsTable";
import IntegrationsTableSkeleton from "../components/IntegrationsTableSkeleton";
import IntegrationsEmptyState from "../components/IntegrationsEmptyState";

function IntegrationsPage() {
  const navigate = useNavigate();

  const { data: integrations = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });
  const { data: types = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: integrationTypesApi.list,
  });

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        breadcrumbs={[{ label: "Integrations" }]}
        subheader="Connected external services Piro uses to deliver notifications, run provider-backed checks, and take actions."
        actions={
          <Button onClick={() => navigate(ROUTES.INTEGRATIONS.NEW)}>
            <Plus />
            New Integration
          </Button>
        }
      />

      <div className="overflow-hidden rounded-xl border bg-card">
        {isLoading ? (
          <IntegrationsTableSkeleton />
        ) : integrations.length === 0 ? (
          <IntegrationsEmptyState />
        ) : (
          <IntegrationsTable integrations={integrations} types={types} />
        )}
      </div>
    </div>
  );
}

export default IntegrationsPage;
