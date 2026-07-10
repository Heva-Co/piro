import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Bell, Settings } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { SectionAccordion } from "@/components/ui/section-accordion";
import DangerZone from "@/components/DangerZone";
import { usersApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { useAuth } from "@/hooks/useAuth";
import UserProfileSection from "../components/UserProfileSection";
import UserRoleSection from "../components/UserRoleSection";
import UserNotificationPreferencesSection from "../components/UserNotificationPreferencesSection";

export default function UserDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user: me } = useAuth();
  const userId = Number(id);

  const { data: user, isLoading } = useQuery({
    queryKey: [...QUERY_KEYS.USERS, userId],
    queryFn: () => usersApi.get(userId),
  });

  const { data: roles = [] } = useQuery({
    queryKey: QUERY_KEYS.ROLES,
    queryFn: usersApi.roles as () => Promise<{ id: number; name: string }[]>,
  });

  const isMe = me?.id === userId;
  const isOwner = user?.roles?.some((r) => r.toLowerCase() === "owner") ?? false;

  async function handleDelete() {
    await usersApi.delete(userId);
    navigate(ROUTES.CONFIG.USERS);
  }

  if (isLoading) {
    return <div className="py-12 text-center text-sm text-muted-foreground">Loading…</div>;
  }

  if (!user) {
    return <div className="py-12 text-center text-sm text-muted-foreground">User not found.</div>;
  }

  return (
    <div>
      <PageHeader
        breadcrumbs={[
          { label: "Users", onClick: () => navigate(ROUTES.CONFIG.USERS) },
          { label: user.name || user.email },
        ]}
      />

      <SectionAccordion
        title="Profile"
        description="Basic information about this user"
        icon={<Settings size={16} className="text-muted-foreground" />}
        defaultOpen
      >
        <UserProfileSection user={user} />
      </SectionAccordion>

      {!isMe && (
        <SectionAccordion
          title="Role"
          description="Change the access level for this user"
          icon={<Settings size={16} className="text-muted-foreground" />}
        >
          <UserRoleSection user={user} roles={roles} />
        </SectionAccordion>
      )}

      <SectionAccordion
        title="Notification Preferences"
        description="Personal handles used when this user is on-call"
        icon={<Bell size={16} className="text-muted-foreground" />}
      >
        <UserNotificationPreferencesSection userId={userId} />
      </SectionAccordion>

      {!isMe && !isOwner && (
        <SectionAccordion
          title="Danger Zone"
          description="Irreversible actions for this user"
          icon={<AlertTriangle size={16} className="text-destructive" />}
          titleClassName="text-destructive"
        >
          <DangerZone objectName="user" objectId={user.email} onDelete={handleDelete} />
        </SectionAccordion>
      )}
    </div>
  );
}
