import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, User as UserIcon } from "lucide-react";
import { toast } from "react-toastify";
import { AdminLayout } from "@/components/AdminLayout";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usersApi } from "@/lib/api";
import { QUERY_KEYS, CHANNEL_TYPE_LABELS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { useAuth } from "@/hooks/useAuth";

function capitalize(s: string) {
  return s ? s.charAt(0).toUpperCase() + s.slice(1) : s;
}

export default function UserDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
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

  const { data: preferences = [], isLoading: prefsLoading } = useQuery({
    queryKey: QUERY_KEYS.USER_NOTIFICATION_PREFERENCES(userId),
    queryFn: () => usersApi.getNotificationPreferences(userId),
  });

  const [selectedRoleId, setSelectedRoleId] = useState<number | "">("");

  if (user && selectedRoleId === "") {
    const currentRole = roles.find((r) => r.name.toLowerCase() === user.roles?.[0]?.toLowerCase());
    if (currentRole) setSelectedRoleId(currentRole.id);
  }

  const updateRoleMutation = useMutation({
    mutationFn: (roleId: number) => usersApi.updateRole(userId, roleId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [...QUERY_KEYS.USERS, userId] });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USERS });
      toast.success("Role updated.");
    },
    onError: () => toast.error("Failed to update role."),
  });

  const isMe = me?.id === userId;

  if (isLoading) {
    return (
      <AdminLayout title="User">
        <div className="py-12 text-center text-sm text-muted-foreground">Loading…</div>
      </AdminLayout>
    );
  }

  if (!user) {
    return (
      <AdminLayout title="User">
        <div className="py-12 text-center text-sm text-muted-foreground">User not found.</div>
      </AdminLayout>
    );
  }

  const initials = user.name
    ? user.name.split(" ").map((n: string) => n[0]).slice(0, 2).join("").toUpperCase()
    : <UserIcon size={20} />;

  return (
    <AdminLayout title="User detail">
      <div className="max-w-2xl space-y-6">

        {/* Back */}
        <button
          type="button"
          onClick={() => navigate(ROUTES.CONFIG.USERS)}
          className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft size={14} /> Users
        </button>

        {/* Profile card */}
        <div className="rounded-xl border border-border bg-card shadow-sm">
          <div className="px-6 py-4 border-b border-border">
            <h2 className="text-sm font-semibold">Profile</h2>
          </div>
          <div className="px-6 py-5 flex items-center gap-4">
            <div className="size-12 rounded-full bg-muted flex items-center justify-center text-sm font-semibold shrink-0">
              {initials}
            </div>
            <div>
              <div className="text-sm font-medium">{user.name || <span className="text-muted-foreground italic">No name</span>}</div>
              <div className="text-xs text-muted-foreground">{user.email}</div>
              <div className="flex gap-1 mt-1">
                {user.roles?.map((r: string) => (
                  <span key={r} className="inline-block rounded px-1.5 py-0.5 text-xs bg-muted text-muted-foreground">
                    {capitalize(r)}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Role card */}
        {!isMe && (
          <div className="rounded-xl border border-border bg-card shadow-sm">
            <div className="px-6 py-4 border-b border-border">
              <h2 className="text-sm font-semibold">Role</h2>
              <p className="text-xs text-muted-foreground mt-0.5">Change the access level for this user.</p>
            </div>
            <div className="px-6 py-5 flex items-end gap-3">
              <div className="space-y-1.5 flex-1 max-w-xs">
                <label className="text-sm font-medium">Role</label>
                <Select
                  value={String(selectedRoleId)}
                  onValueChange={(v) => setSelectedRoleId(Number(v))}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select role" />
                  </SelectTrigger>
                  <SelectContent>
                    {roles.map((r) => (
                      <SelectItem key={r.id} value={String(r.id)}>
                        {capitalize(r.name)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <button
                onClick={() => updateRoleMutation.mutate(selectedRoleId as number)}
                disabled={updateRoleMutation.isPending || !selectedRoleId}
                className="rounded-lg bg-primary text-primary-foreground px-4 py-2 text-sm font-medium disabled:opacity-50 hover:bg-primary/90 transition-colors"
              >
                {updateRoleMutation.isPending ? "Saving…" : "Save"}
              </button>
            </div>
          </div>
        )}

        {/* Notification preferences card */}
        <div className="rounded-xl border border-border bg-card shadow-sm">
          <div className="px-6 py-4 border-b border-border">
            <h2 className="text-sm font-semibold">Notification preferences</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Personal handles used when this user is on-call.
            </p>
          </div>
          <div className="px-6 py-5">
            {prefsLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
            {!prefsLoading && preferences.length === 0 && (
              <p className="text-sm text-muted-foreground">No preferences configured.</p>
            )}
            {preferences.length > 0 && (
              <div className="space-y-2">
                <div className="grid grid-cols-[1.5rem_1fr_1fr] gap-x-3 text-xs font-medium text-muted-foreground mb-1 px-1">
                  <span>#</span>
                  <span>Integration</span>
                  <span>Handle</span>
                </div>
                {preferences.map((pref) => (
                  <div key={pref.id} className="grid grid-cols-[1.5rem_1fr_1fr] gap-x-3 items-center px-1 py-1.5 rounded-lg hover:bg-muted/50">
                    <span className="text-xs text-muted-foreground">{pref.priority}</span>
                    <span className="text-sm">
                      {CHANNEL_TYPE_LABELS[pref.integrationType] ?? pref.integrationType}
                      <span className="text-muted-foreground"> — {pref.integrationName}</span>
                    </span>
                    <span className="text-sm font-mono text-muted-foreground truncate">{pref.handle}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

      </div>
    </AdminLayout>
  );
}
