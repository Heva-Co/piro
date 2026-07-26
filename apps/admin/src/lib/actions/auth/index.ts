import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type ForgotPasswordRequest = components["schemas"]["ForgotPasswordRequest"];
export type ResetPasswordRequest = components["schemas"]["ResetPasswordRequest"];

/** An SSO provider as shown on the sign-in page, unified across protocols (OIDC/SAML). */
export interface SsoSignInProvider {
  id: string;
  displayName: string;
  /** The URL this provider's "Sign in with…" button navigates to. */
  startUrl: string;
}

export const authApi = {
  forgotPassword: (data: ForgotPasswordRequest) =>
    api.post(ENDPOINTS.AUTH.FORGOT_PASSWORD, data),

  resetPassword: (data: ResetPasswordRequest) =>
    api.post(ENDPOINTS.AUTH.RESET_PASSWORD, data),

  /**
   * Enabled SSO providers for the sign-in page, unified across protocols. Each carries the URL its
   * button points to; the UI never distinguishes OIDC from SAML. The two protocols are fetched
   * independently so a failure or absence of one never hides the other.
   */
  ssoProviders: async (): Promise<SsoSignInProvider[]> => {
    const [oidc, saml] = await Promise.all([
      api
        .get<{ id: string; displayName: string }[]>(ENDPOINTS.AUTH.OIDC_PROVIDERS)
        .then((r) => r.data.map((p) => ({ ...p, startUrl: ENDPOINTS.AUTH.OIDC_START(p.id) })))
        .catch(() => [] as SsoSignInProvider[]),
      api
        .get<{ id: string; displayName: string }[]>(ENDPOINTS.AUTH.SAML_PROVIDERS)
        .then((r) => r.data.map((p) => ({ ...p, startUrl: ENDPOINTS.AUTH.SAML_START(p.id) })))
        .catch(() => [] as SsoSignInProvider[]),
    ]);
    return [...oidc, ...saml];
  },

  ssoMode: () =>
    api.get<{ ssoOnly: boolean }>(ENDPOINTS.AUTH.OIDC_SSO_MODE).then((r) => r.data),
};
