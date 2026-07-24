import { useNavigate } from "react-router-dom";
import { Plug, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription, EmptyContent } from "@/components/ui/empty";
import { ROUTES } from "@/constants/routes";

/** Shown in the integrations list when no integrations exist yet. */
function IntegrationsEmptyState() {
  const navigate = useNavigate();

  return (
    <Empty className="py-16">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <Plug />
        </EmptyMedia>
        <EmptyTitle>No integrations yet</EmptyTitle>
        <EmptyDescription>Connect a service to deliver notifications, run provider-backed checks, or take actions.</EmptyDescription>
      </EmptyHeader>
      <EmptyContent>
        <Button onClick={() => navigate(ROUTES.INTEGRATIONS.NEW)}>
          <Plus />
          Add your first integration
        </Button>
      </EmptyContent>
    </Empty>
  );
}

export default IntegrationsEmptyState;
