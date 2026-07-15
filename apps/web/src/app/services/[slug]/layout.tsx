import { notFound } from "next/navigation";
import Link from "next/link";
import { servicesApi } from "@/src/lib/actions/services";

interface Props {
  params: Promise<{ slug: string }>;
  children: React.ReactNode;
}

export async function generateMetadata({ params }: Props) {
  const { slug } = await params;
  try {
    const service = await servicesApi.get(slug);
    return { title: `${service.name} — Status` };
  } catch {
    return { title: "Service — Status" };
  }
}

export default async function ServiceDetailLayout({ params, children }: Props) {
  const { slug } = await params;

  let service;
  try {
    service = await servicesApi.get(slug);
  } catch {
    notFound();
  }

  return (
    <main className="mx-auto w-full max-w-5xl px-4 sm:px-8 py-8 sm:py-10 flex flex-col gap-4">
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

      {children}
    </main>
  );
}
