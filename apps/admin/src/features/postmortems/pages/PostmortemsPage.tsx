import { useNavigate } from "react-router-dom";
import { Plus, Settings } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import TableSkeleton from "@/components/TableSkeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import PostmortemStatusBadge from "@/features/postmortems/components/PostmortemStatusBadge";
import { usePostmortems } from "@/hooks/usePostmortems";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { ROUTES } from "@/constants/routes";

const columns = ["Report", "Status", "Owner", "Incidents", "Created"];

function PostmortemsPage() {
  const navigate = useNavigate();
  const { data, isLoading, isError } = usePostmortems();
  const { formatDate } = useFormattedDate();

  const postmortems = data ?? [];

  return (
    <div className="flex flex-col">
      <PageHeader
        breadcrumbs={[{ label: "Postmortems" }]}
        actions={
          <button
            onClick={() => navigate(ROUTES.POSTMORTEMS.NEW)}
            className="flex items-center gap-1.5 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={14} /> New Postmortem
          </button>
        }
      />

      <div className="rounded-xl border bg-card overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={columns} />
        ) : isError ? (
          <div className="px-6 py-8 text-sm text-destructive">Failed to load postmortems.</div>
        ) : postmortems.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-4 py-20">
            <img src="/piro.svg" alt="Piro" className="h-16 w-16 opacity-20" />
            <div className="text-center">
              <p className="text-sm font-medium text-muted-foreground">No postmortems yet</p>
              <p className="text-xs text-muted-foreground mt-1">
                Write a post-incident review to capture root cause and action items.
              </p>
            </div>
            <button
              onClick={() => navigate(ROUTES.POSTMORTEMS.NEW)}
              className="flex items-center gap-1.5 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-colors"
            >
              <Plus size={14} /> New Postmortem
            </button>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {postmortems.map((pm) => (
                <TableRow
                  key={pm.id}
                  className="hover:bg-muted/30 transition-colors cursor-pointer"
                  onClick={() => navigate(ROUTES.POSTMORTEMS.DETAIL(pm.id))}
                >
                  <TableCell>
                    <span className="font-medium">{pm.name}</span>
                  </TableCell>
                  <TableCell>
                    <PostmortemStatusBadge status={pm.status} />
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {pm.reviewOwnerName ?? "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{pm.incidentCount}</TableCell>
                  <TableCell className="text-muted-foreground">{formatDate(pm.createdAt)}</TableCell>
                  <TableCell className="text-right">
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        navigate(ROUTES.POSTMORTEMS.DETAIL(pm.id));
                      }}
                      className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors"
                    >
                      <Settings size={13} /> Open
                    </button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </div>
  );
}

export default PostmortemsPage;
