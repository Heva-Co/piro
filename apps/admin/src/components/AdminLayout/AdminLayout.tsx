import { useState, useEffect } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import {
  LayoutDashboard,
  Blend,
  Activity,
  AlertTriangle,
  CloudAlert,
  ClockAlert,
  ScrollText,
  Settings,
  Key,
  Server,
  Users,
  KeyRound,
  Globe,
  Mail,
  LogOut,
  Menu,
  X,
  ChevronDown,
  MoreVertical,
  PanelLeft,
  Plug,
  Sun,
  Moon,
  CalendarClock,
  Siren,
  BellRing,
  Send,
  User,
  Clock,
  TriangleAlert,
  Search,
  DatabaseZap,
} from "lucide-react";
import { useTheme } from "@/providers/ThemeProvider";
import { useAuth } from "@/hooks/useAuth";
import { useHealth } from "@/hooks/useHealth";
import { useTimezone } from "@/hooks/useTimezone";
import { useMyOnCallCurrentStatus } from "@/hooks/useOnCallMe";
import { useOnCallNowDismissal } from "@/hooks/useOnCallNowDismissal";
import { useTimezoneMismatchDismissal } from "@/hooks/useTimezoneMismatchDismissal";
import { TimezoneMismatchBanner } from "@/components/TimezoneMismatchBanner";
import { OnCallNowBanner } from "@/components/OnCallNowBanner";
import { Tooltip, TooltipTrigger, TooltipContent } from "@/components/ui/tooltip";
import { GlobalSearchDialog } from "@/components/GlobalSearchDialog";
import { siteApi, maintenancesApi } from "@/lib/api";
import { alertsApi } from "@/lib/actions/alerts";
import { incidentsApi } from "@/lib/actions/incidents";
import { ROUTES } from "@/constants/routes";
import { QUERY_KEYS } from "@/constants/api";
import { cn } from "@/lib/utils";
import type { ReactNode } from "react";

interface NavItem {
  label: string;
  to: string;
  icon: ReactNode;
  end?: boolean;
}

const mainNavItems: NavItem[] = [
  { label: "Overview", to: ROUTES.DASHBOARD, icon: <LayoutDashboard size={18} />, end: true },
  { label: "Services", to: ROUTES.SERVICES.LIST, icon: <Blend size={18} /> },
  { label: "Checks", to: ROUTES.CHECKS.LIST, icon: <Activity size={18} /> },
  { label: "Alerts", to: ROUTES.ALERTS.LIST, icon: <AlertTriangle size={18} /> },
  { label: "Incidents", to: ROUTES.INCIDENTS.LIST, icon: <CloudAlert size={18} /> },
  { label: "Maintenances", to: ROUTES.MAINTENANCES.LIST, icon: <ClockAlert size={18} /> },
  { label: "On Call Schedules", to: ROUTES.ONCALL.LIST, icon: <CalendarClock size={18} /> },
  { label: "Escalation Policies", to: ROUTES.ESCALATION.LIST, icon: <Siren size={18} /> },
];

const logsNavItems: NavItem[] = [
  { label: "System Logs", to: ROUTES.LOGS, icon: <ScrollText size={18} />, end: true },
  { label: "Delivery Logs", to: ROUTES.LOGS_DELIVERIES, icon: <Send size={18} /> },
];

const configNavItems: NavItem[] = [
  { label: "Event Subscriptions", to: ROUTES.NOTIFICATION_SUBSCRIPTIONS.LIST, icon: <BellRing size={18} /> },
  { label: "Integrations", to: ROUTES.INTEGRATIONS.LIST, icon: <Plug size={18} /> },
  { label: "API Keys", to: ROUTES.CONFIG.API_KEYS, icon: <Key size={18} /> },
  { label: "Workers", to: ROUTES.CONFIG.WORKERS, icon: <Server size={18} /> },
  { label: "Users", to: ROUTES.CONFIG.USERS, icon: <Users size={18} /> },
  { label: "SSO", to: ROUTES.CONFIG.SSO, icon: <KeyRound size={18} /> },
  { label: "Site", to: ROUTES.CONFIG.SITE, icon: <Globe size={18} /> },
  { label: "Email", to: ROUTES.CONFIG.EMAIL, icon: <Mail size={18} /> },
  { label: "Jobs", to: ROUTES.CONFIG.JOBS, icon: <Clock size={18} /> },
  { label: "Data Retention", to: ROUTES.CONFIG.DATA_RETENTION, icon: <DatabaseZap size={18} /> },
];

