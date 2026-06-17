import { redirect } from "@sveltejs/kit";
import { PIRO_API } from "$lib/api";
import type { RequestHandler } from "./$types";

/**
 * OIDC callback proxy. Google (or any provider) redirects here after authentication.
 * This route forwards code + state to the internal API without exposing the API port publicly.
 * Works for all configured providers — the backend resolves the provider from the state param.
 */
export const GET: RequestHandler = async ({ url }) => {
  const code  = url.searchParams.get("code");
  const state = url.searchParams.get("state");
  const error = url.searchParams.get("error");

  const errorRedirect = "/auth/sign-in?oidc_error=1";

  if (error || !code || !state) {
    redirect(302, errorRedirect);
  }

  const apiUrl = `${PIRO_API}/api/v1/auth/oidc/callback?code=${encodeURIComponent(code)}&state=${encodeURIComponent(state)}`;

  const res = await fetch(apiUrl, { redirect: "manual" });

  // The API responds with a 302 to /auth/oidc/complete?token=... or to the error page.
  const location = res.headers.get("location");
  if (!location) {
    redirect(302, errorRedirect);
  }

  // Location may be an absolute URL (http://api:8080/...) — strip origin and keep path+query.
  let target: string;
  try {
    const parsed = new URL(location);
    target = parsed.pathname + parsed.search;
  } catch {
    // Already a relative path
    target = location;
  }

  redirect(302, target);
};
