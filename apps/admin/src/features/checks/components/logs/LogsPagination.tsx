import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";

interface Props {
  page: number;
  totalPages: number;
  totalEntries: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}

function LogsPagination(props: Props) {
  const { page, totalPages, totalEntries, pageSize, onPageChange } = props;

  // Window of up to 7 page numbers centered on the current page.
  const windowSize = Math.min(7, totalPages);
  const start = totalPages > 7 ? Math.max(1, Math.min(page - 3, totalPages - 6)) : 1;
  const pages = Array.from({ length: windowSize }, (_, i) => start + i);

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalEntries);

  return (
    <div className="flex items-center justify-between text-sm text-muted-foreground">
      <span>
        Showing {from}–{to} of {totalEntries} entries
      </span>
      <Pagination className="mx-0 w-auto justify-end">
        <PaginationContent>
          <PaginationItem>
            <PaginationPrevious
              href="#"
              aria-disabled={page === 1}
              className={page === 1 ? "pointer-events-none opacity-40" : undefined}
              onClick={(e) => {
                e.preventDefault();
                if (page > 1) onPageChange(page - 1);
              }}
            />
          </PaginationItem>

          {pages.map((p) => (
            <PaginationItem key={p}>
              <PaginationLink
                href="#"
                isActive={p === page}
                onClick={(e) => {
                  e.preventDefault();
                  onPageChange(p);
                }}
              >
                {p}
              </PaginationLink>
            </PaginationItem>
          ))}

          <PaginationItem>
            <PaginationNext
              href="#"
              aria-disabled={page === totalPages}
              className={page === totalPages ? "pointer-events-none opacity-40" : undefined}
              onClick={(e) => {
                e.preventDefault();
                if (page < totalPages) onPageChange(page + 1);
              }}
            />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
    </div>
  );
}

export default LogsPagination;
