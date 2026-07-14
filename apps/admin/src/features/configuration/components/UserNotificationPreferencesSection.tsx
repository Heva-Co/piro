import { NotificationPreferencesEditor } from "@/components/NotificationPreferencesEditor";

interface Props {
  userId: number;
}

export default function UserNotificationPreferencesSection({ userId }: Props) {
  return <NotificationPreferencesEditor userId={userId} />;
}
