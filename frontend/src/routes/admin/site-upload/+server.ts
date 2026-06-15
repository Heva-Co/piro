import { json } from "@sveltejs/kit";
import { PIRO_API } from "$lib/api";
import type { RequestHandler } from "./$types";

/**
 * Proxies multipart file uploads for site assets (logo, favicon, og-image) to the backend API.
 * Expected form fields: `type` (logo|favicon|og-image), `file` (the binary).
 */
export const POST: RequestHandler = async ({ request, locals }) => {
  const token = locals.accessToken;
  if (!token) return json({ error: "Unauthorized" }, { status: 401 });

  const formData = await request.formData();
  const type = formData.get("type") as string;
  if (!type) return json({ error: "Missing type field" }, { status: 400 });

  const res = await fetch(`${PIRO_API}/api/v1/site/upload/${type}`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: formData,
  });

  const result = await res.json();
  return json(result, { status: res.status });
};
