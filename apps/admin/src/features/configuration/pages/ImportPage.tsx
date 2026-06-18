import { useState } from "react";
import { CheckCircle, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { configApi } from "@/lib/api";

export default function ImportPage() {
  const [yaml, setYaml] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setSuccess(false);
    setError("");
    try {
      await configApi.import(yaml);
      setSuccess(true);
      setYaml("");
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { title?: string } } }).response?.data?.title
          : undefined;
      setError(msg ?? "Import failed. Check your YAML and try again.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <AdminLayout title="Import Configuration">
      <div className="max-w-2xl">
        <p className="text-sm text-gray-500 mb-4">
          Paste your YAML configuration below to import services, checks, and channels.
        </p>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          {success && (
            <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
              <CheckCircle size={16} /> Configuration imported successfully.
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <AlertCircle size={16} /> {error}
            </div>
          )}

          <textarea
            value={yaml}
            onChange={(e) => setYaml(e.target.value)}
            required
            rows={20}
            placeholder="# Paste your YAML configuration here..."
            className="rounded-md border border-gray-300 bg-white px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />

          <button
            type="submit"
            disabled={loading || !yaml.trim()}
            className="self-start rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {loading ? "Importing…" : "Import"}
          </button>
        </form>
      </div>
    </AdminLayout>
  );
}
