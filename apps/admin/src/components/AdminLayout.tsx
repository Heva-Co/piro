import { useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Blend,
  Activity,
  CloudAlert,
  ClockAlert,
  Bell,
  ScrollText,
  Settings,
  Upload,
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
} from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { ROUTES } from "@/constants/routes";
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
  { label: "Logs", to: ROUTES.LOGS, icon: <ScrollText size={18} /> },
];

const configNavItems: NavItem[] = [
  { label: "Import", to: ROUTES.CONFIG.IMPORT, icon: <Upload size={18} /> },
  { label: "API Keys", to: ROUTES.CONFIG.API_KEYS, icon: <Key size={18} /> },
  { label: "Workers", to: ROUTES.CONFIG.WORKERS, icon: <Server size={18} /> },
  { label: "Users", to: ROUTES.CONFIG.USERS, icon: <Users size={18} /> },
  { label: "SSO", to: ROUTES.CONFIG.SSO, icon: <KeyRound size={18} /> },
  { label: "Site", to: ROUTES.CONFIG.SITE, icon: <Globe size={18} /> },
  { label: "Email", to: ROUTES.CONFIG.EMAIL, icon: <Mail size={18} /> },
];

interface SidebarProps {
  onClose?: () => void;
}

function Sidebar({ onClose }: SidebarProps) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const isOnConfig = location.pathname.startsWith("/admin/configuration");
  const [configOpen, setConfigOpen] = useState(isOnConfig);

  function handleLogout() {
    logout();
    navigate(ROUTES.AUTH.SIGN_IN);
  }

  return (
    <div className="flex flex-col h-full bg-gray-900 text-gray-100 w-60">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-4 border-b border-gray-700">
        <span className="text-lg font-semibold tracking-tight">Piro</span>
        {onClose && (
          <button onClick={onClose} className="text-gray-400 hover:text-white lg:hidden">
            <X size={20} />
          </button>
        )}
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-3 space-y-0.5 px-2">
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
                  ? "bg-indigo-600 text-white"
                  : "text-gray-300 hover:bg-gray-700 hover:text-white"
              )
            }
          >
            {item.icon}
            {item.label}
          </NavLink>
        ))}

        {/* Configuration section */}
        <div className="pt-2">
          <button
            onClick={() => setConfigOpen((o) => !o)}
            className="flex items-center justify-between w-full px-3 py-2 rounded-md text-sm font-medium text-gray-400 hover:bg-gray-700 hover:text-white transition-colors"
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
                        ? "bg-indigo-600 text-white font-medium"
                        : "text-gray-300 hover:bg-gray-700 hover:text-white"
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

      {/* Footer */}
      <div className="border-t border-gray-700 px-4 py-3">
        <div className="text-sm text-gray-300 truncate mb-2">{user?.email}</div>
        <button
          onClick={handleLogout}
          className="flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors"
        >
          <LogOut size={16} />
          Sign out
        </button>
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

  return (
    <div className="flex h-screen bg-gray-50 text-gray-900 overflow-hidden">
      {/* Desktop sidebar */}
      <div className="hidden lg:flex flex-shrink-0">
        <Sidebar />
      </div>

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

      {/* Main content */}
      <div className="flex flex-col flex-1 overflow-hidden">
        {/* Top bar */}
        <header className="flex items-center gap-4 px-4 py-3 bg-white border-b border-gray-200 shadow-sm">
          <button
            className="lg:hidden text-gray-500 hover:text-gray-900"
            onClick={() => setMobileOpen(true)}
          >
            <Menu size={22} />
          </button>
          {title && (
            <h1 className="text-lg font-semibold text-gray-900">{title}</h1>
          )}
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
}
