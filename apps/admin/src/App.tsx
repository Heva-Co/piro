import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "@/providers/AuthProvider";
import { AuthGuard } from "@/components/AuthGuard";
import { ErrorBoundary, ErrorFallback } from "@/components/ErrorBoundary";
import { ROUTES } from "@/constants/routes";

// Auth pages
import SignInPage from "@/features/auth/pages/SignInPage";
import OidcCallbackPage from "@/features/auth/pages/OidcCallbackPage";
import SetupPage from "@/features/auth/pages/SetupPage";
import AcceptInvitePage from "@/features/auth/pages/AcceptInvitePage";

// Admin pages
import DashboardPage from "@/features/dashboard/pages/DashboardPage";
import ServicesPage from "@/features/services/pages/ServicesPage";
import ServiceFormPage from "@/features/services/pages/ServiceFormPage";
import ServiceDetailPage from "@/features/services/pages/ServiceDetailPage";
import ChecksPage from "@/features/checks/pages/ChecksPage";
import CheckFormPage from "@/features/checks/pages/CheckFormPage";
import CheckDetailPage from "@/features/checks/pages/CheckDetailPage";
import CheckLogsPage from "@/features/checks/pages/CheckLogsPage";
import IncidentsPage from "@/features/incidents/pages/IncidentsPage";
import IncidentFormPage from "@/features/incidents/pages/IncidentFormPage";
import IncidentDetailPage from "@/features/incidents/pages/IncidentDetailPage";
import ChannelsPage from "@/features/channels/pages/ChannelsPage";
import ChannelFormPage from "@/features/channels/pages/ChannelFormPage";
import MaintenancesPage from "@/features/maintenances/pages/MaintenancesPage";
import MaintenanceFormPage from "@/features/maintenances/pages/MaintenanceFormPage";
import MaintenanceDetailPage from "@/features/maintenances/pages/MaintenanceDetailPage";
import LogsPage from "@/features/logs/pages/LogsPage";
import SiteConfigPage from "@/features/configuration/pages/SiteConfigPage";
import EmailConfigPage from "@/features/configuration/pages/EmailConfigPage";
import UsersPage from "@/features/configuration/pages/UsersPage";
import SsoPage from "@/features/configuration/pages/SsoPage";
import ApiKeysPage from "@/features/configuration/pages/ApiKeysPage";
import WorkersPage from "@/features/configuration/pages/WorkersPage";
import ImportPage from "@/features/configuration/pages/ImportPage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard>
      <ErrorBoundary fallback={<ErrorFallback />}>
        {children}
      </ErrorBoundary>
    </AuthGuard>
  );
}

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

            {/* Protected admin routes */}
            <Route path={ROUTES.DASHBOARD} element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />

            {/* Services */}
            <Route path={ROUTES.SERVICES.LIST} element={<ProtectedRoute><ServicesPage /></ProtectedRoute>} />
            <Route path={ROUTES.SERVICES.NEW} element={<ProtectedRoute><ServiceFormPage /></ProtectedRoute>} />
            <Route path="/admin/services/:slug" element={<ProtectedRoute><ServiceDetailPage /></ProtectedRoute>} />
            <Route path="/admin/checks" element={<ProtectedRoute><ChecksPage /></ProtectedRoute>} />
            <Route path="/admin/services/:slug/checks/new" element={<ProtectedRoute><CheckFormPage /></ProtectedRoute>} />
            <Route path="/admin/services/:slug/checks/:checkSlug" element={<ProtectedRoute><CheckDetailPage /></ProtectedRoute>} />
            <Route path="/admin/services/:slug/checks/:checkSlug/logs" element={<ProtectedRoute><CheckLogsPage /></ProtectedRoute>} />

            {/* Incidents */}
            <Route path={ROUTES.INCIDENTS.LIST} element={<ProtectedRoute><IncidentsPage /></ProtectedRoute>} />
            <Route path={ROUTES.INCIDENTS.NEW} element={<ProtectedRoute><IncidentFormPage /></ProtectedRoute>} />
            <Route path="/admin/incidents/:id" element={<ProtectedRoute><IncidentDetailPage /></ProtectedRoute>} />

            {/* Channels */}
            <Route path={ROUTES.CHANNELS.LIST} element={<ProtectedRoute><ChannelsPage /></ProtectedRoute>} />
            <Route path={ROUTES.CHANNELS.NEW} element={<ProtectedRoute><ChannelFormPage /></ProtectedRoute>} />
            <Route path="/admin/channels/:id" element={<ProtectedRoute><ChannelFormPage /></ProtectedRoute>} />

            {/* Maintenances */}
            <Route path={ROUTES.MAINTENANCES.LIST} element={<ProtectedRoute><MaintenancesPage /></ProtectedRoute>} />
            <Route path={ROUTES.MAINTENANCES.NEW} element={<ProtectedRoute><MaintenanceFormPage /></ProtectedRoute>} />
            <Route path="/admin/maintenances/:id" element={<ProtectedRoute><MaintenanceDetailPage /></ProtectedRoute>} />

            {/* Logs */}
            <Route path={ROUTES.LOGS} element={<ProtectedRoute><LogsPage /></ProtectedRoute>} />

            {/* Configuration */}
            <Route path={ROUTES.CONFIG.SITE} element={<ProtectedRoute><SiteConfigPage /></ProtectedRoute>} />
            <Route path={ROUTES.CONFIG.EMAIL} element={<ProtectedRoute><EmailConfigPage /></ProtectedRoute>} />
            <Route path={ROUTES.CONFIG.SSO} element={<ProtectedRoute><SsoPage /></ProtectedRoute>} />
            <Route path={ROUTES.CONFIG.API_KEYS} element={<ProtectedRoute><ApiKeysPage /></ProtectedRoute>} />
            <Route path={ROUTES.CONFIG.USERS} element={<ProtectedRoute><UsersPage /></ProtectedRoute>} />
            <Route path={ROUTES.CONFIG.WORKERS} element={<ProtectedRoute><WorkersPage /></ProtectedRoute>} />
            <Route path={ROUTES.CONFIG.IMPORT} element={<ProtectedRoute><ImportPage /></ProtectedRoute>} />

            {/* Catch-all */}
            <Route path="*" element={<Navigate to={ROUTES.DASHBOARD} replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
