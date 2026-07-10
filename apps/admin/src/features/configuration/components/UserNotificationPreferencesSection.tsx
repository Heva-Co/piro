import { NotificationPreferencesEditor } from "@/components/NotificationPreferencesEditor";

interface Props {
  userId: number;
}

export default function UserNotificationPreferencesSection({ userId }: Props) {
  return (
    <div className="rounded-xl border bg-card p-6">
      <NotificationPreferencesEditor userId={userId} />
    </div>
  );
}
