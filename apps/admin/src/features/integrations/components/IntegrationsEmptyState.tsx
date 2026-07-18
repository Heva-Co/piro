import { useNavigate } from "react-router-dom";
import { Plug, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ROUTES } from "@/constants/routes";

/** Shown in the integrations list when no integrations exist yet. */
function IntegrationsEmptyState() {
  const navigate = useNavigate();

  return (
    <div className="flex flex-col items-center justify-center gap-4 py-16">
      <Plug size={32} className="text-muted-foreground/40" />
      <p className="text-sm text-muted-foreground">No integrations yet.</p>
      <Button onClick={() => navigate(ROUTES.INTEGRATIONS.NEW)}>
        <Plus />
        Add your first integration
      </Button>
    </div>
  );
}

export default IntegrationsEmptyState;
