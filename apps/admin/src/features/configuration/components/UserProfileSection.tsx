import { User as UserIcon } from "lucide-react";
import { capitalize } from "@/lib/utils";
import type { User } from "@/lib/api";

interface Props {
  user: User;
}

export default function UserProfileSection({ user }: Props) {
  const initials = user.name
    ? user.name.split(" ").map((n) => n[0]).slice(0, 2).join("").toUpperCase()
    : null;

  return (
    <div className="flex items-center gap-4">
      <div className="size-12 rounded-full bg-muted flex items-center justify-center text-sm font-semibold shrink-0">
        {initials ?? <UserIcon size={20} />}
      </div>
      <div>
        <div className="text-sm font-medium">
          {user.name || <span className="text-muted-foreground italic">No name</span>}
        </div>
        <div className="text-xs text-muted-foreground">{user.email}</div>
        <div className="flex gap-1 mt-1">
          {user.roles?.map((r) => (
            <span key={r} className="inline-block rounded px-1.5 py-0.5 text-xs bg-muted text-muted-foreground">
              {capitalize(r)}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
