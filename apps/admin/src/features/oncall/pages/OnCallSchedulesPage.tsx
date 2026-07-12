import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, CalendarClock, X, AlertTriangle } from "lucide-react";
import { onCallApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { TimezonePicker } from "@/components/TimezonePicker";
import { PageHeader } from "@/components/PageHeader";
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
import MemberAvatars from "../components/MemberAvatars";

const PAGE_SIZE = 15;

const columns = ["Name", "Timezone", "Members"];

export default function OnCallSchedulesPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [page, setPage] = useState(1);
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [timeZone, setTimeZone] = useState("UTC");
  const [formError, setFormError] = useState("");

  const { data, isLoading } = useQuery({
    queryKey: [...QUERY_KEYS.ONCALL_SCHEDULES, page],
    queryFn: () => onCallApi.list({ page, pageSize: PAGE_SIZE }),
  });
  const schedules = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const createMutation = useMutation({
    mutationFn: () =>
      onCallApi.create({ name, description: description || undefined, timeZone, notifyOnShiftStart: false }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES });
      setOpen(false);
      resetForm();
      navigate(ROUTES.ONCALL.DETAIL(created.id));
    },
    onError: () => setFormError("Failed to create schedule."),
  });

  function resetForm() {
    setName("");
    setDescription("");
    setTimeZone("UTC");
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
          breadcrumbs={[{ label: "On Call Schedules" }]}
          subheader="Define who is on-call at any given moment using rotation layers and overrides."
          actions={
            <button
              onClick={handleOpen}
              className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90"
            >
              <Plus size={15} /> Add schedule
            </button>
          }
        />

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {isLoading ? (
            <TableSkeleton columns={columns} />
          ) : schedules.length === 0 ? (
            <div className="p-8 text-center text-sm text-muted-foreground">
              No on-call schedules yet.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
                </TableRow>
              </TableHeader>
              <TableBody>
                {schedules.map((s) => (
                  <TableRow
                    key={s.id}
                    className="hover:bg-muted/50 cursor-pointer"
                    onClick={() => navigate(ROUTES.ONCALL.DETAIL(s.id))}
                  >
                    <TableCell className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <CalendarClock size={14} className="text-muted-foreground shrink-0" />
                        <span className="font-medium text-foreground">{s.name}</span>
                        {s.layers.length === 0 && (
                          <span
                            title="No rotation layers — nobody is on-call for this schedule"
                            className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-500"
                          >
                            <AlertTriangle size={12} /> No coverage
                          </span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">{s.timeZone}</TableCell>
                    <TableCell className="px-5 py-3.5">
                      <MemberAvatars layers={s.layers} />
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
                          Page {page} of {totalPages} · {totalCount} schedule{totalCount === 1 ? "" : "s"}
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

      {/* Create schedule modal */}
      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-border">
              <h2 className="text-sm font-semibold text-foreground">New On-Call Schedule</h2>
              <button onClick={() => setOpen(false)} className="text-muted-foreground hover:text-foreground">
                <X size={16} />
              </button>
            </div>
            <div className="flex flex-col gap-4 px-6 py-5">
              {formError && (
                <p className="text-xs text-destructive">{formError}</p>
              )}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Name</label>
                <input
                  autoFocus
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Production on-call"
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Timezone</label>
                <TimezonePicker value={timeZone} onChange={setTimeZone} />
                <p className="text-xs text-muted-foreground">
                  Used to display shift times in the Gantt and for shift-start notifications. All data is stored in UTC — this only affects how times are shown.
                </p>
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Description <span className="text-muted-foreground font-normal">(optional)</span></label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Who this schedule covers and when"
                  rows={2}
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20 resize-none"
                />
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border">
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => { setFormError(""); createMutation.mutate(); }}
                disabled={!name.trim() || createMutation.isPending}
                className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {createMutation.isPending ? "Creating…" : "Create schedule"}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
