import { notFound } from "next/navigation";
import Link from "next/link";
import { publicApi } from "@/lib/api";
import { ServiceDetailClient } from "@/components/ServiceDetailClient";

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateMetadata({ params }: Props) {
  const { slug } = await params;
  try {
    const service = await publicApi.service(slug);
    return { title: `${service.name} — Status` };
  } catch {
    return { title: "Service — Status" };
  }
}

export default async function ServiceDetailPage({ params }: Props) {
  const { slug } = await params;

  let service;
  try {
    service = await publicApi.service(slug);
  } catch {
    notFound();
  }

  const [overview, incidents, maintenances] = await Promise.all([
    publicApi.overview(slug, service.historyDaysDesktop),
    publicApi.incidents(true).catch(() => []),
    publicApi.maintenances().catch(() => []),
  ]);

  const serviceIncidents = incidents.filter((i) =>
    i.services.some((s) => s.serviceSlug === slug)
  );
  const serviceMaintenances = maintenances.filter((m) => m.serviceSlugs.includes(slug));

  return (
    <main className="mx-auto w-full max-w-screen-lg px-8 py-10 flex flex-col gap-4">
      <Link href="/" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
        ← Back
      </Link>

      <div className="flex flex-col gap-1 px-1">
        {service.imageUrl && (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={service.imageUrl}
            alt={service.name}
            className="size-12 rounded-xl object-cover mb-1"
          />
        )}
        <h1 className="text-2xl font-bold">{service.name}</h1>
        {service.description && (
          <p className="text-sm text-muted-foreground">{service.description}</p>
        )}
      </div>

      <ServiceDetailClient
        service={service}
        initialOverview={overview}
        incidents={serviceIncidents}
        maintenances={serviceMaintenances}
      />
    </main>
  );
}
