import { Icon } from "@iconify/react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import type { IntegrationTypeMeta } from "@/lib/actions/integrations";
import { capabilityLabel } from "../utils/manifestLabels";
import IntegrationManifestFieldRow from "./IntegrationManifestFieldRow";

interface Props {
  typeMeta: IntegrationTypeMeta | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Read-only reference for an integration type's manifest: identity, declared capabilities, and the
 * full config field schema (with secret/required/generated markers). Opened from the integrations list
 * so an admin can see exactly what a provider needs without leaving the page. Controlled: the list owns
 * which type is shown.
 */
function IntegrationManifestDialog(props: Props) {
  const { typeMeta, open, onOpenChange } = props;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        {typeMeta && (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2.5 text-lg">
                {typeMeta.iconifyIcon && <Icon icon={typeMeta.iconifyIcon} className="size-7" />}
                {typeMeta.label ?? typeMeta.type}
              </DialogTitle>
              {typeMeta.description && (
                <DialogDescription>{typeMeta.description}</DialogDescription>
              )}
            </DialogHeader>

            {typeMeta.channelOnly && (
              <div className="flex flex-wrap items-center gap-1.5">
                <Badge variant="secondary">Per-channel</Badge>
              </div>
            )}

            {typeMeta.capabilities.length > 0 && (
              <div>
                <h3 className="mb-1.5 text-xs font-semibold text-muted-foreground uppercase">
                  Capabilities
                </h3>
                <div className="flex flex-wrap gap-1.5">
                  {typeMeta.capabilities.map((cap) => (
                    <Badge key={cap} variant="outline">
                      {capabilityLabel(cap)}
                    </Badge>
                  ))}
                </div>
              </div>
            )}

            <div>
              <h3 className="mb-1 text-xs font-semibold text-muted-foreground uppercase">
                Configuration fields
              </h3>
              {typeMeta.configSchema.length === 0 ? (
                <p className="py-2 text-sm text-muted-foreground">
                  This type has no configurable fields.
                </p>
              ) : (
                <ScrollArea className="max-h-64">
                  <div className="divide-y pr-3">
                    {typeMeta.configSchema.map((field) => (
                      <IntegrationManifestFieldRow key={field.key} field={field} />
                    ))}
                  </div>
                </ScrollArea>
              )}
            </div>

            <DialogFooter showCloseButton />
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

export default IntegrationManifestDialog;
