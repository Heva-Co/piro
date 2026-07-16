import { useQuery } from "@tanstack/react-query";
import { Plug } from "lucide-react";
import { integrationTypesApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";
import { IntegrationTypeCard } from "./IntegrationTypeCard";
import { IntegrationTypeGridSkeleton } from "./IntegrationTypeGridSkeleton";

interface Props {
  onSelect: (type: string) => void;
}

export function IntegrationTypeGrid(props: Props) {
  const { onSelect } = props;

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
  // (Settings > Email), not by creating an Integration with its own ConfigJson.
  const creatableTypes = types.filter((t) => t.creatable);
  const thirdParty = creatableTypes.filter((t) => t.category === "ThirdParty");
  const notification = creatableTypes.filter((t) => t.category === "Notification");

  return (
    <div className="flex flex-col gap-8">
      {thirdParty.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Third-party</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {thirdParty.map((t) => (
              <IntegrationTypeCard key={t.type} typeMeta={t} onSelect={onSelect} />
            ))}
          </div>
        </section>
      )}
      {notification.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Notification</h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {notification.map((t) => (
              <IntegrationTypeCard key={t.type} typeMeta={t} onSelect={onSelect} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
