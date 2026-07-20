import { BrowserRouter, Routes, Route, Navigate, Outlet } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "@/providers/AuthProvider";
import { ThemeProvider } from "@/providers/ThemeProvider";
import { ConfirmDialogProvider } from "@/providers/ConfirmDialogProvider";
import { TimezoneProvider } from "@/providers/TimezoneProvider";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AuthGuard } from "@/components/AuthGuard";
import AdminLayout from "@/components/AdminLayout";
import { ErrorBoundary, ErrorFallback } from "@/components/ErrorBoundary";
import { ROUTES } from "@/constants/routes";

// Auth pages
import SignInPage from "@/features/auth/pages/SignInPage";
import OidcCallbackPage from "@/features/auth/pages/OidcCallbackPage";
import SetupPage from "@/features/auth/pages/SetupPage";
import AcceptInvitePage from "@/features/auth/pages/AcceptInvitePage";
import ForgotPasswordPage from "@/features/auth/pages/ForgotPasswordPage";
import ResetPasswordPage from "@/features/auth/pages/ResetPasswordPage";

// Admin pages
import DashboardPage from "@/features/dashboard/pages/DashboardPage";
import ServicesPage from "@/features/services/pages/ServicesPage";
import ServiceFormPage from "@/features/services/pages/ServiceFormPage";
import ServiceDetailPage from "@/features/services/pages/ServiceDetailPage";
import ChecksPage from "@/features/checks/pages/ChecksPage";
import CheckFormPage from "@/features/checks/pages/CheckFormPage";
import CheckDetailPage from "@/features/checks/pages/CheckDetailPage";
import CheckLogsPage from "@/features/checks/pages/CheckLogsPage";
import AlertsPage from "@/features/alerts/pages/AlertsPage";
import AlertDetailPage from "@/features/alerts/pages/AlertDetailPage";
import IncidentsPage from "@/features/incidents/pages/IncidentsPage";
import IncidentFormPage from "@/features/incidents/pages/IncidentFormPage";
import IncidentDetailPage from "@/features/incidents/pages/IncidentDetailPage";
import IncidentTimelinePage from "@/features/incidents/pages/IncidentTimelinePage";
import IntegrationsPage from "@/features/integrations/pages/IntegrationsPage";
import IntegrationFormPage from "@/features/integrations/pages/IntegrationFormPage";
import IntegrationOAuthCallbackPage from "@/features/integrations/pages/IntegrationOAuthCallbackPage";
import MaintenancesPage from "@/features/maintenances/pages/MaintenancesPage";
import MaintenanceFormPage from "@/features/maintenances/pages/MaintenanceFormPage";
import MaintenanceDetailPage from "@/features/maintenances/pages/MaintenanceDetailPage";
import LogsPage from "@/features/logs/pages/LogsPage";
import DeliveryLogsPage from "@/features/delivery-logs/pages/DeliveryLogsPage";
import SiteConfigPage from "@/features/configuration/pages/SiteConfigPage";
import EmailConfigPage from "@/features/configuration/pages/EmailConfigPage";
import UsersPage from "@/features/configuration/pages/UsersPage";
import UserDetailPage from "@/features/configuration/pages/UserDetailPage";
import SsoPage from "@/features/configuration/pages/SsoPage";
import SsoProviderFormPage from "@/features/configuration/pages/SsoProviderFormPage";
import ApiKeysPage from "@/features/configuration/pages/ApiKeysPage";
import WorkersPage from "@/features/configuration/pages/WorkersPage";
import ImportPage from "@/features/configuration/pages/ImportPage";
import JobsPage from "@/features/configuration/pages/JobsPage";
import DataRetentionPage from "@/features/configuration/pages/DataRetentionPage";
import OnCallSchedulesPage from "@/features/oncall/pages/OnCallSchedulesPage";
import OnCallScheduleDetailPage from "@/features/oncall/pages/OnCallScheduleDetailPage";
import EscalationPoliciesPage from "@/features/escalation/pages/EscalationPoliciesPage";
import SubscriptionsPage from "@/features/notification-subscriptions/pages/SubscriptionsPage";
import EscalationPolicyDetailPage from "@/features/escalation/pages/EscalationPolicyDetailPage";
import ProfilePage from "@/features/profile/pages/ProfilePage";
import { ToastContainer } from 'react-toastify';
import { Toaster } from "@/components/ui/sonner";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

