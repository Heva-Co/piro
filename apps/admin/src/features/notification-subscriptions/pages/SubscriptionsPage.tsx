import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { toast } from "react-toastify";
import { Plus, BellRing, Trash2 } from "lucide-react";
import { notificationSubscriptionsApi } from "@/lib/actions/notification-subscriptions";
import type {
  NotificationSubscription,
  UpsertNotificationSubscriptionRequest,
} from "@/lib/actions/notification-subscriptions";
import { QUERY_KEYS } from "@/constants/api";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
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
import SubscriptionFormModal from "../components/SubscriptionFormModal";

const PAGE_SIZE = 15;
const columns = ["Name", "Destination", "Events", "Min severity", "Status", ""];

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function destinationLabel(s: NotificationSubscription): string {
  if (s.userName) return `${s.userName} (person)`;
  if (s.integrationName) return `${s.integrationName}${s.target ? ` · ${s.target}` : ""}`;
  return "—";
}

function SubscriptionsPage() {
  const qc = useQueryClient();

  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<NotificationSubscription | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: [...QUERY_KEYS.NOTIFICATION_SUBSCRIPTIONS, page],
    queryFn: () => notificationSubscriptionsApi.list({ page, pageSize: PAGE_SIZE }),
  });
  const subscriptions = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const saveMutation = useMutation({
    mutationFn: (request: UpsertNotificationSubscriptionRequest) =>
      editing
        ? notificationSubscriptionsApi.update(editing.id, request)
        : notificationSubscriptionsApi.create(request),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.NOTIFICATION_SUBSCRIPTIONS });
      closeModal();
    },
    onError: (err) => setFormError(apiErrorMessage(err, "Failed to save subscription.")),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => notificationSubscriptionsApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.NOTIFICATION_SUBSCRIPTIONS }),
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to delete subscription.")),
  });

  function openCreate() {
    setEditing(null);
    setFormError(null);
    setModalOpen(true);
  }

  function openEdit(s: NotificationSubscription) {
    setEditing(s);
    setFormError(null);
    setModalOpen(true);
  }

  function closeModal() {
    setModalOpen(false);
    setEditing(null);
    setFormError(null);
  }

  return (
    <>
      <div className="flex flex-col gap-4">
        <PageHeader
          breadcrumbs={[{ label: "Event Subscriptions" }]}
          subheader="Route alert and incident events to people, team channels, and integrations."
          actions={
            <Button onClick={openCreate}>
              <Plus size={15} /> New subscription
            </Button>
          }
        />

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {isLoading ? (
            <TableSkeleton columns={columns} />
          ) : subscriptions.length === 0 ? (
            <div className="p-8 text-center text-sm text-muted-foreground">No subscriptions yet.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  {columns.map((column, i) => <TableHead key={column || i}>{column}</TableHead>)}
                </TableRow>
              </TableHeader>
              <TableBody>
                {subscriptions.map((s) => (
                  <TableRow key={s.id} className="hover:bg-muted/50 cursor-pointer" onClick={() => openEdit(s)}>
                    <TableCell className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <BellRing size={14} className="text-muted-foreground shrink-0" />
                        <span className="font-medium text-foreground">{s.name}</span>
                      </div>
                    </TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">{destinationLabel(s)}</TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">{s.events.join(", ")}</TableCell>
                    <TableCell className="px-5 py-3.5 text-muted-foreground text-xs">{s.minSeverity}</TableCell>
                    <TableCell className="px-5 py-3.5">
                      <Badge variant={s.enabled ? "default" : "secondary"}>{s.enabled ? "Enabled" : "Disabled"}</Badge>
                    </TableCell>
                    <TableCell className="px-5 py-3.5 text-right">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={(e) => {
                          e.stopPropagation();
                          if (confirm(`Delete subscription "${s.name}"?`)) deleteMutation.mutate(s.id);
                        }}
                      >
                        <Trash2 size={14} className="text-muted-foreground" />
                      </Button>
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
                          Page {page} of {totalPages} · {totalCount} subscription{totalCount === 1 ? "" : "s"}
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
      </div>

      {modalOpen && (
        <SubscriptionFormModal
          existing={editing}
          saving={saveMutation.isPending}
          error={formError}
          onCancel={closeModal}
          onSubmit={(request) => { setFormError(null); saveMutation.mutate(request); }}
        />
      )}
    </>
  );
}

export default SubscriptionsPage;
