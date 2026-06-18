import { useNavigate } from "react-router-dom";
import { AdminLayout } from "@/components/AdminLayout";
import { StatusBadge } from "@/components/StatusBadge";
import { useServices } from "@/hooks/useServices";
import { ROUTES } from "@/constants/routes";

export default function ServicesPage() {
  const navigate = useNavigate();
  const { data: services, isLoading, isError } = useServices();

  return (
    <AdminLayout title="Services">
      <div className="flex justify-between items-center mb-5">
        <p className="text-sm text-gray-500">Manage your monitored services.</p>
        <button
          onClick={() => navigate(ROUTES.SERVICES.NEW)}
          className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 transition-colors"
        >
          New Service
        </button>
      </div>

      <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="p-6 text-sm text-gray-500">Loading...</div>
        ) : isError ? (
          <div className="p-6 text-sm text-red-500">Failed to load services.</div>
        ) : !services || services.length === 0 ? (
          <div className="p-6 text-sm text-gray-500">No services yet.</div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-3 text-left font-medium text-gray-500">Name</th>
                <th className="px-5 py-3 text-left font-medium text-gray-500">Slug</th>
                <th className="px-5 py-3 text-left font-medium text-gray-500">Status</th>
                <th className="px-5 py-3 text-left font-medium text-gray-500">Hidden</th>
                <th className="px-5 py-3 text-left font-medium text-gray-500">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {services.map((service) => (
                <tr key={service.slug} className="hover:bg-gray-50">
                  <td className="px-5 py-3 font-medium text-gray-900">{service.name}</td>
                  <td className="px-5 py-3 text-gray-500">{service.slug}</td>
                  <td className="px-5 py-3">
                    <StatusBadge status={service.status} />
                  </td>
                  <td className="px-5 py-3 text-gray-500">
                    {(service as { isHidden?: boolean }).isHidden ? "Yes" : "No"}
                  </td>
                  <td className="px-5 py-3">
                    <button
                      onClick={() => navigate(ROUTES.SERVICES.DETAIL(service.slug))}
                      className="text-indigo-600 hover:text-indigo-800 text-sm font-medium"
                    >
                      Edit
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </AdminLayout>
  );
}
