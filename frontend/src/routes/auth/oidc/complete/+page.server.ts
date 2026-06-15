import { redirect } from "@sveltejs/kit";
import { setTokenCookies } from "$lib/auth";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ url, cookies }) => {
  const token = url.searchParams.get("token");
  const refresh = url.searchParams.get("refresh");
  const expiresStr = url.searchParams.get("expires");

  if (!token || !refresh || !expiresStr) {
    redirect(302, "/auth/sign-in?oidc_error=1");
  }

  const expires = parseInt(expiresStr, 10);
  if (isNaN(expires)) {
    redirect(302, "/auth/sign-in?oidc_error=1");
  }

  setTokenCookies(cookies, token, refresh, expires);
  redirect(302, "/admin");
};
