import type { Cookies } from "@sveltejs/kit";

export const ACCESS_TOKEN_COOKIE = "piro_access_token";
export const REFRESH_TOKEN_COOKIE = "piro_refresh_token";
const COOKIE_OPTS = { path: "/", httpOnly: true, sameSite: "lax" as const, secure: false };

export function setTokenCookies(
  cookies: Cookies,
  accessToken: string,
  refreshToken: string,
  expiresIn: number
) {
  cookies.set(ACCESS_TOKEN_COOKIE, accessToken, {
    ...COOKIE_OPTS,
    maxAge: expiresIn,
  });
  cookies.set(REFRESH_TOKEN_COOKIE, refreshToken, {
    ...COOKIE_OPTS,
    maxAge: 60 * 60 * 24 * 30, // 30 days
  });
}

export function clearTokenCookies(cookies: Cookies) {
  cookies.delete(ACCESS_TOKEN_COOKIE, { path: "/" });
  cookies.delete(REFRESH_TOKEN_COOKIE, { path: "/" });
}

export function getAccessToken(cookies: Cookies): string | undefined {
  return cookies.get(ACCESS_TOKEN_COOKIE);
}

export function getRefreshToken(cookies: Cookies): string | undefined {
  return cookies.get(REFRESH_TOKEN_COOKIE);
}
