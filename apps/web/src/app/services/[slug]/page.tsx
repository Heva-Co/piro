import { redirect } from "next/navigation";

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}

export default async function ServiceDetailPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const query = await searchParams;
  const days = typeof query.days === "string" ? `?days=${query.days}` : "";
  redirect(`/services/${slug}/status${days}`);
}
