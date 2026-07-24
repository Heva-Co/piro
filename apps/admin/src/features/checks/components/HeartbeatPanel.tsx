import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Copy, RefreshCw, Loader2, Check } from "lucide-react";
import { Button } from "@/components/ui/button";
import { checksApi } from "@/lib/actions/checks";
import { useFormattedDate } from "@/hooks/useFormattedDate";

interface Props {
    serviceSlug: string;
    checkSlug: string;
}

/**
 * The Heartbeat check's ping panel (RFC 0013): shows the inbound ping URL the target calls, the masked
 * token, and when a ping was last received. "Rotate token" issues a fresh token and reveals its full URL
 * once (mirrors the pk_ API-key reveal UX) — after that only the masked token is shown.
 */
function HeartbeatPanel(props: Props) {
    const { serviceSlug, checkSlug } = props;
    const { formatDateTime } = useFormattedDate();
    const [rotating, setRotating] = useState(false);
    const [revealed, setRevealed] = useState<string | null>(null); // the once-shown tokenized URL
    const [copied, setCopied] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const { data, isLoading, refetch } = useQuery({
        queryKey: ["heartbeat", serviceSlug, checkSlug],
        queryFn: () => checksApi.inboundToken(serviceSlug, checkSlug),
    });

    async function rotate() {
        setRotating(true);
        setError(null);
        try {
            const result = await checksApi.rotateInboundToken(serviceSlug, checkSlug);
            setRevealed(result.inboundUrl);
            await refetch();
        } catch (e) {
            setError(e instanceof Error ? e.message : "Failed to rotate token.");
        } finally {
            setRotating(false);
        }
    }

    function copy(text: string) {
        navigator.clipboard.writeText(text);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    }

    if (isLoading) return <div className="text-sm text-muted-foreground py-2">Loading…</div>;

    return (
        <div className="rounded-lg border bg-card p-4 flex flex-col gap-4">
            <div>
                <h3 className="text-sm font-semibold">Heartbeat</h3>
                <p className="text-xs text-muted-foreground mt-0.5">
                    Call this URL from your cron / CI on the same schedule as the check. A missed ping marks it DOWN.
                </p>
            </div>

            {/* Ping URL (base — the tokenized form is revealed only on rotate) */}
            {data?.inboundUrl && (
                <div className="flex items-center gap-2">
                    <code className="flex-1 truncate rounded bg-muted px-2.5 py-1.5 text-xs font-mono">{data.inboundUrl}</code>
                    <Button size="icon-sm" variant="outline" onClick={() => copy(data.inboundUrl!)} aria-label="Copy ping URL">
                        {copied ? <Check size={13} /> : <Copy size={13} />}
                    </Button>
                </div>
            )}

            {/* The freshly rotated URL with token, shown once */}
            {revealed && (
                <div className="rounded-md border border-amber-300 bg-amber-50 dark:border-amber-800 dark:bg-amber-950/40 p-3 flex flex-col gap-2">
                    <p className="text-xs font-medium text-amber-800 dark:text-amber-300">
                        Copy this URL now — the token won't be shown again.
                    </p>
                    <div className="flex items-center gap-2">
                        <code className="flex-1 truncate rounded bg-background px-2.5 py-1.5 text-xs font-mono">{revealed}</code>
                        <Button size="icon-sm" variant="outline" onClick={() => copy(revealed)} aria-label="Copy tokenized URL">
                            {copied ? <Check size={13} /> : <Copy size={13} />}
                        </Button>
                    </div>
                </div>
            )}

            <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span>
                    {data?.maskedToken ? `Token ${data.maskedToken}` : "No token yet"}
                    {data?.lastUsedAt ? ` · last ping ${formatDateTime(data.lastUsedAt)}` : " · no pings received"}
                </span>
                <Button size="sm" variant="outline" onClick={rotate} disabled={rotating}>
                    {rotating ? <Loader2 className="size-3.5 animate-spin" /> : <RefreshCw className="size-3.5" />}
                    Rotate token
                </Button>
            </div>

            {error && <p className="text-xs text-destructive">{error}</p>}
        </div>
    );
}

export default HeartbeatPanel;
