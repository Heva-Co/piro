import { useEffect, useRef } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth";
import { authApi } from "@/lib/api";
import { setStoredAuth } from "@/lib/axios";
import { ROUTES } from "@/constants/routes";
import { PiroLogoLoader } from "@/components/PiroLogoLoader";

/**
 * OIDC callback handler.
 *
 * Flow:
 * 1. Backend redirects browser to /admin/auth/oidc/callback?code=...&state=...
 * 2. This page shows a loading spinner, reads code+state from URL
 * 3. POSTs to /api/v1/auth/oidc/callback — backend returns JSON tokens
 * 4. Saves tokens to localStorage, redirects to /admin
 */
export default function OidcCallbackPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { loginWithTokens } = useAuth();
  const calledRef = useRef(false);

  useEffect(() => {
    // Guard against React StrictMode double-invoke
    if (calledRef.current) return;
    calledRef.current = true;

    const code = searchParams.get("code");
    const state = searchParams.get("state");
    const error = searchParams.get("error");

    if (error || !code || !state) {
      navigate(`${ROUTES.AUTH.SIGN_IN}?oidc_error=1`, { replace: true });
      return;
    }

    authApi
      .oidcCallback(code, state)
      .then(({ accessToken, refreshToken, expiresIn, user }) => {
        setStoredAuth({
          accessToken,
          refreshToken,
          expiresAt: Date.now() + expiresIn * 1000,
        });
        loginWithTokens(
          { accessToken, refreshToken, expiresAt: Date.now() + expiresIn * 1000 },
          user
        );
        navigate(ROUTES.DASHBOARD, { replace: true });
      })
      .catch(() => {
        navigate(`${ROUTES.AUTH.SIGN_IN}?oidc_error=1`, { replace: true });
      });
  }, []);  // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-4">
      <PiroLogoLoader />
      <p className="text-sm text-muted-foreground">Completing sign-in…</p>
    </div>
  );
}
