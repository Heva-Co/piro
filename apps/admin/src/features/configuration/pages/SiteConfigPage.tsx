import { useState, useEffect, useRef } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Upload, CheckCircle, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { siteApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export default function SiteConfigPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.SITE_CONFIG,
    queryFn: siteApi.get,
  });

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [url, setUrl] = useState("");
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (data) {
      setTitle(data.title ?? "");
      setDescription(data.description ?? "");
      setUrl(data.url ?? "");
    }
  }, [data]);

  const updateMutation = useMutation({
    mutationFn: () => siteApi.update({ title, description, url }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.SITE_CONFIG });
      setSuccess(true);
      setError("");
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: () => setError("Failed to save site configuration."),
  });

  const logoInputRef = useRef<HTMLInputElement>(null);
  const faviconInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState<"logo" | "favicon" | null>(null);
  const [uploadError, setUploadError] = useState("");

  async function handleUpload(type: "logo" | "favicon", file: File) {
    setUploading(type);
    setUploadError("");
    try {
      await siteApi.upload(type, file);
      await qc.invalidateQueries({ queryKey: QUERY_KEYS.SITE_CONFIG });
    } catch {
      setUploadError(`Failed to upload ${type}.`);
    } finally {
      setUploading(null);
    }
  }

  return (
    <AdminLayout title="Site Configuration">
      <div className="max-w-xl">
        {isLoading ? (
          <p className="text-gray-400">Loading…</p>
        ) : (
          <form
            onSubmit={(e) => { e.preventDefault(); updateMutation.mutate(); }}
            className="flex flex-col gap-5"
          >
            {success && (
              <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
                <CheckCircle size={16} /> Saved successfully.
              </div>
            )}
            {error && (
              <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                <AlertCircle size={16} /> {error}
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Site Name / Title</label>
              <input
                type="text"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Description</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={3}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Site URL</label>
              <input
                type="url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://status.example.com"
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            {/* Logo / Favicon uploads */}
            <div className="flex gap-4">
              <div className="flex flex-col gap-1.5 flex-1">
                <label className="text-sm font-medium">Logo</label>
                {data?.logoUrl && (
                  <img src={data.logoUrl} alt="logo" className="h-12 object-contain rounded border" />
                )}
                <input
                  ref={logoInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) handleUpload("logo", f);
                  }}
                />
                <button
                  type="button"
                  onClick={() => logoInputRef.current?.click()}
                  disabled={uploading === "logo"}
                  className="flex items-center gap-2 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm hover:bg-gray-50 disabled:opacity-50"
                >
                  <Upload size={14} />
                  {uploading === "logo" ? "Uploading…" : "Upload Logo"}
                </button>
              </div>

              <div className="flex flex-col gap-1.5 flex-1">
                <label className="text-sm font-medium">Favicon</label>
                {data?.faviconUrl && (
                  <img src={data.faviconUrl} alt="favicon" className="h-12 w-12 object-contain rounded border" />
                )}
                <input
                  ref={faviconInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) handleUpload("favicon", f);
                  }}
                />
                <button
                  type="button"
                  onClick={() => faviconInputRef.current?.click()}
                  disabled={uploading === "favicon"}
                  className="flex items-center gap-2 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm hover:bg-gray-50 disabled:opacity-50"
                >
                  <Upload size={14} />
                  {uploading === "favicon" ? "Uploading…" : "Upload Favicon"}
                </button>
              </div>
            </div>

            {uploadError && (
              <p className="text-sm text-red-600">{uploadError}</p>
            )}

            <button
              type="submit"
              disabled={updateMutation.isPending}
              className="self-start rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {updateMutation.isPending ? "Saving…" : "Save Changes"}
            </button>
          </form>
        )}
      </div>
    </AdminLayout>
  );
}
