import { redirect } from "@sveltejs/kit";
import { authApi } from "$lib/api";
import { clearTokenCookies, getAccessToken } from "$lib/auth";
import type { RequestHandler } from "./$types";

export const POST: RequestHandler = async ({ cookies }) => {
  const token = getAccessToken(cookies);
  if (token) {
    await authApi.signOut(token).catch(() => {}); // best-effort
  }
  clearTokenCookies(cookies);
  redirect(302, "/");
};
