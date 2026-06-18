import { useQuery } from "@tanstack/react-query";
import { servicesApi, incidentsApi, maintenancesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { AdminLayout } from "@/components/AdminLayout";
import { StatusBadge } from "@/components/StatusBadge";

function StatCard({
  label,
  value,
  color,
}: {
  label: string;
  value: number;
  color: string;
}) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5 shadow-sm">
      <p className="text-sm text-gray-500 mb-1">{label}</p>
      <p className={`text-3xl font-bold ${color}`}>{value}</p>
    </div>
  );
}

export default function DashboardPage() {
  const servicesQuery = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });
  const incidentsQuery = useQuery({
    queryKey: QUERY_KEYS.INCIDENTS,
    queryFn: incidentsApi.list,
  });
  const maintenancesQuery = useQuery({
    queryKey: QUERY_KEYS.MAINTENANCES,
    queryFn: maintenancesApi.list,
  });

  const services = servicesQuery.data ?? [];
  const incidents = incidentsQuery.data ?? [];
  const maintenances = maintenancesQuery.data ?? [];

  const totalServices = services.length;
  const operational = services.filter((s) => s.status === "UP").length;
  const withIssues = services.filter(
    (s) => s.status === "DOWN" || s.status === "DEGRADED"
  ).length;
  const activeIncidents = incidents.filter(
    (i) => i.status !== "RESOLVED"
  ).length;
  const activeMaintenances = maintenances.filter(
    (m) => m.status === "ACTIVE" || m.status === "SCHEDULED"
  );

  return (
    <AdminLayout title="Overview">
      {/* Stat cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Total Services" value={totalServices} color="text-gray-900" />
        <StatCard label="Operational" value={operational} color="text-green-600" />
        <StatCard label="With Issues" value={withIssues} color="text-red-600" />
        <StatCard label="Active Incidents" value={activeIncidents} color="text-amber-600" />
      </div>

      <div className="flex flex-col lg:flex-row gap-6">
        {/* Services table */}
        <div className="flex-1 lg:w-2/3 bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-gray-100">
            <h2 className="font-semibold text-gray-900">Services</h2>
          </div>
          {servicesQuery.isLoading ? (
            <div className="p-6 text-sm text-gray-500">Loading...</div>
          ) : servicesQuery.isError ? (
            <div className="p-6 text-sm text-red-500">Failed to load services.</div>
          ) : services.length === 0 ? (
            <div className="p-6 text-sm text-gray-500">No services found.</div>
          ) : (
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-5 py-3 text-left font-medium text-gray-500">Name</th>
                  <th className="px-5 py-3 text-left font-medium text-gray-500">Slug</th>
                  <th className="px-5 py-3 text-left font-medium text-gray-500">Status</th>
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
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Right sidebar */}
        <div className="lg:w-1/3 flex flex-col gap-4">
          {/* Active incidents */}
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
            <div className="px-5 py-4 border-b border-gray-100">
              <h2 className="font-semibold text-gray-900">Active Incidents</h2>
            </div>
            {incidentsQuery.isLoading ? (
              <div className="p-4 text-sm text-gray-500">Loading...</div>
            ) : activeIncidents === 0 ? (
              <div className="p-4 text-sm text-gray-500">No active incidents.</div>
            ) : (
              <ul className="divide-y divide-gray-100">
                {incidents
                  .filter((i) => i.status !== "RESOLVED")
                  .map((incident) => (
                    <li key={incident.id} className="px-5 py-3">
                      <p className="text-sm font-medium text-gray-900">{incident.title}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <StatusBadge status={incident.severity} />
                        <span className="text-xs text-gray-500">{incident.status}</span>
                      </div>
                    </li>
                  ))}
              </ul>
            )}
          </div>

          {/* Active maintenances */}
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
            <div className="px-5 py-4 border-b border-gray-100">
              <h2 className="font-semibold text-gray-900">Active Maintenances</h2>
            </div>
            {maintenancesQuery.isLoading ? (
              <div className="p-4 text-sm text-gray-500">Loading...</div>
            ) : activeMaintenances.length === 0 ? (
              <div className="p-4 text-sm text-gray-500">No active maintenances.</div>
            ) : (
              <ul className="divide-y divide-gray-100">
                {activeMaintenances.map((m) => (
                  <li key={m.id} className="px-5 py-3">
                    <p className="text-sm font-medium text-gray-900">{m.name}</p>
                    <p className="text-xs text-gray-500 mt-0.5">{m.status}</p>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
}
