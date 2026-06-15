import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ fetch, locals }) => {
  const token = locals.accessToken;
  if (!token) return { services: [] };
  try {
    const res = await fetch("/admin/api", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action: "getServices" }),
    });
    const services = await res.json();
    return { services: Array.isArray(services) ? services : [] };
  } catch {
    return { services: [] };
  }
};
