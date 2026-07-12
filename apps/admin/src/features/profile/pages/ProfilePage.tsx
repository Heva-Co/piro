import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { profileApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { cn } from "@/lib/utils";
import { NotificationPreferencesEditor } from "@/components/NotificationPreferencesEditor";
import { TimezonePicker } from "@/components/TimezonePicker";
import { MyOnCallCalendarCard } from "@/features/profile/components/MyOnCallCalendarCard";

const COLOR_PALETTE = [
  "#6366f1", "#8b5cf6", "#ec4899", "#ef4444",
  "#f97316", "#eab308", "#22c55e", "#14b8a6",
  "#3b82f6", "#06b6d4", "#64748b", "#78716c",
];

export default function ProfilePage() {
  const qc = useQueryClient();

  const { data: profile, isLoading } = useQuery({
    queryKey: QUERY_KEYS.MY_PROFILE,
    queryFn: profileApi.get,
  });

  // ── Profile form ─────────────────────────────────────────────────────────
  const [name, setName] = useState("");
  const [color, setColor] = useState("");
  const [timeZone, setTimeZone] = useState("");
  const [profileDirty, setProfileDirty] = useState(false);

  if (profile && !profileDirty && name === "" && color === "") {
    setName(profile.name);
    setColor(profile.color);
    setTimeZone(profile.timeZone);
  }

  const updateProfile = useMutation({
    mutationFn: profileApi.update,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MY_PROFILE });
      setProfileDirty(false);
    },
  });

  function handleSaveProfile() {
    toast.promise(updateProfile.mutateAsync({ name, color, timeZone }), {
      loading: "Saving profile…",
      success: "Profile updated.",
      error: "Failed to update profile.",
    });
  }

  if (isLoading) {
    return <div className="py-12 text-center text-muted-foreground text-sm">Loading…</div>;
  }

  return (
    <div className="max-w-2xl space-y-6">

      {/* Basic info card */}
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
              onChange={(e) => { setName(e.target.value); setProfileDirty(true); }}
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
                  onClick={() => { setColor(c); setProfileDirty(true); }}
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
              onChange={(tz) => { setTimeZone(tz); setProfileDirty(true); }}
            />
          </div>

          <div className="flex justify-end pt-1">
            <button
              onClick={handleSaveProfile}
              disabled={!profileDirty || updateProfile.isPending}
              className="rounded-lg bg-primary text-primary-foreground px-4 py-2 text-sm font-medium disabled:opacity-50 hover:bg-primary/90 transition-colors"
            >
              Save
            </button>
          </div>
        </div>
      </div>

      {/* On-call calendar card */}
      <MyOnCallCalendarCard />

      {/* Notification preferences card */}
      {profile && (
        <div className="rounded-xl border border-border bg-card shadow-sm">
          <div className="px-6 py-4 border-b border-border">
            <h2 className="text-sm font-semibold">Notification preferences</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Personal handles used when you're on-call. Tried in order — highest priority first.
            </p>
          </div>
          <div className="px-6 py-5">
            <NotificationPreferencesEditor userId={profile.id} />
          </div>
        </div>
      )}

    </div>
  );
}
