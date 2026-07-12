import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "react-toastify";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usersApi, type User } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { capitalize } from "@/lib/utils";

interface Props {
  user: User;
  roles: { id: number; name: string }[];
}

export default function UserRoleSection({ user, roles }: Props) {
  const qc = useQueryClient();

  const currentRole = roles.find((r) => r.name.toLowerCase() === user.roles?.[0]?.toLowerCase());
  const [selectedRoleId, setSelectedRoleId] = useState<number | "">(currentRole?.id ?? "");

  const updateRoleMutation = useMutation({
    mutationFn: (roleId: number) => usersApi.updateRole(user.id, roleId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [...QUERY_KEYS.USERS, user.id] });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.USERS });
      toast.success("Role updated.");
    },
    onError: () => toast.error("Failed to update role."),
  });

  return (
    <div className="rounded-xl border bg-card p-6 flex items-end gap-3">
      <div className="space-y-1.5 flex-1 max-w-xs">
        <label className="text-sm font-medium">Role</label>
        <Select value={String(selectedRoleId)} onValueChange={(v) => v && setSelectedRoleId(Number(v))}>
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
      <Button
        onClick={() => updateRoleMutation.mutate(selectedRoleId as number)}
        disabled={updateRoleMutation.isPending || !selectedRoleId}
      >
        {updateRoleMutation.isPending ? "Saving…" : "Save"}
      </Button>
    </div>
  );
}
