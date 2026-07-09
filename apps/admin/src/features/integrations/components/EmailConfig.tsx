export function EmailConfig() {
  return (
    <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2.5 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-300">
      Email uses the SMTP server configured at the system level. No additional credentials are needed here. Configure recipients when creating a{" "}
      <span className="font-medium">Notification Channel</span>.
    </div>
  );
}
