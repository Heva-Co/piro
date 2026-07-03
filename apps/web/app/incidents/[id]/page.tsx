import { notFound } from "next/navigation";
import { publicApi } from "@/lib/api";
import { IncidentDetailClient } from "./IncidentDetailClient";

export const dynamic = "force-dynamic";

interface Props {
  params: Promise<{ id: string }>;
}

export async function generateMetadata({ params }: Props) {
  const { id } = await params;
  try {
    const incident = await publicApi.incident(id);
    return { title: incident.title };
  } catch {
    return { title: "Incident" };
  }
}

export default async function IncidentDetailPage({ params }: Props) {
  const { id } = await params;

  let incident;
  try {
    incident = await publicApi.incident(id);
  } catch {
    notFound();
  }

  return <IncidentDetailClient id={id} initial={incident} />;
}
