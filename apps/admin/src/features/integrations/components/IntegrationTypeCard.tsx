import { Icon } from "@iconify/react";
import { Info } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { IntegrationTypeMeta } from "@/lib/actions/integrations";

interface Props {
  typeMeta: IntegrationTypeMeta;
  onSelect: (type: string) => void;
  onViewManifest: (typeMeta: IntegrationTypeMeta) => void;
}

/**
 * Provider card in the "New Integration" picker. Clicking (or Enter/Space) the card selects the
 * type; the corner info button opens the manifest dialog without selecting — so an admin can
 * inspect what a provider needs before committing to it. The card is a div (not a button) so the
 * info button can nest without invalid button-in-button markup.
 */
export function IntegrationTypeCard(props: Props) {
  const { typeMeta, onSelect, onViewManifest } = props;

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={() => onSelect(typeMeta.type)}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onSelect(typeMeta.type);
        }
      }}
      className="relative flex cursor-pointer flex-col gap-3 rounded-xl border bg-card p-4 text-left transition-colors hover:border-foreground/30 hover:bg-muted/40 focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      <div className="flex items-center justify-between">
        <span className="flex size-10 items-center justify-center rounded-lg border bg-background">
          {typeMeta.iconifyIcon && <Icon icon={typeMeta.iconifyIcon} className="size-5" />}
        </span>
        <div className="flex items-center gap-2">
          {typeMeta.channelOnly && (
            <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
              Per-channel
            </span>
          )}
          <Tooltip>
            <TooltipTrigger
              render={
                <Button
                  variant="ghost"
                  size="icon-xs"
                  aria-label="View manifest"
                  onClick={(e) => {
                    e.stopPropagation();
                    onViewManifest(typeMeta);
                  }}
                >
                  <Info />
                </Button>
              }
            />
            <TooltipContent>View manifest</TooltipContent>
          </Tooltip>
        </div>
      </div>
      <div>
        <div className="text-sm font-semibold">{typeMeta.label ?? typeMeta.type}</div>
        {typeMeta.description && (
          <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">{typeMeta.description}</p>
        )}
      </div>
    </div>
  );
}
