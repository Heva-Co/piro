export function SlackConfig() {
  return (
    <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2.5 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-300">
      Slack uses Incoming Webhooks — one URL per channel. Configure the Webhook URL when creating a{" "}
      <span className="font-medium">Notification Channel</span>. To get a URL: go to your Slack App →{" "}
      <span className="font-medium">Incoming Webhooks → Add New Webhook to Workspace</span>.
    </div>
  );
}
