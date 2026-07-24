import { useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { PageHeader } from "@/components/PageHeader";
import { ROUTES } from "@/constants/routes";
import { IntegrationTypeGrid } from "../components/IntegrationTypeGrid";
import { IntegrationConfigStep } from "../components/IntegrationConfigStep";

export default function IntegrationFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);
  const [searchParams] = useSearchParams();

  const providerParam = searchParams.get("provider");
  const [selectedType, setSelectedType] = useState<string | null>(providerParam);

  // Edit mode never shows the type picker — the type is fixed at creation time.
  if (!isEdit && !selectedType) {
    return (
      <>
        <PageHeader 
        breadcrumbs={[{ label: "Integrations", to: ROUTES.INTEGRATIONS.LIST }, { label: "New Integration" }]} 
        subheader="Choose a provider to connect."
        />
        <IntegrationTypeGrid onSelect={setSelectedType} />
      </>
    );
  }

  return (
    <IntegrationConfigStep
      id={id}
      initialType={selectedType ?? "Telegram"}
      onBack={() => setSelectedType(null)}
    />
  );
}
