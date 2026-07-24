import { NextResponse } from "next/server";

const API_BASE = process.env.INTERNAL_API_URL ?? "http://localhost:5117";

// Proxies the public incident timeline. The detail page is a client component and polls this on an
// interval, so it must go through the Next server (which can reach INTERNAL_API_URL) rather than the
// browser calling the backend directly, which would be a cross-origin request the browser blocks.
export async function GET(req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const { searchParams } = new URL(req.url);
  const page = searchParams.get("page") ?? "1";
  const pageSize = searchParams.get("pageSize") ?? "20";
  const res = await fetch(
    `${API_BASE}/api/v1/public/incidents/${id}/timeline?page=${page}&pageSize=${pageSize}`,
    { cache: "no-store" },
  );
  if (!res.ok) return NextResponse.json({ items: [], total: 0 }, { status: res.status });
  return NextResponse.json(await res.json());
}
