import { fail, redirect } from "@sveltejs/kit";
import { authApi, ApiError } from "$lib/api";
import { setTokenCookies } from "$lib/auth";
import type { Actions, PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals, url }) => {
  if (locals.user) redirect(302, "/admin");

  // Load OIDC providers and SSO mode — silently ignore errors
  let oidcProviders: { id: string; displayName: string }[] = [];
  let ssoOnly = false;
  try {
    [oidcProviders] = await Promise.all([
      authApi.getOidcProviders().catch(() => []),
      authApi.getPublicSsoMode().then((r) => { ssoOnly = r.ssoOnly; }).catch(() => {}),
    ]);
  } catch {
    // OIDC not configured — show normal sign-in only
  }

  return {
    invited: url.searchParams.has("invited"),
    oidcError: url.searchParams.has("oidc_error"),
    oidcProviders,
    ssoOnly,
  };
};

export const actions: Actions = {
  default: async ({ request, cookies }) => {
    const form = await request.formData();
    const email = form.get("email") as string;
    const password = form.get("password") as string;

    if (!email || !password) {
      return fail(400, { error: "Email and password are required.", email });
    }

    try {
      const result = await authApi.signIn(email, password);
      setTokenCookies(cookies, result.accessToken, result.refreshToken, result.expiresIn);
    } catch (e) {
      const msg = e instanceof ApiError ? JSON.parse(e.message || "{}").title ?? e.message : "Sign in failed.";
      return fail(400, { error: msg, email });
    }

    redirect(302, "/admin");
  },
};
