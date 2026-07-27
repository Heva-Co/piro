import { redirect } from "next/navigation";

async function ServiceDetailPage(props: PageProps<'/services/[slug]'>) {
  const { params, searchParams } = props;

  const { slug } = await params;
  const query = await searchParams;
  const days = typeof query.days === "string" ? `?days=${query.days}` : "";
  redirect(`/services/${slug}/status${days}`);
}

export default ServiceDetailPage;
