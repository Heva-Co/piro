export function WebhookConfig() {
  return (
    <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2.5 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-300">
      Webhook URLs are configured per{" "}
      <span className="font-medium">Notification Channel</span>, not globally. Create a channel of
      type Webhook and provide the URL there.
    </div>
  );
}
