import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, User as UserIcon, AlertCircle } from "lucide-react";
import { usersApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";


interface RoleOption { id: number; name: string; }

function Modal({ children, onClose }: { children: React.ReactNode; onClose: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 p-6 relative">
        {children}
      </div>
    </div>
  );
}

function ModalClose({ onClose }: { onClose: () => void }) {
  return (
    <button type="button" onClick={onClose}
      className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-xl leading-none">
      ×
    </button>
  );
}

function capitalize(s: unknown) {
  if (typeof s !== "string" || !s) return String(s ?? "");
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export default function UsersPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { data: users = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.USERS,
    queryFn: usersApi.list,
  });

  const { data: roles = [] } = useQuery<RoleOption[]>({
    queryKey: QUERY_KEYS.ROLES,
    queryFn: usersApi.roles as () => Promise<RoleOption[]>,
  });

  // Invite modal
  const [showInvite, setShowInvite] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRoleId, setInviteRoleId] = useState<number | "">("");
  const [inviteError, setInviteError] = useState("");

  // Change role modal
  const [changeRoleUser, setChangeRoleUser] = useState<{ id: number; name: string } | null>(null);
  const [selectedRoleId, setSelectedRoleId] = useState<number | "">("");

  // Set default role when roles load
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

  const updateRoleMutation = useMutation({
    mutationFn: ({ id, roleId }: { id: number; roleId: number }) => usersApi.updateRole(id, roleId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USERS });
      setChangeRoleUser(null);
    },
  });

  function openInvite() {
    setInviteEmail("");
    setInviteRoleId(defaultRoleId ?? "");
    setInviteError("");
    setShowInvite(true);
  }

  return (
    <>
      {/* Header */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Users</h1>
          <p className="text-sm text-gray-500 mt-0.5">Manage team members and their access roles.</p>
        </div>
        <button
          onClick={openInvite}
          className="flex items-center gap-2 rounded-lg bg-foreground px-4 py-2 text-sm font-medium text-background hover:opacity-90"
        >
          <Plus size={15} /> Invite User
        </button>
      </div>

      {/* Users list */}
      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading && (
          <div className="py-16 text-center text-sm text-gray-400">Loading…</div>
        )}
        {!isLoading && users.length === 0 && (
          <div className="py-16 text-center text-sm text-gray-400">No users found.</div>
        )}
        {users.map((u, i) => {
          const role = u.roles?.[0] ?? "";
          const createdAt = (u as any).createdAt
            ? new Date((u as any).createdAt).toLocaleDateString("en-US", { month: "numeric", day: "numeric", year: "numeric" })
            : null;

          return (
            <div key={u.id}
              onClick={() => navigate(ROUTES.CONFIG.USER_DETAIL(u.id))}
              className={`flex items-center gap-4 px-5 py-4 cursor-pointer hover:bg-muted/50 transition-colors ${i > 0 ? "border-t border-gray-100" : ""}`}
            >
              <div className="w-9 h-9 rounded-full bg-gray-100 flex items-center justify-center shrink-0">
                <UserIcon size={18} className="text-gray-400" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-gray-900">{u.name}</p>
                <p className="text-xs text-gray-400">{u.email}</p>
              </div>
              <div className="flex items-center gap-4">
                <span className="rounded-full bg-foreground px-3 py-0.5 text-xs font-semibold text-white">
                  {capitalize(role)}
                </span>
                {createdAt && (
                  <span className="text-sm text-gray-400">{createdAt}</span>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Invite modal */}
      {showInvite && (
        <Modal onClose={() => setShowInvite(false)}>
          <ModalClose onClose={() => setShowInvite(false)} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">Invite User</h2>
          <p className="text-sm text-gray-500 mb-5">Send an invitation email to add a new team member.</p>
          <form onSubmit={(e) => { e.preventDefault(); inviteMutation.mutate(); }} className="flex flex-col gap-4">
            {inviteError && (
              <div className="flex items-center gap-2 text-sm text-red-600">
                <AlertCircle size={14} /> {inviteError}
              </div>
            )}
            <div>
              <label className="block text-sm font-semibold text-gray-900 mb-2">Email address</label>
              <input type="email" value={inviteEmail} onChange={(e) => setInviteEmail(e.target.value)}
                required autoFocus placeholder="colleague@example.com"
                className="w-full rounded-lg border border-border px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
            </div>
            <div>
              <label className="block text-sm font-semibold text-gray-900 mb-2">Role</label>
              <select
                value={inviteRoleId}
                onChange={(e) => setInviteRoleId(Number(e.target.value))}
                className="w-full rounded-lg border border-border bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400"
              >
                {roles.map((r) => (
                  <option key={r.id} value={r.id}>{capitalize(r.name)}</option>
                ))}
              </select>
            </div>
            <div className="flex justify-end gap-3 mt-2">
              <button type="button" onClick={() => setShowInvite(false)}
                className="rounded-lg border border-border px-4 py-2 text-sm font-medium text-gray-700 hover:bg-muted">
                Cancel
              </button>
              <button type="submit" disabled={inviteMutation.isPending || !inviteEmail.trim() || !inviteRoleId}
                className="rounded-lg bg-gray-600 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50">
                {inviteMutation.isPending ? "Sending…" : "Send Invitation"}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Change role modal */}
      {changeRoleUser && (
        <Modal onClose={() => setChangeRoleUser(null)}>
          <ModalClose onClose={() => setChangeRoleUser(null)} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">Change Role</h2>
          <p className="text-sm text-gray-500 mb-5">
            Update role for <strong>{changeRoleUser.name}</strong>.
          </p>
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-2">Role</label>
            <select
              value={selectedRoleId}
              onChange={(e) => setSelectedRoleId(Number(e.target.value))}
              className="w-full rounded-lg border border-border bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400"
            >
              {roles.map((r) => (
                <option key={r.id} value={r.id}>{capitalize(r.name)}</option>
              ))}
            </select>
          </div>
          <div className="flex justify-end gap-3 mt-6">
            <button type="button" onClick={() => setChangeRoleUser(null)}
              className="rounded-lg border border-border px-4 py-2 text-sm font-medium text-gray-700 hover:bg-muted">
              Cancel
            </button>
            <button
              onClick={() => updateRoleMutation.mutate({ id: changeRoleUser.id, roleId: selectedRoleId as number })}
              disabled={updateRoleMutation.isPending || !selectedRoleId}
              className="rounded-lg bg-gray-600 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50"
            >
              {updateRoleMutation.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}
