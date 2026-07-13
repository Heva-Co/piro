import {
  Sparkles,
  Blend,
  Activity,
  AlertTriangle,
  CloudAlert,
  ClockAlert,
  Siren,
  Rocket,
  type LucideIcon,
} from "lucide-react";

export interface ShowcaseSlide {
  icon: LucideIcon;
  title: string;
  description: string;
  example?: string;
  color: string;
}

export const SHOWCASE_SLIDES: ShowcaseSlide[] = [
  {
    icon: Sparkles,
    title: "Welcome to Piro",
    description: "A quick tour of how everything fits together — services, checks, alerts, incidents, and on-call.",
    color: "#3d96fe",
  },
  {
    icon: Blend,
    title: "Services",
    description: "Organize what you monitor into services — the units that show up on your public status page.",
    example: "Piro Frontend, Piro API, Piro Worker",
    color: "#6366f1",
  },
  {
    icon: Activity,
    title: "Checks",
    description: "Each service has checks — HTTP, ping, DNS, and more — running on a schedule to verify it's healthy.",
    example: "GET /health every 30s, ping db.internal every minute",
    color: "#22c55e",
  },
  {
    icon: AlertTriangle,
    title: "Alerts",
    description: "When a check fails past its threshold, it fires an alert and starts paging on-call automatically.",
    example: "\"Piro API health\" failed 3 times in a row",
    color: "#f97316",
  },
  {
    icon: Siren,
    title: "On-call & Escalation",
    description: "Define rotations and escalation policies so the right person gets notified, in the right order.",
    example: "Backend on-call (weekly) → escalate to team lead after 10 min",
    color: "#ef4444",
  },
  {
    icon: CloudAlert,
    title: "Incidents",
    description: "Link an alert to an incident to communicate it publicly — creation is always a deliberate, manual step.",
    example: "\"Piro API is down\" — posted to the public status page",
    color: "#a855f7",
  },
  {
    icon: ClockAlert,
    title: "Maintenances",
    description: "Schedule maintenance windows so planned downtime doesn't trigger false alerts.",
    example: "Database upgrade, Sunday 2:00–4:00 AM",
    color: "#eab308",
  },
  {
    icon: Rocket,
    title: "You're all set",
    description: "That's the whole picture. Head to the dashboard to start setting things up.",
    color: "#3d96fe",
  },
];
