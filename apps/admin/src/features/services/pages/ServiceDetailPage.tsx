import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { AdminLayout } from "@/components/AdminLayout";
import { StatusBadge } from "@/components/StatusBadge";
import { EditServiceForm } from "./ServiceFormPage";
import { useService, useDeleteService } from "@/hooks/useServices";
import { useChecks } from "@/hooks/useChecks";
import { ROUTES } from "@/constants/routes";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";

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

export default function ServiceDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { data: service, isLoading: serviceLoading } = useService(slug!);
  const { data: checks, isLoading: checksLoading } = useChecks(slug!);
  const deleteService = useDeleteService(slug!);

  const [deleteConfirm, setDeleteConfirm] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);

  async function handleDelete() {
    if (deleteConfirm !== slug) return;
    setDeleteError(null);
    try {
      await deleteService.mutateAsync();
      navigate(ROUTES.SERVICES.LIST);
    } catch (err: unknown) {
      setDeleteError(err instanceof Error ? err.message : "Failed to delete service.");
    }
  }

  if (serviceLoading) {
    return (
      <AdminLayout title="Service">
        <div className="text-sm text-gray-500">Loading...</div>
      </AdminLayout>
    );
  }

  if (!service) {
    return (
      <AdminLayout title="Service">
        <div className="text-sm text-red-500">Service not found.</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout title={service.name}>
      <div className="flex items-center gap-3 mb-6">
        <span className="text-sm text-gray-500 font-mono">{service.slug}</span>
        <StatusBadge status={service.status} />
      </div>

      {/* General Settings */}
      <Accordion title="General Settings" defaultOpen>
        <EditServiceForm slug={slug!} />
      </Accordion>

      {/* Checks */}
      <Accordion title="Checks" defaultOpen>
        <div className="flex justify-between items-center mb-4">
          <p className="text-sm text-gray-500">Checks configured for this service.</p>
          <button
            onClick={() => navigate(`/admin/services/${slug}/checks/new`)}
            className="px-3 py-1.5 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 transition-colors"
          >
            Add Check
          </button>
        </div>
        {checksLoading ? (
          <div className="text-sm text-gray-500">Loading checks...</div>
        ) : !checks || checks.length === 0 ? (
          <div className="text-sm text-gray-500">No checks yet.</div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-2 text-left font-medium text-gray-500">Name</th>
                <th className="px-4 py-2 text-left font-medium text-gray-500">Type</th>
                <th className="px-4 py-2 text-left font-medium text-gray-500">Status</th>
                <th className="px-4 py-2 text-left font-medium text-gray-500">Active</th>
                <th className="px-4 py-2 text-left font-medium text-gray-500">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {checks.map((check) => (
                <tr key={check.slug} className="hover:bg-gray-50">
                  <td className="px-4 py-2 font-medium text-gray-900">{check.name}</td>
                  <td className="px-4 py-2 text-gray-500">{check.type}</td>
                  <td className="px-4 py-2">
                    <StatusBadge status={check.status} />
                  </td>
                  <td className="px-4 py-2 text-gray-500">
                    {check.isActive ? "Yes" : "No"}
                  </td>
                  <td className="px-4 py-2">
                    <button
                      onClick={() =>
                        navigate(ROUTES.CHECKS.DETAIL(slug!, check.slug))
                      }
                      className="text-indigo-600 hover:text-indigo-800 text-sm font-medium"
                    >
                      View
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Accordion>

      {/* Danger Zone */}
      <Accordion title="Danger Zone">
        <div className="space-y-3">
          <p className="text-sm text-gray-600">
            Permanently delete this service and all its checks. Type{" "}
            <span className="font-mono font-semibold">{slug}</span> to confirm.
          </p>
          {deleteError && (
            <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md p-3">
              {deleteError}
            </div>
          )}
          <input
            type="text"
            placeholder={slug}
            value={deleteConfirm}
            onChange={(e) => setDeleteConfirm(e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm w-64 focus:outline-none focus:ring-2 focus:ring-red-500"
          />
          <div>
            <button
              onClick={handleDelete}
              disabled={deleteConfirm !== slug || deleteService.isPending}
              className="px-4 py-2 bg-red-600 text-white rounded-md text-sm font-medium hover:bg-red-700 disabled:opacity-40 transition-colors"
            >
              {deleteService.isPending ? "Deleting..." : "Delete Service"}
            </button>
          </div>
        </div>
      </Accordion>
    </AdminLayout>
  );
}
