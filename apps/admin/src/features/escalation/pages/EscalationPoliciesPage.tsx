import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import axios from "axios";
import { Plus, Siren } from "lucide-react";
import { escalationApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import TableSkeleton from "@/components/TableSkeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationPrevious,
  PaginationNext,
} from "@/components/ui/pagination";

const PAGE_SIZE = 15;

const columns = ["Name", "Description", "Steps", "Re-escalation"];

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

export default function EscalationPoliciesPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [page, setPage] = useState(1);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [formError, setFormError] = useState("");

  const { data, isLoading } = useQuery({
    queryKey: [...QUERY_KEYS.ESCALATION_POLICIES, page],
    queryFn: () => escalationApi.list({ page, pageSize: PAGE_SIZE }),
  });
  const policies = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const createMutation = useMutation({
    mutationFn: () =>
      escalationApi.create({
        name,
        description: description || undefined,
        reEscalateAfterInactivityMinutes: 0,
        steps: [],
      }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ESCALATION_POLICIES });
      setOpen(false);
      resetForm();
      navigate(ROUTES.ESCALATION.DETAIL(created.id));
    },
    onError: (err) => setFormError(apiErrorMessage(err, "Failed to create escalation policy.")),
  });

  function resetForm() {
    setName("");
    setDescription("");
    setFormError("");
  }

  function handleOpen() {
    resetForm();
    setOpen(true);
  }

  return (
    <>
      <div className="flex flex-col gap-4">
        <PageHeader
          breadcrumbs={[{ label: "Escalation Policies" }]}
          subheader="Define reusable escalation paths and assign them to services."
          actions={
            <Button onClick={handleOpen}>
              <Plus size={15} /> New policy
            </Button>
          }
        />

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {isLoading ? (
            <TableSkeleton columns={columns} />
          ) : policies.length === 0 ? (
            <div className="p-8 text-center text-sm text-muted-foreground">
              No escalation policies yet.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
                </TableRow>
              </TableHeader>
              <TableBody>
                {policies.map((p) => (
                  <TableRow
                    key={p.id}
                    className="hover:bg-muted/50 cursor-pointer"
                    onClick={() => navigate(ROUTES.ESCALATION.DETAIL(p.id))}
                  >
                    <TableCell className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <Siren size={14} className="text-muted-foreground shrink-0" />
                        <span className="font-medium text-foreground">{p.name}</span>
                      </div>
                    </TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">{p.description || "—"}</TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">{p.steps.length}</TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">
                      {p.reEscalateAfterInactivityMinutes > 0
                        ? `${p.reEscalateAfterInactivityMinutes}m after inactivity`
                        : "Disabled"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
              {totalCount > 0 && (
                <TableFooter>
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={columns.length} className="px-4 py-3">
                      <div className="flex items-center justify-between text-sm font-normal">
                        <span className="text-muted-foreground">
                          Page {page} of {totalPages} · {totalCount} polic{totalCount === 1 ? "y" : "ies"}
                        </span>
                        <Pagination className="mx-0 w-auto">
                          <PaginationContent>
                            <PaginationItem>
                              <PaginationPrevious
                                href="#"
                                onClick={(e) => {
                                  e.preventDefault();
                                  if (page > 1) setPage((p) => p - 1);
                                }}
                                className={page <= 1 ? "pointer-events-none opacity-50" : ""}
                              />
                            </PaginationItem>
                            <PaginationItem>
                              <PaginationLink href="#" isActive size="default" className="pointer-events-none px-3">
                                {page} / {totalPages}
                              </PaginationLink>
                            </PaginationItem>
                            <PaginationItem>
                              <PaginationNext
                                href="#"
                                onClick={(e) => {
                                  e.preventDefault();
                                  if (page < totalPages) setPage((p) => p + 1);
                                }}
                                className={page >= totalPages ? "pointer-events-none opacity-50" : ""}
                              />
                            </PaginationItem>
                          </PaginationContent>
                        </Pagination>
                      </div>
                    </TableCell>
                  </TableRow>
                </TableFooter>
              )}
            </Table>
          )}
        </div>
      </div>

      {/* Create policy modal */}
      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-border">
              <h2 className="text-sm font-semibold text-foreground">New Escalation Policy</h2>
            </div>
            <div className="flex flex-col gap-4 px-6 py-5">
              {formError && <p className="text-xs text-destructive">{formError}</p>}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Name</label>
                <Input
                  autoFocus
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Default policy"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">
                  Description <span className="text-muted-foreground font-normal">(optional)</span>
                </label>
                <Input
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="What this policy covers"
                />
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border">
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                Cancel
              </Button>
              <Button
                type="button"
                onClick={() => { setFormError(""); createMutation.mutate(); }}
                disabled={!name.trim() || createMutation.isPending}
              >
                {createMutation.isPending ? "Creating…" : "Create policy"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
