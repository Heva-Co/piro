import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from "react";
import {
  getStoredAuth,
  setStoredAuth,
  clearStoredAuth,
  type StoredAuth,
} from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import axios from "axios";

export interface UserDto {
  id: number;
  email: string;
  name: string;
  roles: string[];
}

interface SignInResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: UserDto;
}

interface AuthContextValue {
  user: UserDto | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  loginWithTokens: (auth: StoredAuth, user: UserDto) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Hydrate auth state from localStorage on mount
  useEffect(() => {
    const auth = getStoredAuth();
    if (auth) {
      // Decode user from JWT payload (claims are in the token)
      try {
        const payload = JSON.parse(atob(auth.accessToken.split(".")[1]));
        const roles = payload[
          "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        ];
        setUser({
          id: parseInt(payload.sub),
          email: payload.email,
          name: payload.name ?? payload.email,
          roles: Array.isArray(roles) ? roles : roles ? [roles] : [],
        });
      } catch {
        clearStoredAuth();
      }
    }
    setIsLoading(false);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const res = await axios.post<SignInResponse>(ENDPOINTS.AUTH.SIGN_IN, {
      email,
      password,
    });
    const { accessToken, refreshToken, expiresIn, user } = res.data;
    setStoredAuth({
      accessToken,
      refreshToken,
      expiresAt: Date.now() + expiresIn * 1000,
    });
    setUser(user);
  }, []);

  const loginWithTokens = useCallback((auth: StoredAuth, user: UserDto) => {
    setStoredAuth(auth);
    setUser(user);
  }, []);

  const logout = useCallback(() => {
    clearStoredAuth();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: !!user,
        login,
        loginWithTokens,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
