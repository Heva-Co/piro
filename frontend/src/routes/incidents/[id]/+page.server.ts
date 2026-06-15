import { error } from "@sveltejs/kit";
import { publicApi } from "$lib/api";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ params }) => {
  const id = parseInt(params.id, 10);
  if (isNaN(id)) throw error(404, "Not found");

  try {
    const incident = await publicApi.getIncident(id);
    return { incident };
  } catch {
    throw error(404, "Incident not found");
  }
};
