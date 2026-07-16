import { Icon } from "@iconify/react";
import type { IntegrationTypeMeta } from "@/lib/actions/integrations";

interface Props {
  typeMeta: IntegrationTypeMeta;
  onSelect: (type: string) => void;
}

export function IntegrationTypeCard(props: Props) {
  const { typeMeta, onSelect } = props;

  return (
    <button
      type="button"
      onClick={() => onSelect(typeMeta.type)}
      className="flex flex-col gap-3 rounded-xl border bg-card p-4 text-left transition-colors hover:border-foreground/30 hover:bg-muted/40"
    >
      <div className="flex items-center justify-between">
        <span className="flex size-10 items-center justify-center rounded-lg border bg-background">
          {typeMeta.iconifyIcon && <Icon icon={typeMeta.iconifyIcon} className="size-5" />}
        </span>
        {typeMeta.channelOnly && (
          <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
            Per-channel
          </span>
        )}
      </div>
      <div>
        <div className="text-sm font-semibold">{typeMeta.label ?? typeMeta.type}</div>
        {typeMeta.description && (
          <p className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">{typeMeta.description}</p>
        )}
      </div>
    </button>
  );
}
