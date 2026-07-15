import { notFound } from "next/navigation";
import { incidentsApi } from "@/src/lib/actions/incidents";
import { IncidentDetailClient } from "./IncidentDetailClient";

export const dynamic = "force-dynamic";

interface Props {
  params: Promise<{ id: string }>;
}

export async function generateMetadata({ params }: Props) {
  const { id } = await params;
  try {
    const incident = await incidentsApi.get(id);
    return { title: incident.title };
  } catch {
    return { title: "Incident" };
  }
}

export default async function IncidentDetailPage({ params }: Props) {
  const { id } = await params;

  let incident;
  try {
    incident = await incidentsApi.get(id);
  } catch {
    notFound();
  }

  return <IncidentDetailClient id={id} initial={incident} />;
}
