import { NextResponse } from "next/server";

const API_BASE = process.env.INTERNAL_API_URL ?? "http://localhost:5117";

export async function GET(_req: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const res = await fetch(`${API_BASE}/api/v1/incidents/${id}`, { cache: "no-store" });
  if (!res.ok) return NextResponse.json({}, { status: res.status });
  return NextResponse.json(await res.json());
}
