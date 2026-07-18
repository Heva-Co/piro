import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { integrationOAuthApi } from "@/lib/actions/integrations";
import { ROUTES } from "@/constants/routes";
import { PiroLogoLoader } from "@/components/PiroLogoLoader";

/**
 * Integration OAuth callback handler (RFC 0004).
 *
 * Flow:
 * 1. The provider (PagerDuty) redirects the browser here with ?code=...&state=...
 * 2. This page relays code+state to POST /api/v1/integrations/oauth/callback
 * 3. The backend exchanges the code and stores the encrypted token, keyed by the integration the
 *    connect flow was started for (carried in the state), then returns which integration connected
 * 4. We navigate back to that integration's detail page
 */
export default function IntegrationOAuthCallbackPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const calledRef = useRef(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (calledRef.current) return;
    calledRef.current = true;

    const code = searchParams.get("code");
    const state = searchParams.get("state");
    const oauthError = searchParams.get("error");

    if (oauthError || !code || !state) {
      setError(oauthError ?? "Missing code or state in the callback.");
      return;
    }

    integrationOAuthApi
      .callback(code, state)
      .then((result) => {
        navigate(ROUTES.INTEGRATIONS.DETAIL(result.integrationId), { replace: true });
      })
      .catch((err: unknown) => {
        const message =
          err && typeof err === "object" && "message" in err ? String(err.message) : "Connection failed.";
        setError(message);
      });
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (error) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center gap-4">
        <p className="text-sm text-destructive">Failed to connect: {error}</p>
        <button
          className="text-sm underline"
          onClick={() => navigate(ROUTES.INTEGRATIONS.LIST, { replace: true })}
        >
          Back to integrations
        </button>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-4">
      <PiroLogoLoader />
      <p className="text-sm text-muted-foreground">Completing connection…</p>
    </div>
  );
}
