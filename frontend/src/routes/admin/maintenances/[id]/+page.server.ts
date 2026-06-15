import { error } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ params, fetch, locals }) => {
  if (!locals.accessToken) throw error(401, "Unauthorized");
  const id = parseInt(params.id, 10);
  if (isNaN(id)) throw error(404, "Not found");

  const [mRes, sRes] = await Promise.all([
    fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "getMaintenance", data: { id } }),
    }),
    fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "getServices" }),
    }),
  ]);

  const maintenance = await mRes.json();
  if (maintenance.error) throw error(404, maintenance.error);
  const services = await sRes.json();

  return { maintenance, services: Array.isArray(services) ? services : [] };
};
