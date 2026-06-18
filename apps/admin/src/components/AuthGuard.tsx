import { Navigate, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "@/hooks/useAuth";
import { ROUTES } from "@/constants/routes";

interface Props {
  children: ReactNode;
}

/**
 * Wraps protected routes — redirects to sign-in if not authenticated.
 * Preserves the intended destination in `?from=` so sign-in can redirect back.
 */
export function AuthGuard({ children }: Props) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="size-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <Navigate
        to={`${ROUTES.AUTH.SIGN_IN}?from=${encodeURIComponent(location.pathname)}`}
        replace
      />
    );
  }

  return <>{children}</>;
}
