import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Icon } from "@iconify/react";
import { ExternalLink, MoreVertical } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  integrationActionsApi,
  type ActionContext,
  type IntegrationActionDescriptor,
} from "@/lib/actions/integration-actions";
import ActionDialog from "./ActionDialog";

interface Props {
  context: ActionContext;
  targetId: number;
}

/**
 * Generic container for the integration (3rd-party) actions applicable to an object of the given
 * context (RFC 0012 §4.3–4.4). The backend decides *which* actions apply; the page decides *where*
 * this container goes. Actions are collapsed under a kebab (⋮) menu so the page header stays clean as
 * more integrations contribute actions (RFC 0012 §8). External references already created for the
 * object are shown inline as outbound links. Selecting an action with input opens ActionDialog.
 */
function ActionButtons(props: Props) {
  const { context, targetId } = props;

  const qc = useQueryClient();
  const [activeDescriptor, setActiveDescriptor] = useState<IntegrationActionDescriptor | null>(null);

  const { data: descriptors = [] } = useQuery({
    queryKey: ["integration-actions", context],
    queryFn: () => integrationActionsApi.list(context),
  });

  const { data: references = [] } = useQuery({
    queryKey: ["integration-references", context, targetId],
    queryFn: () => integrationActionsApi.references(context, targetId),
  });

  if (descriptors.length === 0 && references.length === 0) return null;

  function onExecuted() {
    // The action created a new external reference — refresh the inline links.
    qc.invalidateQueries({ queryKey: ["integration-references", context, targetId] });
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      {references.map((reference) => (
        <a
          key={`${reference.integrationId}:${reference.externalId}`}
          href={reference.url}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center gap-1.5 rounded-lg border px-3 py-2 text-xs font-medium text-muted-foreground hover:text-foreground"
        >
          <ExternalLink size={13} />
          {reference.label}
        </a>
      ))}

      {descriptors.length > 0 && (
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<Button variant="outline" size="icon" aria-label="Integration actions" />}
          >
            <MoreVertical size={16} />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuGroup>
              {descriptors.map((descriptor) => (
                <DropdownMenuItem
                  key={`${descriptor.integrationId}:${descriptor.actionId}`}
                  onClick={() => setActiveDescriptor(descriptor)}
                >
                  {descriptor.iconifyIcon && <Icon icon={descriptor.iconifyIcon} className="size-4" />}
                  {descriptor.label}
                </DropdownMenuItem>
              ))}
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      )}

      {activeDescriptor && (
        <ActionDialog
          descriptor={activeDescriptor}
          context={context}
          targetId={targetId}
          open={activeDescriptor !== null}
          onOpenChange={(open) => !open && setActiveDescriptor(null)}
          onExecuted={onExecuted}
        />
      )}
    </div>
  );
}

export default ActionButtons;
