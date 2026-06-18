import axios from "axios";
import { ENDPOINTS } from "@/constants/api";

const AUTH_STORAGE_KEY = "piro_auth";

export interface StoredAuth {
  accessToken: string;
  refreshToken: string;
  expiresAt: number; // Unix timestamp ms
}

export function getStoredAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function setStoredAuth(auth: StoredAuth): void {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(auth));
}

export function clearStoredAuth(): void {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}

const api = axios.create({
  headers: { "Content-Type": "application/json" },
});

// Attach access token to every request
api.interceptors.request.use((config) => {
  const auth = getStoredAuth();
  if (auth?.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`;
  }
  return config;
});

// Silent token refresh on 401
let refreshPromise: Promise<string | null> | null = null;

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config;

    if (error.response?.status !== 401 || original._retry) {
      return Promise.reject(error);
    }

    original._retry = true;

    // Deduplicate concurrent 401s into a single refresh call
    if (!refreshPromise) {
      refreshPromise = (async () => {
        const auth = getStoredAuth();
        if (!auth?.refreshToken) return null;

        try {
          const res = await axios.post<{
            accessToken: string;
            refreshToken: string;
            expiresIn: number;
          }>(ENDPOINTS.AUTH.REFRESH, { refreshToken: auth.refreshToken });

          const { accessToken, refreshToken, expiresIn } = res.data;
          setStoredAuth({
            accessToken,
            refreshToken,
            expiresAt: Date.now() + expiresIn * 1000,
          });
          return accessToken;
        } catch {
          clearStoredAuth();
          window.location.href = "/admin/auth/sign-in";
          return null;
        } finally {
          refreshPromise = null;
        }
      })();
    }

    const newToken = await refreshPromise;
    if (!newToken) return Promise.reject(error);

    original.headers.Authorization = `Bearer ${newToken}`;
    return api(original);
  }
);

export default api;