function ProtectedLayout() {
  return (
    <AuthGuard>
      <ErrorBoundary fallback={<ErrorFallback />}>
        <AdminLayout>
          <Outlet />
        </AdminLayout>
      </ErrorBoundary>
    </AuthGuard>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ToastContainer />
      <TooltipProvider>
      <ThemeProvider>
      <Toaster />
      <ConfirmDialogProvider>
      <BrowserRouter>
        <AuthProvider>
        <TimezoneProvider>
          <Routes>
            {/* Redirect root to admin */}
            <Route path="/" element={<Navigate to={ROUTES.DASHBOARD} replace />} />

            {/* Public auth routes */}
            <Route path={ROUTES.AUTH.SIGN_IN} element={<SignInPage />} />
            <Route path={ROUTES.AUTH.OIDC_CALLBACK} element={<OidcCallbackPage />} />
            <Route path={ROUTES.SETUP} element={<SetupPage />} />
            <Route path="/admin/invite/:token" element={<AcceptInvitePage />} />
            <Route path={ROUTES.AUTH.FORGOT_PASSWORD} element={<ForgotPasswordPage />} />
            <Route path={ROUTES.AUTH.RESET_PASSWORD} element={<ResetPasswordPage />} />

            {/* Protected admin routes */}
            <Route element={<ProtectedLayout />}>
              <Route path={ROUTES.DASHBOARD} element={<DashboardPage />} />

              {/* Services */}
              <Route path={ROUTES.SERVICES.LIST} element={<ServicesPage />} />
              <Route path={ROUTES.SERVICES.NEW} element={<ServiceFormPage />} />
              <Route path="/admin/services/:slug" element={<ServiceDetailPage />} />
              <Route path="/admin/checks" element={<ChecksPage />} />
              <Route path="/admin/services/:slug/checks/new" element={<CheckFormPage />} />
              <Route path="/admin/services/:slug/checks/:checkSlug" element={<CheckDetailPage />} />
              <Route path="/admin/services/:slug/checks/:checkSlug/logs" element={<CheckLogsPage />} />

              {/* Alerts */}
              <Route path={ROUTES.ALERTS.LIST} element={<AlertsPage />} />
              <Route path="/admin/alerts/:id" element={<AlertDetailPage />} />

              {/* Incidents */}
              <Route path={ROUTES.INCIDENTS.LIST} element={<IncidentsPage />} />
              <Route path={ROUTES.INCIDENTS.NEW} element={<IncidentFormPage />} />
              <Route path="/admin/incidents/:id" element={<IncidentDetailPage />} />
              <Route path="/admin/incidents/:id/timeline" element={<IncidentTimelinePage />} />

              {/* Integrations */}
              <Route path={ROUTES.INTEGRATIONS.LIST} element={<IntegrationsPage />} />
              <Route path={ROUTES.INTEGRATIONS.NEW} element={<IntegrationFormPage />} />
              <Route path="/admin/settings/integrations/:id" element={<IntegrationFormPage />} />
              <Route path={ROUTES.INTEGRATIONS.OAUTH_CALLBACK} element={<IntegrationOAuthCallbackPage />} />

              {/* Maintenances */}
              <Route path={ROUTES.MAINTENANCES.LIST} element={<MaintenancesPage />} />
              <Route path={ROUTES.MAINTENANCES.NEW} element={<MaintenanceFormPage />} />
              <Route path="/admin/maintenances/:id" element={<MaintenanceDetailPage />} />

              {/* Logs */}
              <Route path={ROUTES.LOGS} element={<LogsPage />} />
              <Route path={ROUTES.LOGS_DELIVERIES} element={<DeliveryLogsPage />} />

              {/* Configuration */}
              <Route path={ROUTES.CONFIG.SITE} element={<SiteConfigPage />} />
              <Route path={ROUTES.CONFIG.EMAIL} element={<EmailConfigPage />} />
              <Route path={ROUTES.CONFIG.SSO} element={<SsoPage />} />
              <Route path={ROUTES.CONFIG.SSO_NEW} element={<SsoProviderFormPage />} />
              <Route path={ROUTES.CONFIG.SSO_DETAIL(":id")} element={<SsoProviderFormPage />} />
              <Route path={ROUTES.CONFIG.API_KEYS} element={<ApiKeysPage />} />
              <Route path={ROUTES.CONFIG.USERS} element={<UsersPage />} />
              <Route path={ROUTES.CONFIG.USER_DETAIL(":id")} element={<UserDetailPage />} />
              <Route path={ROUTES.CONFIG.WORKERS} element={<WorkersPage />} />
              <Route path={ROUTES.CONFIG.IMPORT} element={<ImportPage />} />
              <Route path={ROUTES.CONFIG.JOBS} element={<JobsPage />} />
              <Route path={ROUTES.CONFIG.DATA_RETENTION} element={<DataRetentionPage />} />

              {/* On-Call */}
              <Route path={ROUTES.ONCALL.LIST} element={<OnCallSchedulesPage />} />
              <Route path="/admin/oncall/:id" element={<OnCallScheduleDetailPage />} />

              {/* Escalation policies */}
              <Route path={ROUTES.ESCALATION.LIST} element={<EscalationPoliciesPage />} />
              <Route path={ROUTES.ESCALATION.DETAIL(":policyId")} element={<EscalationPolicyDetailPage />} />

              {/* Notification subscriptions (RFC 0009) */}
              <Route path={ROUTES.NOTIFICATION_SUBSCRIPTIONS.LIST} element={<SubscriptionsPage />} />
              <Route path={ROUTES.PROFILE} element={<ProfilePage />} />
            </Route>

            {/* Catch-all */}
            <Route path="*" element={<Navigate to={ROUTES.DASHBOARD} replace />} />
          </Routes>
        </TimezoneProvider>
        </AuthProvider>
      </BrowserRouter>
      </ConfirmDialogProvider>
      </ThemeProvider>
      </TooltipProvider>
    </QueryClientProvider>
  );
}
