/**
 * Proxy: forwards /hub/* requests to the internal API for SignalR.
 * WebSocket upgrades are not supported here — SignalR will fall back to
 * Server-Sent Events or Long Polling automatically.
 */
import { PIRO_API } from "$lib/api";
import type { RequestHandler } from "./$types";

const proxy: RequestHandler = async ({ request, params, url }) => {
  const target = `${PIRO_API}/hub/${params.path}${url.search}`;

  const headers = new Headers(request.headers);
  headers.delete("host");

  const hasBody = request.method !== "GET" && request.method !== "HEAD";

  const upstream = await fetch(target, {
    method: request.method,
    headers,
    body: hasBody ? request.body : undefined,
    redirect: "manual",
    // @ts-ignore
    duplex: "half",
  });

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
