import { StatusPill } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { ROUTES } from "@/constants/routes";
import { useChecks } from "@/hooks/useChecks";
import { useNavigate } from "react-router-dom";

interface Props {
    slug: string
}

function ChecksSection({ slug }: Props) {
    const navigate = useNavigate();
    const { data: checks, isLoading } = useChecks(slug);

    return (
        <div className="rounded-xl border bg-card overflow-hidden">
            {isLoading ? (
                <div className="px-5 py-6 text-sm text-muted-foreground">Loading…</div>
            ) : !checks || checks.length === 0 ? (
                <div className="px-5 py-8 text-sm text-muted-foreground text-center">No checks configured yet.</div>
            ) : (
                <table className="min-w-full text-sm">
                    <thead>
                        <tr className="border-b bg-muted/40">
                            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Name</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Type</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Status</th>
                            <th className="px-5 py-2.5 text-left text-xs font-semibold text-muted-foreground">Active</th>
                            <th className="px-5 py-2.5" />
                        </tr>
                    </thead>
                    <tbody className="divide-y">
                        {checks.map((check) => (
                            <tr key={check.slug} className="hover:bg-muted/30 transition-colors">
                                <td className="px-5 py-3 font-medium">
                                    {check.name}
                                </td>
                                <td className="px-5 py-3 text-muted-foreground">
                                    {check.type}
                                </td>
                                <td className="px-5 py-3">
                                    <StatusPill status={check.currentStatus} />
                                </td>
                                <td className="px-5 py-3 text-muted-foreground">
                                    {check.isActive ? "Yes" : "No"}
                                </td>
                                <td className="px-5 py-3 text-right">
                                    <Button
                                        onClick={() => navigate(ROUTES.CHECKS.DETAIL(slug, check.slug))}
                                        variant="outline"
                                    >
                                        Configure
                                    </Button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}

export default ChecksSection;