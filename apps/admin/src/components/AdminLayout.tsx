import { useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  LayoutDashboard,
  Blend,
  Activity,
  CloudAlert,
  ClockAlert,
  Bell,
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
  User,
} from "lucide-react";
import { useTheme } from "@/providers/ThemeProvider";
import { useAuth } from "@/hooks/useAuth";
import { siteApi } from "@/lib/api";
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
  { label: "Incidents", to: ROUTES.INCIDENTS.LIST, icon: <CloudAlert size={18} /> },
  { label: "Maintenances", to: ROUTES.MAINTENANCES.LIST, icon: <ClockAlert size={18} /> },
  { label: "Notification Channels", to: ROUTES.CHANNELS.LIST, icon: <Bell size={18} /> },
  { label: "On-Call", to: ROUTES.ONCALL.LIST, icon: <CalendarClock size={18} /> },
  { label: "Escalation", to: ROUTES.ESCALATION, icon: <Siren size={18} /> },
  { label: "Logs", to: ROUTES.LOGS, icon: <ScrollText size={18} /> },
];

const configNavItems: NavItem[] = [
  { label: "Integrations", to: ROUTES.INTEGRATIONS.LIST, icon: <Plug size={18} /> },
  { label: "API Keys", to: ROUTES.CONFIG.API_KEYS, icon: <Key size={18} /> },
  { label: "Workers", to: ROUTES.CONFIG.WORKERS, icon: <Server size={18} /> },
  { label: "Users", to: ROUTES.CONFIG.USERS, icon: <Users size={18} /> },
  { label: "SSO", to: ROUTES.CONFIG.SSO, icon: <KeyRound size={18} /> },
  { label: "Site", to: ROUTES.CONFIG.SITE, icon: <Globe size={18} /> },
  { label: "Email", to: ROUTES.CONFIG.EMAIL, icon: <Mail size={18} /> },
  { label: "Incidents", to: ROUTES.CONFIG.INCIDENTS, icon: <CloudAlert size={18} /> },
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
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  const { data: siteConfig } = useQuery({
    queryKey: QUERY_KEYS.SITE_CONFIG,
    queryFn: () => siteApi.get(),
    staleTime: 60_000,
  });

  const siteName = siteConfig?.name || "Piro";
  const logoUrl = siteConfig?.logoUrl;
  const apiBase = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

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
        </NavLink>
        {onClose && (
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground lg:hidden p-1">
            <X size={18} />
          </button>
        )}
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-2 space-y-0.5 px-2">
        {mainNavItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            onClick={onClose}
            className={({ isActive }) =>
              cn(
                "flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors",
                isActive
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
              )
            }
          >
            {item.icon}
            {item.label}
          </NavLink>
        ))}

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
  title?: string;
}

export function AdminLayout({ children, title }: AdminLayoutProps) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const { resolvedTheme, setTheme } = useTheme();

  const { data: siteConfig } = useQuery({
    queryKey: QUERY_KEYS.SITE_CONFIG,
    queryFn: () => siteApi.get(),
    staleTime: 60_000,
  });
  const siteUrl = siteConfig?.url;

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

          {title && <h1 className="text-sm font-semibold">{title}</h1>}

          <div className="flex-1" />

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

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
}
