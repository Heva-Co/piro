import { fail, redirect } from "@sveltejs/kit";
import { userApi } from "$lib/api.js";
import type { PageServerLoad, Actions } from "./$types";

export const load: PageServerLoad = async ({ params }) => {
  return { token: params.token };
};

export const actions: Actions = {
  default: async ({ request, params }) => {
    const form = await request.formData();
    const name = (form.get("name") as string)?.trim();
    const password = form.get("password") as string;
    const confirmPassword = form.get("confirmPassword") as string;

    if (!name) return fail(400, { error: "Name is required.", name, password: "" });
    if (!password || password.length < 8)
      return fail(400, { error: "Password must be at least 8 characters.", name, password: "" });
    if (password !== confirmPassword)
      return fail(400, { error: "Passwords do not match.", name, password: "" });

    try {
      await userApi.acceptInvite(params.token, name, password);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : "Invalid or expired invitation.";
      return fail(400, { error: msg, name, password: "" });
    }

    redirect(303, "/auth/sign-in?invited=1");
  },
};
