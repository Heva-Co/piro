import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";

/** Guard: all /admin/* routes require authentication. */
export const load: LayoutServerLoad = async ({ locals, url, parent }) => {
  if (!locals.user) {
    redirect(302, `/auth/sign-in?next=${encodeURIComponent(url.pathname)}`);
  }
  const parentData = await parent();
  return { ...parentData, user: locals.user, accessToken: locals.accessToken };
};
