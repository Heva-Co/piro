import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { profileApi } from "@/lib/actions/profile";
import { QUERY_KEYS } from "@/constants/api";
import { cn } from "@/lib/utils";
import { TimezonePicker } from "@/components/TimezonePicker";

const COLOR_PALETTE = [
  "#6366f1", "#8b5cf6", "#ec4899", "#ef4444",
  "#f97316", "#eab308", "#22c55e", "#14b8a6",
  "#3b82f6", "#06b6d4", "#64748b", "#78716c",
];

/** Profile tab: display name, avatar color, and time zone. */
function BasicInfoSection() {
  const qc = useQueryClient();

  const { data: profile } = useQuery({
    queryKey: QUERY_KEYS.MY_PROFILE,
    queryFn: profileApi.get,
  });

  const [name, setName] = useState("");
  const [color, setColor] = useState("");
  const [timeZone, setTimeZone] = useState("");
  const [dirty, setDirty] = useState(false);

  if (profile && !dirty && name === "" && color === "") {
    setName(profile.name);
    setColor(profile.color);
    setTimeZone(profile.timeZone);
  }

  const updateProfile = useMutation({
    mutationFn: profileApi.update,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MY_PROFILE });
      setDirty(false);
    },
  });

  function handleSave() {
    toast.promise(updateProfile.mutateAsync({ name, color, timeZone }), {
      loading: "Saving profile…",
      success: "Profile updated.",
      error: "Failed to update profile.",
    });
  }

  return (
    <div className="rounded-xl border border-border bg-card shadow-sm">
      <div className="px-6 py-4 border-b border-border">
        <h2 className="text-sm font-semibold">Basic information</h2>
        <p className="text-xs text-muted-foreground mt-0.5">Your display name and avatar color.</p>
      </div>
      <div className="px-6 py-5 space-y-5">
        <div className="flex items-center gap-4">
          <div
            className="size-12 rounded-full flex items-center justify-center text-white text-lg font-semibold shrink-0"
            style={{ backgroundColor: color || profile?.color }}
          >
            {name ? name.split(" ").map((n) => n[0]).slice(0, 2).join("").toUpperCase() : "?"}
          </div>
          <div>
            <div className="text-sm font-medium">{name || profile?.name}</div>
            <div className="text-xs text-muted-foreground">{profile?.email}</div>
            {profile?.roles.map((r) => (
              <span key={r} className="inline-block mt-1 mr-1 rounded px-1.5 py-0.5 text-xs bg-muted text-muted-foreground">{r}</span>
            ))}
          </div>
        </div>

        <div className="space-y-1.5">
          <label className="text-sm font-medium">Display name</label>
          <input
            value={name}
            onChange={(e) => { setName(e.target.value); setDirty(true); }}
            className="w-full rounded-lg border border-input bg-transparent px-3 py-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
          />
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">Avatar color</label>
          <div className="flex flex-wrap gap-2">
            {COLOR_PALETTE.map((c) => (
              <button
                key={c}
                type="button"
                onClick={() => { setColor(c); setDirty(true); }}
                className={cn(
                  "size-7 rounded-full border-2 transition-all",
                  color === c ? "border-foreground scale-110" : "border-transparent hover:scale-105"
                )}
                style={{ backgroundColor: c }}
              />
            ))}
          </div>
        </div>

        <div className="space-y-1.5">
          <label className="text-sm font-medium">Time zone</label>
          <TimezonePicker
            value={timeZone}
            onChange={(tz) => { setTimeZone(tz); setDirty(true); }}
          />
        </div>

        <div className="flex justify-end pt-1">
          <button
            onClick={handleSave}
            disabled={!dirty || updateProfile.isPending}
            className="rounded-lg bg-primary text-primary-foreground px-4 py-2 text-sm font-medium disabled:opacity-50 hover:bg-primary/90 transition-colors"
          >
            Save
          </button>
        </div>
      </div>
    </div>
  );
}

export default BasicInfoSection;
