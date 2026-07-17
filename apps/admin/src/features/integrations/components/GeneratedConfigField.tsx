import { useState } from "react";
import { Check, Copy, RefreshCw } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { MASKED_SECRET_VALUE } from "@/constants/integrations";
import type { ConfigFieldSchema } from "@/lib/actions/integrations";

interface Props {
  field: ConfigFieldSchema;
  value: string;
  /** True while creating a new Integration — nothing to show yet, the server generates the value on submit. */
  isCreating: boolean;
  /** Regenerates this field's value server-side, invalidating the old one — undefined while creating. */
  onRegenerate?: () => void;
  isRegenerating?: boolean;
}

/** Read-only display for a server-generated config field (e.g. a webhook auth token) — never user-editable. */
export function GeneratedConfigField(props: Props) {
  const { field, value, isCreating, onRegenerate, isRegenerating } = props;
  const [copied, setCopied] = useState(false);
  const [confirmingRegenerate, setConfirmingRegenerate] = useState(false);
  const isRevealed = value !== "" && value !== MASKED_SECRET_VALUE;

  function handleCopy() {
    navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  function handleRegenerate() {
    onRegenerate?.();
    setConfirmingRegenerate(false);
  }

  if (isCreating) {
    return (
      <div className="flex flex-col gap-1.5">
        <Label>{field.label}</Label>
        <p className="text-xs text-muted-foreground">
          Generated automatically once you create this integration.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-1.5">
      <Label>{field.label}</Label>
      <div className="flex items-center gap-2">
        <Input readOnly value={isRevealed ? value : "••••••••••••"} className="font-mono text-xs" />
        {isRevealed && (
          <Button type="button" variant="outline" size="icon" onClick={handleCopy}>
            {copied ? <Check size={14} /> : <Copy size={14} />}
          </Button>
        )}
        {onRegenerate && (
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setConfirmingRegenerate(true)}
            disabled={isRegenerating}
            title="Regenerate"
          >
            <RefreshCw size={14} className={isRegenerating ? "animate-spin" : ""} />
          </Button>
        )}
      </div>
      {confirmingRegenerate && (
        <div className="flex items-center gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-xs text-amber-600 dark:text-amber-400">
          <span className="flex-1">
            Regenerating replaces this value immediately — anything using the old one (e.g. an already-configured webhook) will stop working until updated.
          </span>
          <Button type="button" variant="outline" size="sm" onClick={() => setConfirmingRegenerate(false)}>
            Cancel
          </Button>
          <Button type="button" size="sm" onClick={handleRegenerate}>
            Regenerate
          </Button>
        </div>
      )}
      {isRevealed ? (
        <p className="text-xs text-amber-600 dark:text-amber-400">
          Copy this now — it won't be shown again.
        </p>
      ) : (
        field.helpText && <p className="text-xs text-muted-foreground">{field.helpText}</p>
      )}
    </div>
  );
}
