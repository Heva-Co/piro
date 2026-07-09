export function GoogleChatConfig() {
  return (
    <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2.5 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-300">
      Google Chat uses incoming webhooks configured per space. Set the Webhook URL when creating a{" "}
      <span className="font-medium">Notification Channel</span>. To get a URL: open the space, go to{" "}
      <span className="font-medium">Apps &amp; Integrations → Webhooks → Add Webhook</span>.
    </div>
  );
}
