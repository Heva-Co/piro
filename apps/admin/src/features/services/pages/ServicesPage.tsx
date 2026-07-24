import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Blend, Filter, Plus, Settings } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { StatusPill } from "@/components/StatusBadge";
import TableSkeleton from "@/components/TableSkeleton";
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationPrevious,
  PaginationNext,
} from "@/components/ui/pagination";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription, EmptyContent } from "@/components/ui/empty";
import { Button } from "@/components/ui/button";
import { useServices } from "@/hooks/useServices";
import { ROUTES } from "@/constants/routes";

const PAGE_SIZE = 20;

const columns = ["Service", "Slug", "Status", "Hidden", "Checks"];

function initials(name: string) {
  const words = name.trim().split(/\s+/);
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[1][0]).toUpperCase();
}

export default function ServicesPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const { data, isLoading, isError } = useServices({ page, pageSize: PAGE_SIZE });

  const services = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="flex flex-col">
      <PageHeader
        breadcrumbs={[{ label: "Services" }]}
        subheader="The systems you monitor. Each service groups its checks and surfaces their combined status."
        actions={
          <button
            onClick={() => navigate(ROUTES.SERVICES.NEW)}
            className="flex items-center gap-1.5 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={14} /> New Service
          </button>
        }
      />

      <div className="rounded-xl border bg-card overflow-hidden">
        <div className="px-4 py-3 border-b flex items-center">
          <button className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors">
            <Filter size={13} /> Filters
          </button>
        </div>

        {isLoading ? (
          <TableSkeleton columns={columns} />
        ) : isError ? (
          <div className="px-6 py-8 text-sm text-destructive">Failed to load services.</div>
        ) : services.length === 0 ? (
          <Empty className="py-20">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Blend />
              </EmptyMedia>
              <EmptyTitle>No services yet</EmptyTitle>
              <EmptyDescription>Add your first service to start monitoring uptime.</EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button onClick={() => navigate(ROUTES.SERVICES.NEW)}>
                <Plus size={14} /> New Service
              </Button>
            </EmptyContent>
          </Empty>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {services.map((service) => (
                <TableRow
                  key={service.slug}
                  className="hover:bg-muted/30 transition-colors cursor-pointer"
                  onClick={() => navigate(ROUTES.SERVICES.DETAIL(service.slug))}
                >
                  <TableCell>
                    <div className="flex items-center gap-3">
                      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-muted-foreground">
                        {initials(service.name)}
                      </div>
                      <span className="font-medium">{service.name}</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <code className="rounded border bg-muted px-2 py-0.5 text-xs font-mono">
                      {service.slug}
                    </code>
                  </TableCell>
                  <TableCell>
                    <StatusPill status={service.currentStatus} />
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {service.isHidden ? "YES" : "NO"}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{service.checkCount ?? "—"}</TableCell>
                  <TableCell className="text-right">
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        navigate(ROUTES.SERVICES.DETAIL(service.slug));
                      }}
                      className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted transition-colors"
                    >
                      <Settings size={13} /> Configure
                    </button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
            <TableFooter>
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={columns.length + 1} className="px-4 py-3">
                  <div className="flex items-center justify-between text-sm font-normal">
                    <span className="text-muted-foreground">
                      Page {page} of {totalPages} · {totalCount} service{totalCount === 1 ? "" : "s"}
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
          </Table>
        )}
      </div>
    </div>
  );
}
