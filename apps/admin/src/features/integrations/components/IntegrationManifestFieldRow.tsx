import { KeyRound, Cog, Asterisk } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import type { ConfigFieldSchema } from "@/lib/actions/integrations";

interface Props {
  field: ConfigFieldSchema;
}

/** One configuration field in the manifest dialog: label, type, and secret/required/generated markers. */
function IntegrationManifestFieldRow(props: Props) {
  const { field } = props;

  return (
    <div className="flex items-start justify-between gap-3 py-2">
      <div className="min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="text-sm font-medium">{field.label}</span>
          {field.required && (
            <Asterisk className="size-3 text-destructive" aria-label="Required" />
          )}
        </div>
        {field.helpText && (
          <p className="mt-0.5 text-xs text-muted-foreground">{field.helpText}</p>
        )}
      </div>
      <div className="flex shrink-0 items-center gap-1.5">
        {field.isSecret && (
          <Badge variant="outline" className="gap-1 text-muted-foreground">
            <KeyRound />
            Secret
          </Badge>
        )}
        {field.isGenerated && (
          <Badge variant="outline" className="gap-1 text-muted-foreground">
            <Cog />
            Generated
          </Badge>
        )}
        <Badge variant="secondary" className="font-mono">
          {field.type}
        </Badge>
      </div>
    </div>
  );
}

export default IntegrationManifestFieldRow;
