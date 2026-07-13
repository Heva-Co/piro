import { Navigate, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useAuth } from "@/hooks/useAuth";
import { setupApi } from "@/lib/api";
import { ROUTES } from "@/constants/routes";
import { PiroLogoLoader } from "@/components/PiroLogoLoader";

interface Props {
  children: ReactNode;
}

/**
 * Wraps protected routes.
 * 1. If setup is not complete → redirect to /admin/setup
 * 2. If not authenticated → redirect to sign-in
 */
export function AuthGuard({ children }: Props) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  const setupQuery = useQuery({
    queryKey: ["setup-status"],
    queryFn: () => setupApi.status(),
    staleTime: 60_000,
    retry: false,
  });

  if (isLoading || setupQuery.isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <PiroLogoLoader />
      </div>
    );
  }

  if (setupQuery.data && !setupQuery.data.isComplete) {
    return <Navigate to={ROUTES.SETUP} replace />;
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
