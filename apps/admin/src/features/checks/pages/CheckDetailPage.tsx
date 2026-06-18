import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AdminLayout } from "@/components/AdminLayout";
import { StatusBadge } from "@/components/StatusBadge";
import {
  useCheck,
  useUpdateCheck,
  useDeleteCheck,
  useRunCheck,
  useCheckLogs,
  useAlertConfigs,
  useCreateAlertConfig,
  useDeleteAlertConfig,
} from "@/hooks/useChecks";
import { channelsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { ChevronDown } from "lucide-react";
import { cn, formatLatency } from "@/lib/utils";

function Accordion({
  title,
  children,
  defaultOpen = false,
}: {
  title: string;
  children: React.ReactNode;
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden mb-4">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex items-center justify-between w-full px-6 py-4 text-left hover:bg-gray-50 transition-colors"
      >
        <span className="font-semibold text-gray-900">{title}</span>
        <ChevronDown
          size={18}
          className={cn("text-gray-400 transition-transform", open ? "rotate-180" : "")}
        />
      </button>
      {open && <div className="px-6 py-5 border-t border-gray-100">{children}</div>}
    </div>
  );
}

function GeneralSettingsSection({
  serviceSlug,
  checkSlug,
}: {
  serviceSlug: string;
  checkSlug: string;
}) {
  const { data: check, isLoading } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [cron, setCron] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [initialized, setInitialized] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  if (!initialized && check) {
    setName(check.name);
    setDescription((check.config?.description as string) ?? "");
    setCron((check.config?.cron as string) ?? "");
    setIsActive(check.isActive);
    setInitialized(true);
  }

  if (isLoading) return <div className="text-sm text-gray-500">Loading...</div>;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccessMsg(null);
    try {
      await updateCheck.mutateAsync({ name, isActive, config: { cron } });
      setSuccessMsg("Saved successfully.");
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to update check.");
    }
  }

  function inputClass(extraClass = "") {
    return `border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 ${extraClass}`;
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4 max-w-lg">
      {error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
          {error}
        </div>
      )}
      {successMsg && (
        <div className="text-sm text-green-700 bg-green-50 border border-green-200 rounded-md p-3">
          {successMsg}
        </div>
      )}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
        <input
          type="text"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          className={inputClass("w-full")}
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={2}
          className={inputClass("w-full")}
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Cron Schedule</label>
        <input
          type="text"
          value={cron}
          onChange={(e) => setCron(e.target.value)}
          className={inputClass("w-56 font-mono")}
          placeholder="*/5 * * * *"
        />
      </div>
      <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
        <input
          type="checkbox"
          checked={isActive}
          onChange={(e) => setIsActive(e.target.checked)}
          className="rounded border-gray-300 text-indigo-600"
        />
        Active
      </label>
      <div className="pt-1">
        <button
          type="submit"
          disabled={updateCheck.isPending}
          className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
        >
          {updateCheck.isPending ? "Saving..." : "Save Changes"}
        </button>
      </div>
    </form>
  );
}

function ConfigurationSection({
  serviceSlug,
  checkSlug,
}: {
  serviceSlug: string;
  checkSlug: string;
}) {
  const { data: check, isLoading } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const [configJson, setConfigJson] = useState("");
  const [initialized, setInitialized] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  if (!initialized && check) {
    setConfigJson(
      JSON.stringify(check.config?.typeDataJson ? JSON.parse(check.config.typeDataJson as string) : check.config, null, 2)
    );
    setInitialized(true);
  }

  if (isLoading) return <div className="text-sm text-gray-500">Loading...</div>;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccessMsg(null);
    try {
      const parsed = JSON.parse(configJson);
      await updateCheck.mutateAsync({
        config: { ...(check?.config ?? {}), typeDataJson: JSON.stringify(parsed) },
      });
      setSuccessMsg("Configuration saved.");
    } catch (err: unknown) {
      if (err instanceof SyntaxError) {
        setError("Invalid JSON: " + err.message);
      } else {
        setError(err instanceof Error ? err.message : "Failed to update configuration.");
      }
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
          {error}
        </div>
      )}
      {successMsg && (
        <div className="text-sm text-green-700 bg-green-50 border border-green-200 rounded-md p-3">
          {successMsg}
        </div>
      )}
      <p className="text-sm text-gray-500">Edit the type-specific configuration as JSON.</p>
      <textarea
        value={configJson}
        onChange={(e) => setConfigJson(e.target.value)}
        rows={12}
        className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
      />
      <button
        type="submit"
        disabled={updateCheck.isPending}
        className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
      >
        {updateCheck.isPending ? "Saving..." : "Save Configuration"}
      </button>
    </form>
  );
}