interface SidebarProps {
  onClose?: () => void;
}

function Sidebar({ onClose }: SidebarProps) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const isOnConfig = location.pathname.startsWith("/admin/configuration") || location.pathname.startsWith("/admin/settings");
  const [configOpen, setConfigOpen] = useState(isOnConfig);
  const isOnLogs = location.pathname.startsWith("/admin/logs");
  const [logsOpen, setLogsOpen] = useState(isOnLogs);
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  const { data: siteConfig } = useQuery({
    queryKey: QUERY_KEYS.SITE_CONFIG,
    queryFn: () => siteApi.get(),
    staleTime: 60_000,
  });

  const { data: activeAlerts } = useQuery({
    queryKey: [...QUERY_KEYS.ALERTS, "active-count"],
    queryFn: () => alertsApi.list({ activeOnly: true, pageSize: 1 }),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });
  const hasActiveAlerts = (activeAlerts?.totalCount ?? 0) > 0;

  const { data: activeIncidents } = useQuery({
    queryKey: [...QUERY_KEYS.INCIDENTS, "active"],
    queryFn: () => incidentsApi.list("active"),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });
  const hasActiveIncidents = (activeIncidents?.length ?? 0) > 0;

  const { data: maintenances } = useQuery({
    queryKey: [...QUERY_KEYS.MAINTENANCES, "sidebar"],
    queryFn: () => maintenancesApi.list(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });
  const hasOngoingMaintenance = (maintenances ?? []).some((m) => m.displayStatus === "Active");

  const siteName = siteConfig?.name || "Piro";
  const logoUrl = siteConfig?.logoUrl;
  const apiBase = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");
  const { data: health } = useHealth();

  function handleLogout() {
    logout();
    navigate(ROUTES.AUTH.SIGN_IN);
  }

  const initials = user?.name
    ? user.name.split(" ").map((n) => n[0]).slice(0, 2).join("").toUpperCase()
    : user?.email?.slice(0, 2).toUpperCase() ?? "?";

  return (
    <div className="flex flex-col h-full bg-sidebar text-sidebar-foreground w-60">
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-3">
        <NavLink to={ROUTES.DASHBOARD} className="flex items-center gap-2 px-1 py-1 rounded-md hover:bg-sidebar-accent transition-colors">
          {logoUrl ? (
            <img src={`${apiBase}${logoUrl}`} alt="Logo" className="size-5 rounded-sm object-contain" />
          ) : (
            <img src="/piro.svg" alt="Piro" className="size-5 rounded-sm object-contain" />
          )}
          <span className="text-base font-semibold">{siteName}</span>
          {health?.version && (
            <span className="text-xs text-muted-foreground font-mono">{health.version}</span>
          )}
        </NavLink>
        {onClose && (
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground lg:hidden p-1">
            <X size={18} />
          </button>
        )}
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-2 space-y-0.5 px-2">
        {mainNavItems.map((item) => {
          const isAlerting =
            (item.to === ROUTES.ALERTS.LIST && hasActiveAlerts) ||
            (item.to === ROUTES.INCIDENTS.LIST && hasActiveIncidents) ||
            (item.to === ROUTES.MAINTENANCES.LIST && hasOngoingMaintenance);
          return (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              onClick={onClose}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors",
                  isAlerting
                    ? "text-destructive hover:bg-destructive/10"
                    : isActive
                      ? "bg-sidebar-accent text-sidebar-accent-foreground"
                      : "text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
                  isAlerting && isActive && "bg-destructive/10"
                )
              }
            >
              {isAlerting ? (
                <motion.span
                  className="inline-flex"
                  animate={{ rotate: [0, -12, 12, -8, 8, 0] }}
                  transition={{ duration: 0.6, repeat: Infinity, repeatDelay: 3, ease: "easeInOut" }}
                >
                  {item.icon}
                </motion.span>
              ) : (
                item.icon
              )}
              {item.label}
            </NavLink>
          );
        })}

        {/* Logs section */}
        <div className="pt-1">
          <button
            onClick={() => setLogsOpen((o) => !o)}
            className="flex items-center justify-between w-full px-3 py-2 rounded-md text-sm font-medium text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors"
          >
            <div className="flex items-center gap-3">
              <ScrollText size={18} />
              Logs
            </div>
            <ChevronDown
              size={16}
              className={cn("transition-transform", logsOpen ? "rotate-180" : "")}
            />
          </button>
          {logsOpen && (
            <div className="ml-4 mt-0.5 space-y-0.5">
              {logsNavItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      "flex items-center gap-3 px-3 py-2 rounded-md text-sm transition-colors",
                      isActive
                        ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                        : "text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
                    )
                  }
                >
                  {item.icon}
                  {item.label}
                </NavLink>
              ))}
            </div>
          )}
        </div>

        {/* Configuration section */}
        <div className="pt-1">
          <button
            onClick={() => setConfigOpen((o) => !o)}
            className="flex items-center justify-between w-full px-3 py-2 rounded-md text-sm font-medium text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors"
          >
            <div className="flex items-center gap-3">
              <Settings size={18} />
              Configuration
            </div>
            <ChevronDown
              size={16}
              className={cn("transition-transform", configOpen ? "rotate-180" : "")}
            />
          </button>
          {configOpen && (
            <div className="ml-4 mt-0.5 space-y-0.5">
              {configNavItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  onClick={onClose}
                  className={({ isActive }) =>
                    cn(
                      "flex items-center gap-3 px-3 py-2 rounded-md text-sm transition-colors",
                      isActive
                        ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                        : "text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
                    )
                  }
                >
                  {item.icon}
                  {item.label}
                </NavLink>
              ))}
            </div>
          )}
        </div>
      </nav>

      {/* Footer — user info */}
      <div className="border-t border-sidebar-border px-3 py-3">
        <div className="relative">
          <button
            onClick={() => setUserMenuOpen((o) => !o)}
            className="flex items-center gap-3 w-full px-2 py-2 rounded-md hover:bg-sidebar-accent transition-colors text-left"
          >
            <div className="size-8 rounded-full bg-muted flex items-center justify-center text-xs font-semibold shrink-0">
              {initials}
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-sm font-medium truncate">{user?.name}</div>
              <div className="text-xs text-muted-foreground truncate">{user?.email}</div>
            </div>
            <MoreVertical size={16} className="text-muted-foreground shrink-0" />
          </button>
          {userMenuOpen && (
            <div className="absolute bottom-full left-0 right-0 mb-1 bg-popover border border-border rounded-md shadow-md py-1 z-50">
              <NavLink
                to={ROUTES.PROFILE}
                onClick={() => setUserMenuOpen(false)}
                className="flex items-center gap-2 w-full px-3 py-2 text-sm hover:bg-muted transition-colors"
              >
                <User size={16} />
                Profile
              </NavLink>
              <button
                onClick={handleLogout}
                className="flex items-center gap-2 w-full px-3 py-2 text-sm hover:bg-muted transition-colors text-destructive"
              >
                <LogOut size={16} />
                Sign out
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

interface AdminLayoutProps {
  children: ReactNode;
}

export function AdminLayout({ children }: AdminLayoutProps) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const { resolvedTheme, setTheme } = useTheme();
  const { mismatch, useBrowserTimeZone, activeTimeZone, profileTimeZone, browserTimeZone } = useTimezone();
  const { data: currentOnCallSlot } = useMyOnCallCurrentStatus();
  const { isDismissed: onCallBannerDismissed } = useOnCallNowDismissal(currentOnCallSlot);
  const showOnCallIcon = !!currentOnCallSlot && onCallBannerDismissed;
  const { isDismissed: timezoneMismatchDismissed } = useTimezoneMismatchDismissal(
    profileTimeZone ?? "",
    browserTimeZone
  );
  const showTimezoneMismatchIcon = mismatch && !useBrowserTimeZone && timezoneMismatchDismissed;

  const { data: siteConfig } = useQuery({
    queryKey: QUERY_KEYS.SITE_CONFIG,
    queryFn: () => siteApi.get(),
    staleTime: 60_000,
  });
  const siteUrl = siteConfig?.url;

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setSearchOpen((open) => !open);
      }
    }
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, []);

  return (
    <div className="flex h-screen bg-muted/40 overflow-hidden p-2 gap-2">
      {/* Desktop sidebar */}
      {!sidebarCollapsed && (
        <div className="hidden lg:flex flex-shrink-0 rounded-xl overflow-hidden border border-border shadow-sm">
          <Sidebar />
        </div>
      )}

      {/* Mobile overlay */}
      {mobileOpen && (
        <div className="fixed inset-0 z-40 flex lg:hidden">
          <div
            className="fixed inset-0 bg-black/50"
            onClick={() => setMobileOpen(false)}
          />
          <div className="relative z-50">
            <Sidebar onClose={() => setMobileOpen(false)} />
          </div>
        </div>
      )}

      {/* Main content — white "paper" panel */}
      <div className="flex flex-col flex-1 overflow-hidden bg-background rounded-xl border border-border shadow-sm">
        {/* Top bar */}
        <header className="flex items-center gap-3 px-4 h-12 border-b border-border shrink-0">
          {/* Mobile menu */}
          <button
            className="lg:hidden text-muted-foreground hover:text-foreground"
            onClick={() => setMobileOpen(true)}
          >
            <Menu size={20} />
          </button>
          {/* Desktop sidebar toggle */}
          <button
            className="hidden lg:flex text-muted-foreground hover:text-foreground transition-colors"
            onClick={() => setSidebarCollapsed((c) => !c)}
            title={sidebarCollapsed ? "Show sidebar" : "Hide sidebar"}
          >
            <PanelLeft size={18} />
          </button>

          <div className="h-4 w-px bg-border mx-1" />

          <button
            onClick={() => setSearchOpen(true)}
            className="flex items-center gap-2 rounded-lg border border-border bg-muted/40 px-3 py-1.5 text-sm text-muted-foreground hover:bg-muted transition-colors w-full max-w-xs"
          >
            <Search size={14} className="shrink-0" />
            <span className="flex-1 text-left truncate">Search...</span>
            <span className="hidden sm:flex items-center gap-0.5 shrink-0">
              <kbd className="rounded border border-border bg-background px-1.5 py-0.5 text-[10px] font-medium">⌘</kbd>
              <kbd className="rounded border border-border bg-background px-1.5 py-0.5 text-[10px] font-medium">K</kbd>
            </span>
          </button>

          <div className="flex-1" />

          {useBrowserTimeZone && (
            <Tooltip>
              <TooltipTrigger
                render={
                  <span className="p-1.5 rounded-md text-amber-600 dark:text-amber-400" />
                }
              >
                <TriangleAlert size={16} />
              </TooltipTrigger>
              <TooltipContent>
                Showing times in {activeTimeZone} for this session only. Your profile timezone is {profileTimeZone}.
              </TooltipContent>
            </Tooltip>
          )}

          {showTimezoneMismatchIcon && (
            <Tooltip>
              <TooltipTrigger
                render={
                  <span className="p-1.5 rounded-md text-amber-600 dark:text-amber-400" />
                }
              >
                <Globe size={16} />
              </TooltipTrigger>
              <TooltipContent>
                Your profile timezone is {profileTimeZone}, but this device is set to {browserTimeZone}.
              </TooltipContent>
            </Tooltip>
          )}

          {showOnCallIcon && (
            <Tooltip>
              <TooltipTrigger
                render={
                  <span className="p-1.5 rounded-md text-blue-600 dark:text-blue-400" />
                }
              >
                <Siren size={16} />
              </TooltipTrigger>
              <TooltipContent>
                You're on-call now for {currentOnCallSlot?.scheduleName}.
              </TooltipContent>
            </Tooltip>
          )}

          <button
            onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
            title="Toggle theme"
          >
            {resolvedTheme === "dark" ? <Sun size={16} /> : <Moon size={16} />}
          </button>

          {siteUrl && (
            <a
              href={siteUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs px-3 py-1.5 rounded-md border border-border hover:bg-muted transition-colors font-medium"
            >
              Status Page
            </a>
          )}
        </header>

        <TimezoneMismatchBanner />
        <OnCallNowBanner />

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>

      <GlobalSearchDialog open={searchOpen} onOpenChange={setSearchOpen} />
    </div>
  );
}

export default AdminLayout;