import { AutoRefreshButton } from "@/components/AutoRefreshButton";
import { Button } from "@/components/ui/button";
import { ROUTES } from "@/constants/routes";
import { useCheckLogs } from "@/hooks/useChecks";
import { ExternalLink } from "lucide-react";
import { useNavigate } from "react-router-dom";

interface Props {
  serviceSlug: string;
  checkSlug: string;
}

function RecentLogsActions(props: Props) {
  const { serviceSlug, checkSlug } = props;
  const navigate = useNavigate();
  const { refetch } = useCheckLogs(serviceSlug, checkSlug);
  return (
    <>
      <AutoRefreshButton onRefetch={refetch} />
      <Button onClick={() => navigate(ROUTES.CHECKS.LOGS(serviceSlug, checkSlug))}
        className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors">
        <ExternalLink size={12} />
        View all logs
      </Button>
    </>
  );
}

export default RecentLogsActions;