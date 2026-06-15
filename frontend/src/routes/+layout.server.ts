import { authApi, type SiteConfigDto } from "$lib/api";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals }) => {
  let siteConfig: SiteConfigDto | null = null;
  try {
    siteConfig = await authApi.getSiteConfig();
  } catch { /* ignore — app still works without site config */ }
  return { user: locals.user, siteConfig };
};
