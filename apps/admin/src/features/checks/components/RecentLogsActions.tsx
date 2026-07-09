import { Button } from "@/components/ui/button";
import { ROUTES } from "@/constants/routes";
import { useCheckLogs } from "@/hooks/useChecks";
import { ExternalLink, RefreshCw } from "lucide-react";
import { useNavigate } from "react-router-dom";

function RecentLogsActions({ serviceSlug, checkSlug }: { serviceSlug: string; checkSlug: string }) {
  const navigate = useNavigate();
  const { isFetching, refetch } = useCheckLogs(serviceSlug, checkSlug);
  return (
    <>
      <Button onClick={() => refetch()} disabled={isFetching}
        className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors">
        <RefreshCw size={12} className={isFetching ? "animate-spin" : ""} />
        Refresh
      </Button>
      <Button onClick={() => navigate(ROUTES.CHECKS.LOGS(serviceSlug, checkSlug))}
        className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors">
        <ExternalLink size={12} />
        View all logs
      </Button>
    </>
  );
}

export default RecentLogsActions;