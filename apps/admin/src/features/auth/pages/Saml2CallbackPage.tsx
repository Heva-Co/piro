import { useEffect, useRef } from "react";
import { setStoredAuth } from "@/lib/axios";
import { ROUTES } from "@/constants/routes";
import { PiroLogoLoader } from "@/components/PiroLogoLoader";

/**
 * SAML callback handler.
 *
 * Flow:
 * 1. The IdP POSTs the SAMLResponse to the backend ACS (/api/v1/auth/saml/acs).
 * 2. The backend validates it and 302-redirects the browser here with the issued tokens
 *    in the URL fragment (#access_token=…&refresh_token=…&expires_in=…) — the fragment keeps
 *    tokens out of server/referer logs.
 * 3. This page reads the fragment, persists the tokens, and does a full-page navigation to the
 *    dashboard so AuthProvider re-hydrates the user identity from the stored JWT claims.
 */
function Saml2CallbackPage() {
  const calledRef = useRef(false);

  useEffect(() => {
    // Guard against React StrictMode double-invoke
    if (calledRef.current) return;
    calledRef.current = true;

    const hash = window.location.hash.startsWith("#")
      ? window.location.hash.slice(1)
      : window.location.hash;
    const params = new URLSearchParams(hash);

    const accessToken = params.get("access_token");
    const refreshToken = params.get("refresh_token");
    const expiresIn = Number(params.get("expires_in"));

    if (!accessToken || !refreshToken || !Number.isFinite(expiresIn) || expiresIn <= 0) {
      window.location.assign(`${ROUTES.AUTH.SIGN_IN}?oidc_error=1`);
      return;
    }

    setStoredAuth({
      accessToken,
      refreshToken,
      expiresAt: Date.now() + expiresIn * 1000,
    });

    // Full-page navigation: AuthProvider decodes the user from the stored access token on mount.
    window.location.assign(ROUTES.DASHBOARD);
  }, []);

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-4">
      <PiroLogoLoader />
      <p className="text-sm text-muted-foreground">Completing sign-in…</p>
    </div>
  );
}

export default Saml2CallbackPage;
