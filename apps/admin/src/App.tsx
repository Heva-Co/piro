import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "@/providers/AuthProvider";
import { AuthGuard } from "@/components/AuthGuard";
import { ROUTES } from "@/constants/routes";

// Auth pages
import SignInPage from "@/features/auth/pages/SignInPage";
import OidcCallbackPage from "@/features/auth/pages/OidcCallbackPage";
import SetupPage from "@/features/auth/pages/SetupPage";
import AcceptInvitePage from "@/features/auth/pages/AcceptInvitePage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            {/* Redirect root to admin */}
            <Route path="/" element={<Navigate to={ROUTES.DASHBOARD} replace />} />

            {/* Public auth routes */}
            <Route path={ROUTES.AUTH.SIGN_IN} element={<SignInPage />} />
            <Route path={ROUTES.AUTH.OIDC_CALLBACK} element={<OidcCallbackPage />} />
            <Route path={ROUTES.SETUP} element={<SetupPage />} />
            <Route path="/admin/invite/:token" element={<AcceptInvitePage />} />

            {/* Protected admin routes — Phase 4 */}
            <Route
              path={ROUTES.DASHBOARD}
              element={
                <AuthGuard>
                  <div className="p-8 text-foreground">Admin dashboard coming in Phase 4</div>
                </AuthGuard>
              }
            />

            {/* Catch-all */}
            <Route path="*" element={<Navigate to={ROUTES.DASHBOARD} replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
