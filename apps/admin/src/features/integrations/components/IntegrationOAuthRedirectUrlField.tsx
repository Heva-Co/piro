import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Check, Copy } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { integrationOAuthApi } from "@/lib/actions/integrations";
import type { IntegrationTypeMeta } from "@/lib/actions/integrations";

interface Props {
  typeMeta: IntegrationTypeMeta;
}

/**
 * Shows the exact Redirect URL the admin must register in the provider's OAuth app (RFC 0004), with a
 * copy button — so they don't have to guess it (a wrong redirect_uri is the most common connect
 * failure). Rendered only for types that declare RequiresOAuthConnection. The URL is resolved by the
 * backend (from the configured site URL) — the same value the OAuth flow actually sends — never built
 * client-side, so the two can't drift.
 */
export function IntegrationOAuthRedirectUrlField(props: Props) {
  const { typeMeta } = props;
  const [copied, setCopied] = useState(false);

  const { data } = useQuery({
    queryKey: ["integration-oauth-redirect-uri"],
    queryFn: integrationOAuthApi.redirectUri,
    staleTime: Infinity,
  });

  const url = data?.redirectUri ?? "";

  function handleCopy() {
    if (!url) return;
    navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="flex flex-col gap-1.5">
      <Label>Redirect URL</Label>
      <div className="flex items-center gap-2">
        <Input readOnly value={url} placeholder="Loading…" className="font-mono text-xs" />
        <Button type="button" variant="outline" size="icon" onClick={handleCopy} disabled={!url}>
          {copied ? <Check size={14} /> : <Copy size={14} />}
        </Button>
      </div>
      <p className="text-xs text-muted-foreground">
        Register this as the Redirect URL in your {typeMeta.label ?? typeMeta.type} OAuth app — it must match exactly.
      </p>
    </div>
  );
}
