import { fail, redirect } from "@sveltejs/kit";
import { authApi, ApiError } from "$lib/api";
import { setTokenCookies } from "$lib/auth";
import type { Actions, PageServerLoad } from "./$types";

export const load: PageServerLoad = async () => {
  const status = await authApi.setupStatus().catch(() => ({ setupRequired: false }));
  if (!status.setupRequired) redirect(302, "/");
  return {};
};

export const actions: Actions = {
  default: async ({ request, cookies }) => {
    const form = await request.formData();
    const email = form.get("email") as string;
    const password = form.get("password") as string;
    const name = form.get("name") as string;
    if (!email || !password || !name) {
      return fail(400, { error: "All fields are required.", email, name });
    }

    try {
      await authApi.completeSetup(email, password, name);
      // Auto sign-in after setup
      const result = await authApi.signIn(email, password);
      setTokenCookies(cookies, result.accessToken, result.refreshToken, result.expiresIn);
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : "Setup failed.";
      return fail(400, { error: msg, email, name });
    }

    // Return success — client advances to step 2 (email config)
    return { accountCreated: true };
  },
};
