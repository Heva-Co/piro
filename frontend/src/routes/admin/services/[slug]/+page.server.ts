import { error, fail, redirect } from "@sveltejs/kit";
import { adminApi, ApiError } from "$lib/api";
import type { Actions, PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals, params }) => {
  try {
    const [services, checks] = await Promise.all([
      adminApi.getServices(locals.accessToken!),
      adminApi.getChecks(locals.accessToken!, params.slug),
    ]);
    const service = services.find((s) => s.slug === params.slug);
    if (!service) error(404, "Service not found");
    return { service, checks };
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) error(404, "Service not found");
    throw e;
  }
};

export const actions: Actions = {
  updateService: async ({ request, locals, params }) => {
    const form = await request.formData();
    const data: Record<string, unknown> = {};
    const name = (form.get("name") as string).trim();
    const description = (form.get("description") as string).trim();
    if (name) data.name = name;
    data.description = description || null;
    data.isHidden = form.get("isHidden") === "on";
    data.displayOrder = parseInt(form.get("displayOrder") as string) || 0;

    try {
      await adminApi.updateService(locals.accessToken!, params.slug, data);
    } catch (e: unknown) {
      return fail(400, { error: e instanceof Error ? e.message : "Update failed." });
    }
    return { success: true };
  },

  updateHistoryDays: async ({ request, locals, params }) => {
    const form = await request.formData();
    const desktop = parseInt(form.get("historyDaysDesktop") as string);
    const mobile = parseInt(form.get("historyDaysMobile") as string);
    if (isNaN(desktop) || isNaN(mobile) || desktop < 1 || mobile < 1 || desktop > 365 || mobile > 365) {
      return fail(400, { error: "Values must be between 1 and 365." });
    }
    try {
      await adminApi.updateService(locals.accessToken!, params.slug, {
        historyDaysDesktop: desktop,
        historyDaysMobile: mobile,
      });
    } catch (e: unknown) {
      return fail(400, { error: e instanceof Error ? e.message : "Update failed." });
    }
    return { success: true };
  },

  deleteService: async ({ locals, params }) => {
    await adminApi.deleteService(locals.accessToken!, params.slug);
    redirect(302, "/admin/services");
  },

  createCheck: async ({ request, locals, params }) => {
    const form = await request.formData();
    const slug = (form.get("slug") as string).trim();
    const name = (form.get("name") as string).trim();
    const type = form.get("type") as string;
    const cron = (form.get("cron") as string).trim() || "* * * * *";
    const typeDataJson = (form.get("typeDataJson") as string).trim() || "{}";

    if (!slug || !name || !type) return fail(400, { checkError: "Slug, name, and type are required." });

    // Validate JSON
    try { JSON.parse(typeDataJson); } catch {
      return fail(400, { checkError: "Type data must be valid JSON." });
    }

    try {
      await adminApi.createCheck(locals.accessToken!, params.slug, {
        slug, name, type, cron, typeDataJson, defaultStatus: "NO_DATA",
      });
    } catch (e: unknown) {
      return fail(400, { checkError: e instanceof Error ? e.message : "Failed to create check." });
    }
    return { checkSuccess: true };
  },

  deleteCheck: async ({ request, locals, params }) => {
    const form = await request.formData();
    const checkSlug = form.get("checkSlug") as string;
    await adminApi.deleteCheck(locals.accessToken!, params.slug, checkSlug);
    return { checkSuccess: true };
  },

  runCheck: async ({ request, locals, params }) => {
    const form = await request.formData();
    const checkSlug = form.get("checkSlug") as string;
    await adminApi.runCheck(locals.accessToken!, params.slug, checkSlug);
    return { checkSuccess: true };
  },
};
