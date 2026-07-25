import { ListChecks, CheckCircle2, AlertTriangle, XCircle } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Separator } from "@/components/ui/separator";
import type { CheckSummary } from "@/lib/actions/checks";

interface Props {
  checks: CheckSummary[];
}

interface Stat {
  label: string;
  value: number;
  icon: LucideIcon;
  iconClassName?: string;
  valueClassName?: string;
}

function ChecksStatsBar(props: Props) {
  const { checks } = props;

  const total = checks.length;
  const up = checks.filter((c) => c.currentStatus.toLowerCase() === "up").length;
  const degraded = checks.filter((c) => c.currentStatus.toLowerCase() === "degraded").length;
  const down = checks.filter((c) => c.currentStatus.toLowerCase() === "down").length;

  const stats: Stat[] = [
    { label: "Total", value: total, icon: ListChecks },
    { label: "Up", value: up, icon: CheckCircle2, iconClassName: "text-green-600", valueClassName: "text-green-600" },
    { label: "Degraded", value: degraded, icon: AlertTriangle, iconClassName: "text-yellow-600", valueClassName: "text-yellow-600" },
    { label: "Down", value: down, icon: XCircle, iconClassName: "text-red-600", valueClassName: "text-red-600" },
  ];

  return (
    <div className="flex items-stretch rounded-xl border bg-card">
      {stats.map((stat, i) => {
        const Icon = stat.icon;
        return (
          <div key={stat.label} className="flex flex-1 items-stretch">
            {i > 0 && <Separator orientation="vertical" />}
            <div className="flex flex-1 items-center gap-3 px-5 py-4">
              <Icon size={20} className={stat.iconClassName ?? "text-muted-foreground"} />
              <div className="flex flex-col">
                <span className="text-xs text-muted-foreground">{stat.label}</span>
                <span className={`text-2xl font-bold leading-tight ${stat.valueClassName ?? ""}`}>
                  {stat.value}
                </span>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export default ChecksStatsBar;
