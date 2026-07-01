import { publicApi } from "@/lib/api";
import { IncidentHistoryClient } from "@/components/IncidentHistoryClient";

export const revalidate = 60;

export const metadata = {
  title: "Incident History",
};

export default async function IncidentHistoryPage() {
  const [incidents, maintenances] = await Promise.all([
    publicApi.incidents(true).catch(() => []),
    publicApi.maintenances().catch(() => []),
  ]);

  return (
    <main className="mx-auto w-full max-w-screen-lg px-8 py-10 flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl sm:text-3xl font-bold">Incident History</h1>
        <p className="text-sm text-muted-foreground">
          Past and current incidents and scheduled maintenances
        </p>
      </div>
      <IncidentHistoryClient incidents={incidents} maintenances={maintenances} />
    </main>
  );
}
