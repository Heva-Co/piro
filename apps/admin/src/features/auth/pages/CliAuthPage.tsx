import { useState } from "react";
import { Navigate, useSearchParams } from "react-router-dom";
import { Terminal, ShieldCheck, AlertTriangle } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { ROUTES } from "@/constants/routes";
import api from "@/lib/axios";
import { Button } from "@/components/ui/button";
import { Alert, AlertTitle, AlertDescription } from "@/components/ui/alert";
import AuthCardShell from "@/features/auth/components/AuthCardShell";

/**
 * Consent screen for `piro login` (RFC 0019 §4.6, §4.11).
 *
 * The CLI opens this with its loopback callback, a random state and a PKCE challenge, then waits.
 * Nothing is granted until the user clicks Authorize: a page that minted a token on render would
 * turn any opened link into a credential leak.
 */
function CliAuthPage() {
  const [params] = useSearchParams();
  const { isAuthenticated, isLoading, user } = useAuth();
  const [status, setStatus] = useState<"idle" | "working" | "done" | "failed">("idle");
  const [error, setError] = useState<string | null>(null);

  const callback = params.get("callback") ?? "";
  const state = params.get("state") ?? "";
  const challenge = params.get("challenge") ?? "";
  const label = params.get("label");

  if (isLoading) return null;

  // An unauthenticated visitor signs in normally and comes back here with the parameters intact,
  // which is what lets an OIDC or SAML-only instance complete a CLI login at all.
  if (!isAuthenticated) {
    const from = `${window.location.pathname}${window.location.search}`;
    return <Navigate to={`${ROUTES.AUTH.SIGN_IN}?from=${encodeURIComponent(from)}`} replace />;
  }

  const invalid = !callback || !state || !challenge
    ? "This link is missing the parameters the CLI should have supplied."
    : !isLoopback(callback)
      ? "This link asks to send your session to a non-local address, so it was refused."
      : null;

  async function authorize() {
    setStatus("working");
    setError(null);

    try {
      const res = await api.post<{ code: string; state: string }>("/api/v1/auth/cli/authorize", {
        redirectUri: callback,
        codeChallenge: challenge,
        state,
        clientLabel: label,
      });

      setStatus("done");
      redirect(callback, { code: res.data.code, state: res.data.state });
    } catch {
      setStatus("failed");
      setError("Could not authorize the CLI. Try running `piro login` again.");
    }
  }

  function cancel() {
    // Redirecting with an error lets the CLI exit promptly instead of hanging until its timeout.
    setStatus("done");
    redirect(callback, { error: "access_denied", state });
  }

  if (invalid) {
    return (
      <AuthCardShell title="Authorize the Piro CLI">
        <Alert variant="destructive">
          <AlertTriangle className="size-4" />
          <AlertTitle>This request was refused</AlertTitle>
          <AlertDescription>{invalid}</AlertDescription>
        </Alert>
      </AuthCardShell>
    );
  }

  if (status === "done") {
    return (
      <AuthCardShell title="Authorized">
        <Alert>
          <ShieldCheck className="size-4" />
          <AlertTitle>You can close this tab</AlertTitle>
          <AlertDescription>The CLI has been sent back to your terminal.</AlertDescription>
        </Alert>
      </AuthCardShell>
    );
  }

  return (
    <AuthCardShell title="Authorize the Piro CLI">
      <div className="flex flex-col gap-5">
        <div className="flex items-start gap-3 rounded-lg border p-4">
          <Terminal className="mt-0.5 size-5 shrink-0 text-muted-foreground" />
          <div className="min-w-0">
            <p className="text-sm font-medium">{label || "Piro CLI"}</p>
            <p className="text-xs text-muted-foreground break-all">is asking to sign in as {user?.email}</p>
          </div>
        </div>

        <p className="text-sm text-muted-foreground">
          The CLI will get its own session with your permissions. It appears in your sessions list and
          you can revoke it at any time, without affecting this browser.
        </p>

        {error && (
          <Alert variant="destructive">
            <AlertTriangle className="size-4" />
            <AlertTitle>{error}</AlertTitle>
          </Alert>
        )}

        <div className="flex gap-3">
          <Button onClick={authorize} disabled={status === "working"} className="flex-1">
            {status === "working" ? "Authorizing…" : "Authorize"}
          </Button>
          <Button variant="outline" onClick={cancel} disabled={status === "working"} className="flex-1">
            Cancel
          </Button>
        </div>
      </div>
    </AuthCardShell>
  );
}

/**
 * Only a loopback callback is ever acceptable. The server enforces this too — a client-side check is
 * a courtesy, not a control — but refusing here means a crafted link never even renders a button.
 */
function isLoopback(callback: string): boolean {
  try {
    const url = new URL(callback);
    return (
      (url.protocol === "http:" || url.protocol === "https:") &&
      (url.hostname === "127.0.0.1" || url.hostname === "localhost" || url.hostname === "[::1]")
    );
  } catch {
    return false;
  }
}

function redirect(callback: string, params: Record<string, string>) {
  const url = new URL(callback);
  for (const [key, value] of Object.entries(params)) url.searchParams.set(key, value);
  window.location.replace(url.toString());
}

export default CliAuthPage;
