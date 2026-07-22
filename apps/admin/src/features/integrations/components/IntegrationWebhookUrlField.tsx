import { useState } from "react";
import { Check, Copy } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { API_BASE } from "@/constants/api";
import { MASKED_SECRET_VALUE } from "@/constants/integrations";
import type { IntegrationTypeMeta } from "@/lib/actions/integrations";

interface Props {
  integrationId: string;
  typeMeta: IntegrationTypeMeta;
  /** Current ConfigJson values, keyed the same way as the type's configSchema. */
  configValues: Record<string, unknown>;
}

/**
 * Pre-built webhook URL for any inbound-webhook IntegrationType (RFC 0001 §4.8) — the auth token
 * is embedded in the query string since most third-party webhook dialogs only accept one plain
 * "Endpoint URL" field, no custom headers. Renders nothing for a type with no webhookPath (i.e.
 * every non-webhook type) — not GCP-specific, any future inbound webhook type reuses this as-is.
 */
export function IntegrationWebhookUrlField(props: Props) {
  const { integrationId, typeMeta, configValues } = props;
  const [copied, setCopied] = useState(false);

  if (!typeMeta.webhookPath) return null;

  const tokenField = typeMeta.configSchema.find((f) => f.isGenerated && f.isSecret);
  const rawToken = tokenField ? configValues[tokenField.key] : undefined;
  const token = typeof rawToken === "string" ? rawToken : "";
  const isTokenKnown = token !== "" && token !== MASKED_SECRET_VALUE;

  const url = `${API_BASE}/webhooks/${typeMeta.webhookPath}/${integrationId}?auth_token=${isTokenKnown ? token : "…"}`;

  function handleCopy() {
    navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="flex flex-col gap-1.5">
      <Label>Webhook URL</Label>
      <div className="flex items-center gap-2">
        <Input readOnly value={url} className="font-mono text-xs" />
        {isTokenKnown && (
          <Button type="button" variant="outline" size="icon" onClick={handleCopy}>
            {copied ? <Check size={14} /> : <Copy size={14} />}
          </Button>
        )}
      </div>
      <p className="text-xs text-muted-foreground">
        Paste this as the endpoint URL when configuring the webhook in {typeMeta.label ?? typeMeta.type}.
      </p>
    </div>
  );
}
