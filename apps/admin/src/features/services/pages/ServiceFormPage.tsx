import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { AdminLayout } from "@/components/AdminLayout";
import { useCreateService, useUpdateService, useService } from "@/hooks/useServices";
import { ROUTES } from "@/constants/routes";

interface ServiceFormProps {
  mode: "new" | "edit";
  /** For edit mode: initial values */
  initialValues?: {
    name: string;
    description: string;
    displayOrder: number;
    isHidden: boolean;
    isPublic: boolean;
  };
  onSubmit: (values: {
    name: string;
    slug: string;
    description: string;
    displayOrder: number;
    isHidden: boolean;
    isPublic: boolean;
  }) => Promise<void>;
  isLoading: boolean;
  error: string | null;
}

function slugify(str: string): string {
  return str
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function ServiceForm({ mode, initialValues, onSubmit, isLoading, error }: ServiceFormProps) {
  const [name, setName] = useState(initialValues?.name ?? "");
  const [slug, setSlug] = useState("");
  const [slugManual, setSlugManual] = useState(false);
  const [description, setDescription] = useState(initialValues?.description ?? "");
  const [displayOrder, setDisplayOrder] = useState(initialValues?.displayOrder ?? 0);
  const [isHidden, setIsHidden] = useState(initialValues?.isHidden ?? false);
  const [isPublic, setIsPublic] = useState(initialValues?.isPublic ?? true);

  useEffect(() => {
    if (mode === "new" && !slugManual) {
      setSlug(slugify(name));
    }
  }, [name, mode, slugManual]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    await onSubmit({ name, slug, description, displayOrder, isHidden, isPublic });
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4 max-w-lg">
      {error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Name <span className="text-red-500">*</span>
        </label>
        <input
          type="text"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      {mode === "new" && (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Slug <span className="text-red-500">*</span>
          </label>
          <input
            type="text"
            required
            value={slug}
            onChange={(e) => {
              setSlugManual(true);
              setSlug(e.target.value);
            }}
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={3}
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Display Order</label>
        <input
          type="number"
          value={displayOrder}
          onChange={(e) => setDisplayOrder(Number(e.target.value))}
          className="w-32 border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      <div className="flex items-center gap-6">
        <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
          <input
            type="checkbox"
            checked={isHidden}
            onChange={(e) => setIsHidden(e.target.checked)}
            className="rounded border-gray-300 text-indigo-600"
          />
          Hidden
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
          <input
            type="checkbox"
            checked={isPublic}
            onChange={(e) => setIsPublic(e.target.checked)}
            className="rounded border-gray-300 text-indigo-600"
          />
          Public
        </label>
      </div>

      <div className="pt-2">
        <button
          type="submit"
          disabled={isLoading}
          className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
        >
          {isLoading ? "Saving..." : mode === "new" ? "Create Service" : "Save Changes"}
        </button>
      </div>
    </form>
  );
}

/** New service page */
export function NewServicePage() {
  const navigate = useNavigate();
  const createService = useCreateService();
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(values: {
    name: string;
    slug: string;
    description: string;
    displayOrder: number;
    isHidden: boolean;
    isPublic: boolean;
  }) {
    setError(null);
    try {
      const service = await createService.mutateAsync({
        name: values.name,
        description: values.description || undefined,
        displayOrder: values.displayOrder,
        isPublic: values.isPublic,
      } as Parameters<typeof createService.mutateAsync>[0]);
      navigate(ROUTES.SERVICES.DETAIL(service.slug));
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : "Failed to create service.";
      setError(message);
    }
  }

  return (
    <AdminLayout title="New Service">
      <div className="bg-white rounded-lg border border-gray-200 shadow-sm p-6">
        <ServiceForm
          mode="new"
          onSubmit={handleSubmit}
          isLoading={createService.isPending}
          error={error}
        />
      </div>
    </AdminLayout>
  );
}

/** Edit service form (used inside ServiceDetailPage accordion) */
export function EditServiceForm({ slug }: { slug: string }) {
  const { data: service, isLoading } = useService(slug);
  const updateService = useUpdateService(slug);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  if (isLoading) return <div className="text-sm text-gray-500">Loading...</div>;
  if (!service) return <div className="text-sm text-red-500">Service not found.</div>;

  async function handleSubmit(values: {
    name: string;
    slug: string;
    description: string;
    displayOrder: number;
    isHidden: boolean;
    isPublic: boolean;
  }) {
    setError(null);
    setSuccessMsg(null);
    try {
      await updateService.mutateAsync({
        name: values.name,
        description: values.description || undefined,
        displayOrder: values.displayOrder,
        isPublic: values.isPublic,
      });
      setSuccessMsg("Saved successfully.");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to update service.";
      setError(message);
    }
  }

  return (
    <>
      {successMsg && (
        <div className="mb-4 text-sm text-green-700 bg-green-50 border border-green-200 rounded-md p-3">
          {successMsg}
        </div>
      )}
      <ServiceForm
        mode="edit"
        initialValues={{
          name: service.name,
          description: service.description ?? "",
          displayOrder: service.displayOrder,
          isHidden: false,
          isPublic: service.isPublic,
        }}
        onSubmit={handleSubmit}
        isLoading={updateService.isPending}
        error={error}
      />
    </>
  );
}

/** Default export for new service route */
export default function ServiceFormPage() {
  const params = useParams<{ slug?: string }>();
  if (params.slug) {
    // Should not happen via this route, but just in case
    return <EditServiceForm slug={params.slug} />;
  }
  return <NewServicePage />;
}
