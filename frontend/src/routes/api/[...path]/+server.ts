/**
 * Proxy: forwards all /api/* requests to the internal API (PIRO_API_URL).
 * This lets the browser call /api/v1/... without knowing the API's internal URL.
 */
import { PIRO_API } from "$lib/api";
import type { RequestHandler } from "./$types";

const proxy: RequestHandler = async ({ request, params, url }) => {
  const target = `${PIRO_API}/api/${params.path}${url.search}`;

  const headers = new Headers(request.headers);
  headers.delete("host");

  const hasBody = request.method !== "GET" && request.method !== "HEAD";

  const upstream = await fetch(target, {
    method: request.method,
    headers,
    body: hasBody ? request.body : undefined,
    redirect: "manual",
    // @ts-ignore — required for streaming request bodies in Node 18+
    duplex: "half",
  });

  // Pass through response as-is (including 302 redirects for OIDC start)
  return new Response(upstream.body, {
    status: upstream.status,
    headers: upstream.headers,
  });
};

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
