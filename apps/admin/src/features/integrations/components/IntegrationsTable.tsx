import { Table, TableBody, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { Integration, IntegrationTypeMeta } from "@/lib/actions/integrations";
import IntegrationsTableRow from "./IntegrationsTableRow";

interface Props {
  integrations: Integration[];
  types: IntegrationTypeMeta[];
}

/** Table of existing integrations: name, provider type, configuration status, and a Configure action. */
function IntegrationsTable(props: Props) {
  const { integrations, types } = props;

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Type</TableHead>
          <TableHead>Status</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {integrations.map((integration) => (
          <IntegrationsTableRow
            key={integration.id}
            integration={integration}
            typeMeta={types.find((t) => t.type === integration.type)}
          />
        ))}
      </TableBody>
    </Table>
  );
}

export default IntegrationsTable;
