import { useState, useEffect, useRef } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Upload, Save } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { siteApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { INCIDENT_CORRELATION_MODE_MAP, type IncidentCorrelationModeKey } from "@/constants/incidents";

const DEFAULT_ASSET = "/piro.svg";

export default function SiteConfigPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.SITE_CONFIG,
    queryFn: siteApi.get,
  });

  const [name, setName] = useState("");
  const [url, setUrl] = useState("");
  const [metaTitle, setMetaTitle] = useState("");
  const [metaDescription, setMetaDescription] = useState("");

  const [infoSaving, setInfoSaving] = useState(false);
  const [infoSuccess, setInfoSuccess] = useState(false);
  const [seoSaving, setSeoSaving] = useState(false);
  const [seoSuccess, setSeoSuccess] = useState(false);

  const [uploading, setUploading] = useState<"logo" | "favicon" | "og-image" | null>(null);
  const [uploadError, setUploadError] = useState("");

  const logoRef = useRef<HTMLInputElement>(null);
  const faviconRef = useRef<HTMLInputElement>(null);
  const ogRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!data) return;
    setName(data.name ?? "");
    setUrl(data.url ?? "");
    setMetaTitle(data.metaTitle ?? "");
    setMetaDescription(data.metaDescription ?? "");
  }, [data]);

  const infoMutation = useMutation({
    mutationFn: () => siteApi.update({ name, url }),
    onMutate: () => setInfoSaving(true),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.SITE_CONFIG });
      setInfoSuccess(true);
      setTimeout(() => setInfoSuccess(false), 3000);
    },
    onSettled: () => setInfoSaving(false),
  });

  const seoMutation = useMutation({
    mutationFn: () => siteApi.update({ metaTitle, metaDescription }),
    onMutate: () => setSeoSaving(true),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.SITE_CONFIG });
      setSeoSuccess(true);
      setTimeout(() => setSeoSuccess(false), 3000);
    },
    onSettled: () => setSeoSaving(false),
  });

  async function handleUpload(type: "logo" | "favicon" | "og-image", file: File) {
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

  if (isLoading) {
    return (
      <AdminLayout title="Site">
        <div className="text-sm text-muted-foreground">Loading…</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout title="Site">
      <div className="max-w-4xl space-y-4">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Site</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Customize your status page identity and SEO settings.
          </p>
        </div>

        {/* ── Site Information ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">Site Information</h2>
            <p className="text-xs text-muted-foreground mt-0.5">Basic information about your status page</p>
          </div>

          <div className="grid grid-cols-2 gap-4 mb-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Site Name</label>
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Acme Status"
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">The name displayed in the header and browser tab</p>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Site URL</label>
              <input
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://status.example.com"
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">Used in email notifications and links</p>
            </div>
          </div>

          <div className="flex justify-end">
            <button
              onClick={() => infoMutation.mutate()}
              disabled={infoSaving}
              className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              <Save size={14} />
              {infoSuccess ? "Saved!" : infoSaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>

        {/* ── Logo ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">Logo</h2>
            <p className="text-xs text-muted-foreground mt-0.5">Upload your site logo (max 512 KB, PNG/JPG/SVG/WebP)</p>
          </div>
          <div className="flex items-center gap-4">
            <img
              src={data?.logoUrl ?? DEFAULT_ASSET}
              alt="logo"
              className="h-10 w-10 object-contain rounded"
            />
            <div className="flex flex-col gap-1">
              <p className="text-xs text-muted-foreground">
                {data?.logoUrl ? "Custom logo" : "Using default logo"}
              </p>
              <input
                ref={logoRef}
                type="file"
                accept="image/png,image/jpeg,image/svg+xml,image/webp"
                className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) handleUpload("logo", f); }}
              />
              <button
                type="button"
                onClick={() => logoRef.current?.click()}
                disabled={uploading === "logo"}
                className="flex items-center gap-2 rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors w-fit"
              >
                <Upload size={14} />
                {uploading === "logo" ? "Uploading…" : "Upload Logo"}
              </button>
            </div>
          </div>
          {uploadError && uploading === null && (
            <p className="text-xs text-destructive mt-2">{uploadError}</p>
          )}
        </div>

        {/* ── Favicon ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">Favicon</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Upload your site favicon (max 512 KB, PNG/JPG/SVG/WebP, recommended 64×64px)
            </p>
          </div>
          <div className="flex items-center gap-4">
            <img
              src={data?.faviconUrl ?? DEFAULT_ASSET}
              alt="favicon"
              className="h-10 w-10 object-contain rounded"
            />
            <div className="flex flex-col gap-1">
              <p className="text-xs text-muted-foreground">
                {data?.faviconUrl ? "Custom favicon" : "Using default favicon"}
              </p>
              <input
                ref={faviconRef}
                type="file"
                accept="image/png,image/jpeg,image/svg+xml,image/webp"
                className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) handleUpload("favicon", f); }}
              />
              <button
                type="button"
                onClick={() => faviconRef.current?.click()}
                disabled={uploading === "favicon"}
                className="flex items-center gap-2 rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors w-fit"
              >
                <Upload size={14} />
                {uploading === "favicon" ? "Uploading…" : "Upload Favicon"}
              </button>
            </div>
          </div>
        </div>

        {/* ── Social Preview & SEO ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">Social Preview &amp; SEO</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Configure the social preview image and meta tags for search engines
            </p>
          </div>

          {/* OG Image */}
          <div className="flex items-start gap-4 mb-5">
            <div className="h-24 w-44 rounded-lg border bg-muted flex items-center justify-center shrink-0 overflow-hidden">
              {data?.ogImageUrl ? (
                <img src={data.ogImageUrl} alt="og" className="h-full w-full object-cover" />
              ) : (
                <Upload size={20} className="text-muted-foreground" />
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <input
                ref={ogRef}
                type="file"
                accept="image/png,image/jpeg,image/svg+xml,image/webp"
                className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) handleUpload("og-image", f); }}
              />
              <button
                type="button"
                onClick={() => ogRef.current?.click()}
                disabled={uploading === "og-image"}
                className="flex items-center gap-2 rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors w-fit"
              >
                <Upload size={14} />
                {uploading === "og-image" ? "Uploading…" : "Upload Social Preview"}
              </button>
              <p className="text-xs text-muted-foreground">
                Optional. Leave empty to use no social preview image. (max 2 MB)
              </p>
            </div>
          </div>

          <div className="flex flex-col gap-4 mb-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Meta Title</label>
              <input
                value={metaTitle}
                onChange={(e) => setMetaTitle(e.target.value)}
                placeholder={name || "Acme Status"}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">Overrides the default page title in search results</p>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Meta Description</label>
              <textarea
                value={metaDescription}
                onChange={(e) => setMetaDescription(e.target.value)}
                placeholder={`Real-time status and uptime for ${name || "Acme"} services.`}
                rows={3}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring resize-none"
              />
              <p className="text-xs text-muted-foreground">Shown as the snippet text in search engine results</p>
            </div>
          </div>

          <div className="flex justify-end">
            <button
              onClick={() => seoMutation.mutate()}
              disabled={seoSaving}
              className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              <Save size={14} />
              {seoSuccess ? "Saved!" : seoSaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
}
