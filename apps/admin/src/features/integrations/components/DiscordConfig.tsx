export function DiscordConfig() {
  return (
    <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2.5 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-300">
      In Discord, webhooks are created per channel — not per server. Configure the Webhook URL when creating a{" "}
      <span className="font-medium">Notification Channel</span> for Discord. To get a URL: open the channel, go to{" "}
      <span className="font-medium">Settings → Integrations → Webhooks</span>.
    </div>
  );
}
