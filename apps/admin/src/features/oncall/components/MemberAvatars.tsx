import type { OnCallLayer } from "@/lib/api";

const MAX_VISIBLE = 4;

interface Props {
  layers: OnCallLayer[];
}

function MemberAvatars(props: Props) {
  const { layers } = props;

  const seen = new Set<number>();
  const users: { userId: number; userName: string; userInitials: string; userColor: string }[] = [];
  for (const layer of layers) {
    for (const u of layer.users) {
      if (!seen.has(u.userId)) {
        seen.add(u.userId);
        users.push(u);
      }
    }
  }

  if (users.length === 0) {
    return <span className="text-xs text-muted-foreground">—</span>;
  }

  const visible = users.slice(0, MAX_VISIBLE);
  const extra = users.length - MAX_VISIBLE;

  return (
    <div className="flex items-center -space-x-2">
      {visible.map((u) => (
        <div
          key={u.userId}
          title={u.userName}
          className="size-7 rounded-full border-2 border-card flex items-center justify-center text-white text-[10px] font-semibold shrink-0"
          style={{ backgroundColor: u.userColor || "#6366f1" }}
        >
          {u.userInitials}
        </div>
      ))}
      {extra > 0 && (
        <div className="size-7 rounded-full border-2 border-card bg-muted flex items-center justify-center text-[10px] font-semibold text-muted-foreground shrink-0">
          +{extra}
        </div>
      )}
    </div>
  );
}

export default MemberAvatars;
