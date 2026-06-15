import type { Handle } from "@sveltejs/kit";
import { authApi, ApiError } from "$lib/api";
import { getAccessToken, getRefreshToken, setTokenCookies, clearTokenCookies } from "$lib/auth";

/**
 * Global server hook: attaches user info to event.locals.
 * Attempts token refresh if the access token is missing but a refresh token exists.
 */
export const handle: Handle = async ({ event, resolve }) => {
  const accessToken = getAccessToken(event.cookies);
  const refreshToken = getRefreshToken(event.cookies);

  if (accessToken) {
    // Decode JWT payload (no verification needed — server trusts the API)
    try {
      const payload = JSON.parse(atob(accessToken.split(".")[1]));
      event.locals.user = {
        id: parseInt(payload.sub),
        email: payload.email,
        name: payload.name,
        roles: Array.isArray(payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"])
          ? payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
          : [payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]].filter(Boolean),
      };
      event.locals.accessToken = accessToken;
    } catch {
      // Malformed token — clear it
      clearTokenCookies(event.cookies);
    }
  } else if (refreshToken) {
    // Try to silently refresh
    try {
      const result = await authApi.refresh(refreshToken);
      setTokenCookies(event.cookies, result.accessToken, result.refreshToken, result.expiresIn);
      event.locals.user = result.user;
      event.locals.accessToken = result.accessToken;
    } catch (e) {
      if (e instanceof ApiError && e.status === 400) {
        clearTokenCookies(event.cookies);
      }
    }
  }

  return resolve(event);
};
