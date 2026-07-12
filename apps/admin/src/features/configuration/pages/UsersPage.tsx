import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, User as UserIcon, AlertCircle } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usersApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { capitalize } from "@/lib/utils";
import { useFormattedDate } from "@/hooks/useFormattedDate";

interface RoleOption { id: number; name: string; }

export default function UsersPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { formatDate } = useFormattedDate();
  const { data: users = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.USERS,
    queryFn: usersApi.list,
  });

  const { data: roles = [] } = useQuery<RoleOption[]>({
    queryKey: QUERY_KEYS.ROLES,
    queryFn: usersApi.roles as () => Promise<RoleOption[]>,
  });

  const [showInvite, setShowInvite] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRoleId, setInviteRoleId] = useState<number | "">("");
  const [inviteError, setInviteError] = useState("");

  const defaultRoleId = roles.find((r) => r.name.toLowerCase() === "admin")?.id ?? roles[0]?.id;

  const inviteMutation = useMutation({
    mutationFn: () => usersApi.invite(inviteEmail, inviteRoleId as number),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USERS });
      setInviteEmail("");
      setInviteRoleId("");
      setShowInvite(false);
      setInviteError("");
    },
    onError: () => setInviteError("Failed to send invite."),
  });

  function openInvite() {
    setInviteEmail("");
    setInviteRoleId(defaultRoleId ?? "");
    setInviteError("");
    setShowInvite(true);
  }

  return (
    <div>
      <PageHeader
        breadcrumbs={[{ label: "Users" }]}
        actions={
          <Button onClick={openInvite}>
            <Plus size={15} /> Invite User
          </Button>
        }
      />
      <p className="text-sm text-muted-foreground -mt-4 mb-6">
        Manage team members and their access roles.
      </p>

      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading && (
          <div className="py-16 text-center text-sm text-muted-foreground">Loading…</div>
        )}
        {!isLoading && users.length === 0 && (
          <div className="py-16 text-center text-sm text-muted-foreground">No users found.</div>
        )}
        {users.map((u, i) => {
          const role = u.roles?.[0] ?? "";
          const createdAt = u.createdAt
            ? formatDate(u.createdAt, { month: "numeric", day: "numeric", year: "numeric" })
            : null;

          return (
            <div key={u.id}
              onClick={() => navigate(ROUTES.CONFIG.USER_DETAIL(u.id))}
              className={`flex items-center gap-4 px-5 py-4 cursor-pointer hover:bg-muted/50 transition-colors ${i > 0 ? "border-t border-border" : ""}`}
            >
              <div className="w-9 h-9 rounded-full bg-muted flex items-center justify-center shrink-0">
                <UserIcon size={18} className="text-muted-foreground" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold">{u.name}</p>
                <p className="text-xs text-muted-foreground">{u.email}</p>
              </div>
              <div className="flex items-center gap-4">
                <span className="rounded-full bg-foreground px-3 py-0.5 text-xs font-semibold text-background">
                  {capitalize(role)}
                </span>
                {createdAt && (
                  <span className="text-sm text-muted-foreground">{createdAt}</span>
                )}
              </div>
            </div>
          );
        })}
      </div>

      <Dialog open={showInvite} onOpenChange={setShowInvite}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Invite User</DialogTitle>
            <DialogDescription>Send an invitation email to add a new team member.</DialogDescription>
          </DialogHeader>
          <form onSubmit={(e) => { e.preventDefault(); inviteMutation.mutate(); }} className="flex flex-col gap-4">
            {inviteError && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle size={14} /> {inviteError}
              </div>
            )}
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Email address</label>
              <Input type="email" value={inviteEmail} onChange={(e) => setInviteEmail(e.target.value)}
                required autoFocus placeholder="colleague@example.com" />
            </div>
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Role</label>
              <Select value={String(inviteRoleId)} onValueChange={(v) => v && setInviteRoleId(Number(v))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select role" />
                </SelectTrigger>
                <SelectContent>
                  {roles.map((r) => (
                    <SelectItem key={r.id} value={String(r.id)}>{capitalize(r.name)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setShowInvite(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={inviteMutation.isPending || !inviteEmail.trim() || !inviteRoleId}>
                {inviteMutation.isPending ? "Sending…" : "Send Invitation"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