function RecentLogsSection({
  serviceSlug,
  checkSlug,
}: {
  serviceSlug: string;
  checkSlug: string;
}) {
  const { data: logs, isLoading, refetch } = useCheckLogs(serviceSlug, checkSlug);
  const runCheck = useRunCheck(serviceSlug, checkSlug);
  const [runMsg, setRunMsg] = useState<string | null>(null);

  async function handleRun() {
    setRunMsg(null);
    try {
      await runCheck.mutateAsync();
      setRunMsg("Check triggered. Logs will update shortly.");
      await refetch();
    } catch {
      setRunMsg("Failed to run check.");
    }
  }

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <p className="text-sm text-gray-500">Recent check results.</p>
        <button
          onClick={handleRun}
          disabled={runCheck.isPending}
          className="px-3 py-1.5 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
        >
          {runCheck.isPending ? "Running..." : "Run Now"}
        </button>
      </div>
      {runMsg && <div className="mb-3 text-sm text-gray-600">{runMsg}</div>}
      {isLoading ? (
        <div className="text-sm text-gray-500">Loading...</div>
      ) : !logs || logs.length === 0 ? (
        <div className="text-sm text-gray-500">No logs yet.</div>
      ) : (
        <table className="min-w-full divide-y divide-gray-100 text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-2 text-left font-medium text-gray-500">Time</th>
              <th className="px-4 py-2 text-left font-medium text-gray-500">Status</th>
              <th className="px-4 py-2 text-left font-medium text-gray-500">Latency</th>
              <th className="px-4 py-2 text-left font-medium text-gray-500">Message</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {logs.map((log) => (
              <tr key={log.id} className="hover:bg-gray-50">
                <td className="px-4 py-2 text-gray-500 text-xs">
                  {new Date(log.checkedAt).toLocaleString()}
                </td>
                <td className="px-4 py-2">
                  <StatusBadge status={log.status} />
                </td>
                <td className="px-4 py-2 text-gray-500">
                  {formatLatency(log.latencyMs)}
                </td>
                <td className="px-4 py-2 text-gray-500 text-xs">{log.message ?? ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function AlertConfigsSection({
  serviceSlug,
  checkSlug,
}: {
  serviceSlug: string;
  checkSlug: string;
}) {
  const { data: alertConfigs, isLoading } = useAlertConfigs(serviceSlug, checkSlug);
  const { data: channels } = useQuery({
    queryKey: QUERY_KEYS.CHANNELS,
    queryFn: channelsApi.list,
  });
  const createAlertConfig = useCreateAlertConfig(serviceSlug, checkSlug);
  const deleteAlertConfig = useDeleteAlertConfig(serviceSlug, checkSlug);

  const [channelId, setChannelId] = useState<number | "">("");
  const [onDown, setOnDown] = useState(true);
  const [onRecovery, setOnRecovery] = useState(true);
  const [addError, setAddError] = useState<string | null>(null);

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    if (!channelId) return;
    setAddError(null);
    try {
      await createAlertConfig.mutateAsync({
        channelId: channelId as number,
        onDown,
        onRecovery,
      });
      setChannelId("");
    } catch (err: unknown) {
      setAddError(err instanceof Error ? err.message : "Failed to add alert config.");
    }
  }

  function channelName(id: number) {
    return channels?.find((c) => c.id === id)?.name ?? String(id);
  }

  return (
    <div className="space-y-5">
      {isLoading ? (
        <div className="text-sm text-gray-500">Loading...</div>
      ) : !alertConfigs || alertConfigs.length === 0 ? (
        <div className="text-sm text-gray-500">No alert configurations yet.</div>
      ) : (
        <table className="min-w-full divide-y divide-gray-100 text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-2 text-left font-medium text-gray-500">Channel</th>
              <th className="px-4 py-2 text-left font-medium text-gray-500">On Down</th>
              <th className="px-4 py-2 text-left font-medium text-gray-500">On Recovery</th>
              <th className="px-4 py-2 text-left font-medium text-gray-500">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {alertConfigs.map((ac) => (
              <tr key={ac.id} className="hover:bg-gray-50">
                <td className="px-4 py-2 text-gray-900">{channelName(ac.channelId)}</td>
                <td className="px-4 py-2 text-gray-500">{ac.onDown ? "Yes" : "No"}</td>
                <td className="px-4 py-2 text-gray-500">{ac.onRecovery ? "Yes" : "No"}</td>
                <td className="px-4 py-2">
                  <button
                    onClick={() => deleteAlertConfig.mutate(ac.id)}
                    className="text-red-600 hover:text-red-800 text-sm font-medium"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="border-t border-gray-100 pt-4">
        <h4 className="text-sm font-medium text-gray-700 mb-3">Add Alert Configuration</h4>
        {addError && (
          <div className="mb-3 text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
            {addError}
          </div>
        )}
        <form onSubmit={handleAdd} className="flex flex-wrap items-end gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Channel</label>
            <select
              required
              value={channelId}
              onChange={(e) => setChannelId(Number(e.target.value))}
              className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="">Select channel</option>
              {channels?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer pb-2">
            <input
              type="checkbox"
              checked={onDown}
              onChange={(e) => setOnDown(e.target.checked)}
              className="rounded border-gray-300 text-indigo-600"
            />
            On Down
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer pb-2">
            <input
              type="checkbox"
              checked={onRecovery}
              onChange={(e) => setOnRecovery(e.target.checked)}
              className="rounded border-gray-300 text-indigo-600"
            />
            On Recovery
          </label>
          <button
            type="submit"
            disabled={createAlertConfig.isPending}
            className="px-3 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors mb-0"
          >
            {createAlertConfig.isPending ? "Adding..." : "Add"}
          </button>
        </form>
      </div>
    </div>
  );
}

export default function CheckDetailPage() {
  const { slug: serviceSlug, checkSlug } = useParams<{
    slug: string;
    checkSlug: string;
  }>();
  const navigate = useNavigate();
  const { data: check, isLoading } = useCheck(serviceSlug!, checkSlug!);
  const deleteCheck = useDeleteCheck(serviceSlug!, checkSlug!);
  const [deleteConfirm, setDeleteConfirm] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);

  async function handleDelete() {
    if (deleteConfirm !== checkSlug) return;
    setDeleteError(null);
    try {
      await deleteCheck.mutateAsync();
      navigate(ROUTES.SERVICES.DETAIL(serviceSlug!));
    } catch (err: unknown) {
      setDeleteError(err instanceof Error ? err.message : "Failed to delete check.");
    }
  }

  if (isLoading) {
    return (
      <AdminLayout title="Check">
        <div className="text-sm text-gray-500">Loading...</div>
      </AdminLayout>
    );
  }

  if (!check) {
    return (
      <AdminLayout title="Check">
        <div className="text-sm text-red-500">Check not found.</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout title={check.name}>
      <div className="flex items-center gap-3 mb-6">
        <span className="text-sm text-gray-500 font-mono">{check.slug}</span>
        <StatusBadge status={check.status} />
        <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded">{check.type}</span>
      </div>

      <Accordion title="General Settings" defaultOpen>
        <GeneralSettingsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </Accordion>

      <Accordion title="Configuration" defaultOpen>
        <ConfigurationSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </Accordion>

      <Accordion title="Recent Logs" defaultOpen>
        <RecentLogsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </Accordion>

      <Accordion title="Alert Configurations">
        <AlertConfigsSection serviceSlug={serviceSlug!} checkSlug={checkSlug!} />
      </Accordion>

      <Accordion title="Danger Zone">
        <div className="space-y-3">
          <p className="text-sm text-gray-600">
            Permanently delete this check. Type{" "}
            <span className="font-mono font-semibold">{checkSlug}</span> to confirm.
          </p>
          {deleteError && (
            <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
              {deleteError}
            </div>
          )}
          <input
            type="text"
            placeholder={checkSlug}
            value={deleteConfirm}
            onChange={(e) => setDeleteConfirm(e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm w-64 focus:outline-none focus:ring-2 focus:ring-red-500"
          />
          <div>
            <button
              onClick={handleDelete}
              disabled={deleteConfirm !== checkSlug || deleteCheck.isPending}
              className="px-4 py-2 bg-red-600 text-white rounded-md text-sm font-medium hover:bg-red-700 disabled:opacity-40 transition-colors"
            >
              {deleteCheck.isPending ? "Deleting..." : "Delete Check"}
            </button>
          </div>
        </div>
      </Accordion>
    </AdminLayout>
  );
}
