import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, CalendarClock, AlertTriangle } from "lucide-react";
import { onCallApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import TableSkeleton from "@/components/TableSkeleton";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
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
import CreateScheduleModal from "../components/CreateScheduleModal";

const PAGE_SIZE = 15;

const columns = ["Name", "Timezone", "Members"];

function OnCallSchedulesPage() {
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
    <PageContainer className="flex flex-col gap-4">
      <PageHeader
        breadcrumbs={[{ label: "On Call Schedules" }]}
        subheader="Define who is on-call at any given moment using rotation layers and overrides."
        actions={
          <Button onClick={handleOpen}>
            <Plus size={15} /> Add schedule
          </Button>
        }
      />

      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={columns} />
        ) : schedules.length === 0 ? (
          <Empty className="border-0 py-14">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <CalendarClock />
              </EmptyMedia>
              <EmptyTitle>No on-call schedules yet</EmptyTitle>
              <EmptyDescription>
                Create a schedule to define who is on-call using rotation layers and overrides.
              </EmptyDescription>
            </EmptyHeader>
            <Button onClick={handleOpen}>
              <Plus size={15} /> Add schedule
            </Button>
          </Empty>
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
                              onClick={(e) => { e.preventDefault(); if (page > 1) setPage((p) => p - 1); }}
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
                              onClick={(e) => { e.preventDefault(); if (page < totalPages) setPage((p) => p + 1); }}
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

      <CreateScheduleModal
        open={open}
        onOpenChange={(next) => { setOpen(next); if (!next) resetForm(); }}
        name={name}
        onNameChange={setName}
        description={description}
        onDescriptionChange={setDescription}
        timeZone={timeZone}
        onTimeZoneChange={setTimeZone}
        error={formError}
        isCreating={createMutation.isPending}
        onSubmit={() => { setFormError(""); createMutation.mutate(); }}
      />
    </PageContainer>
  );
}

export default OnCallSchedulesPage;
