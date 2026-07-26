import { useQuery } from "@tanstack/react-query";
import { authApi } from "@/lib/actions/auth";
import { QUERY_KEYS } from "@/constants/api";

/** Enabled SSO providers (OIDC + SAML) for the sign-in page, unified across protocols. */
export function useSsoProviders() {
  return useQuery({
    queryKey: QUERY_KEYS.SSO_PROVIDERS,
    queryFn: authApi.ssoProviders,
    staleTime: 60_000,
    retry: false,
  });
}

/** Whether SSO-only mode is active (password sign-in disabled). */
export function useSsoMode() {
  return useQuery({
    queryKey: QUERY_KEYS.SSO_MODE,
    queryFn: authApi.ssoMode,
    staleTime: 60_000,
    retry: false,
  });
}
