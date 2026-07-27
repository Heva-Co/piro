import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, CheckCircle2, ArrowRight } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { integrationsApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";

interface Props {
  /** Undefined while the integration is still being created — redemption needs a saved integration. */
  integrationId?: string;
  /** The relay push URL already stored on this integration, if any. */
  currentPushUrl?: string;
  /** The relay app id stored after a successful redemption — its presence means "connected". */
  currentAppId?: string;
  /** The relay key id stored after a successful redemption, for support. */
  currentKeyId?: string;
}

const DEFAULT_PUSH_URL = "https://api.dev.heva.pro/socket.io/v1/push";

/**
 * Connects this Piro instance to the Heva push relay.
 *
 * The relay exists because the App Store / Play Store builds of Piro are signed against Heva's Firebase
 * project and Apple bundle id, so a self-hosted backend has no credentials that can reach them. Heva
 * issues a single-use invite code; Piro exchanges it for a scoped API key and stores it encrypted.
 *
 * The exchange is deliberately one-shot: the relay spends the code on success, so this never retries.
 */
function MobilePushRelayConnect(props: Props) {
  const { integrationId, currentPushUrl, currentAppId, currentKeyId } = props;
  const queryClient = useQueryClient();

  const [pushUrl, setPushUrl] = useState(currentPushUrl || DEFAULT_PUSH_URL);
  const [inviteCode, setInviteCode] = useState("");

  const isConnected = Boolean(currentAppId);

  const redeem = useMutation({
    mutationFn: () =>
      integrationsApi.redeemRelayInvite(integrationId!, {
        pushUrl: pushUrl.trim(),
        inviteCode: inviteCode.trim(),
      }),
    onSuccess: (integration) => {
      setInviteCode("");
      toast.success("Connected to the Heva push relay.");
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATIONS });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATION(integration.id) });
    },
    onError: (error: Error & { response?: { data?: { error?: string } } }) => {
      // The backend distinguishes "invite already used" from "relay unreachable", because those need
      // opposite responses from the operator. Surface its message rather than a generic failure.
      toast.error(error.response?.data?.error || error.message || "Could not redeem the invite code.");
    },
  });

  const canSubmit =
    Boolean(integrationId) &&
    pushUrl.trim().length > 0 &&
    inviteCode.trim().length > 0 &&
    !redeem.isPending;

  return (
    <div className="space-y-4">
      {isConnected ? (
        <div className="flex items-start gap-2 rounded-md border border-emerald-500/30 bg-emerald-500/5 p-3">
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
          <div className="space-y-1 text-sm">
            <p className="font-medium text-emerald-700 dark:text-emerald-400">
              Connected to the Heva push relay
            </p>
            <p className="text-muted-foreground">
              App <code className="font-mono">{currentAppId}</code>
              {currentKeyId ? (
                <>
                  {" · key "}
                  <code className="font-mono">{currentKeyId}</code>
                </>
              ) : null}
            </p>
            <p className="text-muted-foreground">
              Redeeming another invite below replaces the stored key.
            </p>
          </div>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          Paste the invite code Heva gave you. Piro exchanges it for an API key and stores it encrypted,
          so you never handle the key itself.
        </p>
      )}

      <div className="space-y-2">
        <Label htmlFor="relay-push-url">Relay push URL</Label>
        <Input
          id="relay-push-url"
          value={pushUrl}
          onChange={(e) => setPushUrl(e.target.value)}
          placeholder={DEFAULT_PUSH_URL}
          disabled={redeem.isPending}
        />
        <p className="text-xs text-muted-foreground">
          The full push endpoint, including any path prefix. Piro derives the register endpoint from it.
        </p>
      </div>

      <div className="space-y-2">
        <Label htmlFor="relay-invite-code">Invite code</Label>
        <Input
          id="relay-invite-code"
          value={inviteCode}
          onChange={(e) => setInviteCode(e.target.value)}
          placeholder="inv_… (or an hvr_ key you already hold)"
          autoComplete="off"
          spellCheck={false}
          disabled={redeem.isPending}
        />
        <p className="text-xs text-muted-foreground">
          An invite can only be redeemed once. If it fails as already used, ask Heva for a new one.
        </p>
      </div>

      {!integrationId && (
        <p className="text-xs text-muted-foreground">
          Save this integration first, then redeem the invite.
        </p>
      )}

      <Button
        type="button"
        onClick={() => redeem.mutate()}
        disabled={!canSubmit}
        className="w-full sm:w-auto"
      >
        {redeem.isPending ? (
          <>
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Redeeming…
          </>
        ) : (
          <>
            {isConnected ? "Redeem a new invite" : "Connect to relay"}
            <ArrowRight className="ml-2 h-4 w-4" />
          </>
        )}
      </Button>
    </div>
  );
}

export default MobilePushRelayConnect;
