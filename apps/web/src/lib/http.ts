/**
 * Shared fetch helper for Server Components. Runs on the server and calls the
 * backend directly (no browser auth needed since the status page is fully public).
 */

const API_BASE = process.env.INTERNAL_API_URL ?? "http://localhost:5117";

export async function get<T>(path: string, revalidate = 30): Promise<T> {
  const res = await fetch(`${API_BASE}/api/v1${path}`, {
    next: { revalidate },
  });
  if (!res.ok) throw new Error(`API error ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}
