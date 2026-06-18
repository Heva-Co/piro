import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { ChevronDown, ChevronUp, Save } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { useService, useUpdateService, useDeleteService } from "@/hooks/useServices";
import { useChecks } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";
import { cn } from "@/lib/utils";

// ── Toggle ───────────────────────────────────────────────────────────────────

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${
        checked ? "bg-foreground" : "bg-input"
      }`}
    >
      <span
        className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-background shadow-lg ring-0 transition-transform ${
          checked ? "translate-x-5" : "translate-x-0"
        }`}
      />
    </button>
  );
}

// ── Accordion ────────────────────────────────────────────────────────────────

function Accordion({
  title,
  children,
  defaultOpen = false,
  titleClassName,
}: {
  title: React.ReactNode;
  children: React.ReactNode;
  defaultOpen?: boolean;
  titleClassName?: string;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="border-b border-border">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex items-center justify-between w-full py-4 text-left"
      >
        <span className={cn("text-sm font-semibold", titleClassName)}>
          {title}
        </span>
        {open ? (
          <ChevronUp size={16} className="text-muted-foreground" />
        ) : (
          <ChevronDown size={16} className="text-muted-foreground" />
        )}
      </button>
      {open && <div className="pb-6">{children}</div>}
    </div>
  );
}

// ── General Settings form ────────────────────────────────────────────────────

