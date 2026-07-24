import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Plug } from "lucide-react";
import { integrationTypesApi, type IntegrationTypeMeta } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { IntegrationTypeCard } from "./IntegrationTypeCard";
import { IntegrationTypeGridSkeleton } from "./IntegrationTypeGridSkeleton";
import IntegrationManifestDialog from "./IntegrationManifestDialog";

interface Props {
  onSelect: (type: string) => void;
}

export function IntegrationTypeGrid(props: Props) {
  const { onSelect } = props;
  const [manifestType, setManifestType] = useState<IntegrationTypeMeta | null>(null);

  const { data: types = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_TYPES,
    queryFn: integrationTypesApi.list,
  });

  if (isLoading)
    return <IntegrationTypeGridSkeleton />;

  if (types.length === 0)
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-16">
        <Plug size={32} className="text-muted-foreground/40" />
        <p className="text-sm text-muted-foreground">No integration types available.</p>
      </div>
    );

  // Non-creatable types (e.g. Email) have a valid dispatcher but are configured platform-wide
  // (Settings > Email), not by creating an Integration with its own ConfigJson. The backend already
  // returns the types sorted A→Z by label, so no client-side ordering is needed here.
  const creatableTypes = types.filter((t) => t.creatable);

  return (
    <div className="flex flex-col gap-8">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {creatableTypes.map((t) => (
          <IntegrationTypeCard key={t.type} typeMeta={t} onSelect={onSelect} onViewManifest={setManifestType} />
        ))}
      </div>

      <IntegrationManifestDialog
        typeMeta={manifestType}
        open={manifestType !== null}
        onOpenChange={(open) => {
          if (!open) setManifestType(null);
        }}
      />
    </div>
  );
}
