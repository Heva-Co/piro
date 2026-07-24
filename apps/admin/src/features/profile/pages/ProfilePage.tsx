import { useQuery } from "@tanstack/react-query";
import { profileApi } from "@/lib/actions/profile";
import { QUERY_KEYS } from "@/constants/api";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { NotificationPreferencesEditor } from "@/components/NotificationPreferencesEditor";
import { MyOnCallCalendarCard } from "@/features/profile/components/MyOnCallCalendarCard";
import { ChangePasswordSection } from "@/features/profile/components/ChangePasswordSection";
import BasicInfoSection from "@/features/profile/components/BasicInfoSection";

function ProfilePage() {
  const { data: profile, isLoading } = useQuery({
    queryKey: QUERY_KEYS.MY_PROFILE,
    queryFn: profileApi.get,
  });

  if (isLoading) {
    return <div className="py-12 text-center text-muted-foreground text-sm">Loading…</div>;
  }

  // Password tab only for local accounts — an OIDC user has no password to change.
  const showPassword = profile && !profile.isOidc;

  return (
    <div className="max-w-2xl">
      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">Profile</TabsTrigger>
          {showPassword && <TabsTrigger value="password">Password</TabsTrigger>}
          <TabsTrigger value="oncall">On-call</TabsTrigger>
          <TabsTrigger value="notifications">Notifications</TabsTrigger>
        </TabsList>

        <TabsContent value="profile" className="mt-4">
          <BasicInfoSection />
        </TabsContent>

        {showPassword && (
          <TabsContent value="password" className="mt-4">
            <ChangePasswordSection />
          </TabsContent>
        )}

        <TabsContent value="oncall" className="mt-4">
          <MyOnCallCalendarCard />
        </TabsContent>

        <TabsContent value="notifications" className="mt-4">
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
        </TabsContent>
      </Tabs>
    </div>
  );
}

export default ProfilePage;
