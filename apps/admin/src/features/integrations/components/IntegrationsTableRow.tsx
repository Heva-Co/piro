import type { MouseEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Icon } from "@iconify/react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { TableCell, TableRow } from "@/components/ui/table";
import { ROUTES } from "@/constants/routes";
import type { Integration, IntegrationTypeMeta } from "@/lib/actions/integrations";
import { getIntegrationHealth } from "../utils/integrationHealth";
import IntegrationHealthBadge from "./IntegrationHealthBadge";
import IntegrationOAuthStatusBadge from "./IntegrationOAuthStatusBadge";

interface Props {
  integration: Integration;
  typeMeta: IntegrationTypeMeta | undefined;
}

/** One integration row: whole row navigates to the detail page; the Configure button does the same. */
function IntegrationsTableRow(props: Props) {
  const { integration, typeMeta } = props;
  const navigate = useNavigate();

  const health = getIntegrationHealth(integration.configJson, typeMeta);

  function openDetail() {
    navigate(ROUTES.INTEGRATIONS.DETAIL(integration.id));
  }

  function handleConfigureClick(e: MouseEvent) {
    // Stop the row's own click from also firing — a single navigation is enough.
    e.stopPropagation();
    openDetail();
  }

  return (
    <TableRow onClick={openDetail} className="cursor-pointer">
      <TableCell className="font-medium">
        <div>{integration.name}</div>
        {integration.description && (
          <div className="mt-0.5 text-xs text-muted-foreground">{integration.description}</div>
        )}
      </TableCell>
      <TableCell>
        <Badge variant="outline">
          {typeMeta?.iconifyIcon && <Icon icon={typeMeta.iconifyIcon} className="size-3.5" />}
          {typeMeta?.label ?? integration.type}
        </Badge>
      </TableCell>
      <TableCell>
        {health.status === "oauth" ? (
          <IntegrationOAuthStatusBadge integrationId={integration.id} />
        ) : (
          <IntegrationHealthBadge health={health} />
        )}
      </TableCell>
      <TableCell className="text-right">
        <Button variant="outline" size="sm" onClick={handleConfigureClick}>
          Configure
        </Button>
      </TableCell>
    </TableRow>
  );
}

export default IntegrationsTableRow;