function GeneralSettingsForm({ slug }: { slug: string }) {
  const { data: service } = useService(slug);
  const updateService = useUpdateService(slug);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [displayOrder, setDisplayOrder] = useState(0);
  const [isHidden, setIsHidden] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!service) return;
    setName(service.name);
    setDescription(service.description ?? "");
    setDisplayOrder(service.displayOrder);
    setIsHidden(service.isHidden);
  }, [service]);

  async function handleSave() {
    setError("");
    try {
      await updateService.mutateAsync({ name, description: description || undefined, displayOrder, isHidden });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save changes.");
    }
  }

  return (
    <div className="rounded-xl border bg-card p-6 flex flex-col gap-5">
      {error && (
        <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {/* Name + Slug */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">
            Name <span className="text-destructive">*</span>
          </label>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Slug</label>
          <input
            value={slug}
            readOnly
            className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none"
          />
          <p className="text-xs text-muted-foreground">Cannot be changed after creation</p>
        </div>
      </div>

      {/* Description */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Description</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={3}
          placeholder="A brief description of this service"
          className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring resize-none"
        />
      </div>

      {/* Display Order + Hidden toggle */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Display Order</label>
          <input
            type="number"
            value={displayOrder}
            onChange={(e) => setDisplayOrder(Number(e.target.value))}
            className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
          <p className="text-xs text-muted-foreground">Lower numbers appear first</p>
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-sm font-semibold">Hidden from Public Page</label>
          <div className="flex items-center gap-2.5">
            <Toggle checked={isHidden} onChange={setIsHidden} />
            <span className="text-sm">{isHidden ? "Hidden" : "Visible"}</span>
          </div>
          <p className="text-xs text-muted-foreground">Hidden services won't appear on the status page</p>
        </div>
      </div>

      {/* Save */}
      <div className="flex justify-end">
        <button
          type="button"
          onClick={handleSave}
          disabled={updateService.isPending}
          className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
        >
          <Save size={14} />
          {saved ? "Saved!" : updateService.isPending ? "Saving…" : "Save changes"}
        </button>
      </div>
    </div>
  );
}

// ── Checks section ───────────────────────────────────────────────────────────

function ChecksSection({ slug }: { slug: string }) {
  const navigate = useNavigate();
  const { data: checks, isLoading } = useChecks(slug);

  return (
    <div className="rounded-xl border bg-card overflow-hidden">
      <div className="flex items-center justify-between px-5 py-3 border-b">
        <p className="text-sm text-muted-foreground">Checks configured for this service.</p>
        <button
          onClick={() => navigate(`/admin/services/${slug}/checks/new`)}
          className="rounded-lg bg-foreground text-background px-3 py-1.5 text-sm font-medium hover:opacity-90 transition-opacity"
        >
          + Add Check
        </button>
      </div>
      {isLoading ? (
        <div className="px-5 py-6 text-sm text-muted-foreground">Loading…</div>
      ) : !checks || checks.length === 0 ? (
        <div className="px-5 py-8 text-sm text-muted-foreground text-center">No checks configured yet.</div>
      ) : (
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b bg-muted/40">
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Name</th>
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Type</th>
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
              <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Active</th>
              <th className="px-5 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y">
            {checks.map((check) => (
              <tr key={check.slug} className="hover:bg-muted/30 transition-colors">
                <td className="px-5 py-3 font-medium">{check.name}</td>
                <td className="px-5 py-3 text-muted-foreground">{check.type}</td>
                <td className="px-5 py-3">
                  <span className="rounded-full bg-foreground text-background px-2.5 py-0.5 text-xs font-semibold uppercase">
                    {check.currentStatus}
                  </span>
                </td>
                <td className="px-5 py-3 text-muted-foreground">{check.isActive ? "Yes" : "No"}</td>
                <td className="px-5 py-3 text-right">
                  <button
                    onClick={() => navigate(ROUTES.CHECKS.DETAIL(slug, check.slug))}
                    className="rounded-lg border px-3 py-1 text-sm font-medium hover:bg-muted transition-colors"
                  >
                    Configure
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

// ── Danger Zone ──────────────────────────────────────────────────────────────

function DangerZone({ slug }: { slug: string }) {
  const navigate = useNavigate();
  const deleteService = useDeleteService(slug);
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");

  async function handleDelete() {
    if (confirm !== slug) return;
    setError("");
    try {
      await deleteService.mutateAsync();
      navigate(ROUTES.SERVICES.LIST);
    } catch {
      setError("Failed to delete service.");
    }
  }

  return (
    <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 flex flex-col gap-4">
      <p className="text-sm">
        Permanently delete this service and all its checks. Type{" "}
        <code className="font-mono font-semibold">{slug}</code> to confirm.
      </p>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <div className="flex items-center gap-3">
        <input
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          placeholder={slug}
          className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-destructive w-64"
        />
        <button
          onClick={handleDelete}
          disabled={confirm !== slug || deleteService.isPending}
          className="rounded-lg bg-destructive text-destructive-foreground px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-40 transition-opacity"
        >
          {deleteService.isPending ? "Deleting…" : "Delete Service"}
        </button>
      </div>
    </div>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function ServiceDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { data: service, isLoading } = useService(slug!);
  const { data: checks } = useChecks(slug!);

  if (isLoading) {
    return (
      <AdminLayout title="Service">
        <div className="text-sm text-muted-foreground">Loading…</div>
      </AdminLayout>
    );
  }

  if (!service) {
    return (
      <AdminLayout title="Service">
        <div className="text-sm text-destructive">Service not found.</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout title={service.name}>
      <div>
        {/* Breadcrumb + actions */}
        <div className="flex items-center justify-between mb-6">
          <nav className="flex items-center gap-2 text-sm text-muted-foreground">
            <button
              type="button"
              onClick={() => navigate(ROUTES.SERVICES.LIST)}
              className="hover:text-foreground transition-colors"
            >
              Services
            </button>
            <span>/</span>
            <span className="text-foreground font-medium">{service.name}</span>
          </nav>
          <div className="flex items-center gap-2">
            <span className="rounded-lg border px-3 py-1.5 text-sm text-muted-foreground">
              {service.currentStatus === "NO_DATA" ? "No data" : service.currentStatus}
            </span>
            <button className="rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors">
              View
            </button>
          </div>
        </div>

        {/* Accordion sections */}
        <Accordion title="General Settings" defaultOpen>
          <GeneralSettingsForm slug={slug!} />
        </Accordion>

        <Accordion title={`Checks (${checks?.length ?? 0})`}>
          <ChecksSection slug={slug!} />
        </Accordion>

        <Accordion title="Uptime">
          <div className="rounded-xl border bg-card px-6 py-8 text-sm text-muted-foreground text-center">
            Uptime statistics coming soon.
          </div>
        </Accordion>

        <Accordion title="Status History">
          <div className="rounded-xl border bg-card px-6 py-8 text-sm text-muted-foreground text-center">
            Status history coming soon.
          </div>
        </Accordion>

        <Accordion title="Danger Zone" titleClassName="text-destructive">
          <DangerZone slug={slug!} />
        </Accordion>
      </div>
    </AdminLayout>
  );
}
