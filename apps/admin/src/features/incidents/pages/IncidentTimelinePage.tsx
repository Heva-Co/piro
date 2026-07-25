import { useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useInfiniteQuery } from "@tanstack/react-query";
import { CheckCheck, MessageSquareText, Globe, Lock, FlagTriangleRight } from "lucide-react";
import { marked } from "marked";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { Badge } from "@/components/ui/badge";
import { Bubble, BubbleContent } from "@/components/ui/bubble";
import { Message, MessageContent, MessageHeader } from "@/components/ui/message";
import { Marker, MarkerIcon, MarkerContent } from "@/components/ui/marker";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Empty, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { incidentsApi } from "@/lib/actions/incidents";
import { ROUTES } from "@/constants/routes";
import { QUERY_KEYS } from "@/constants/api";
import IncidentStatusBadge from "../components/IncidentStatusBadge";
import { SYSTEM_EVENT_ICON, describeSystemEvent } from "../components/incidentTimeline";

const TIMELINE_PAGE_SIZE = 20;

function IncidentTimelinePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { formatTimestamp, formatDateTime } = useFormattedDate();

  const { data: incident, isLoading } = useQuery({
    queryKey: QUERY_KEYS.INCIDENT(id!),
    queryFn: () => incidentsApi.get(id!),
  });

  const {
    data: timelinePages,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteQuery({
    queryKey: [...QUERY_KEYS.INCIDENT(id!), "timeline"],
    queryFn: ({ pageParam }) => incidentsApi.getTimeline(id!, pageParam, TIMELINE_PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
    enabled: !!id,
  });

  const sentinelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage();
        }
      },
      { threshold: 0.1 }
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [fetchNextPage, hasNextPage, isFetchingNextPage]);

  if (isLoading) {
    return (
      <PageContainer>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </PageContainer>
    );
  }
  if (!incident) {
    return (
      <PageContainer>
        <div className="text-sm text-destructive">Incident not found.</div>
      </PageContainer>
    );
  }

  // Backend already returns events most-recent-first, one page at a time.
  const timeline = timelinePages?.pages.flatMap((p) => p.items) ?? [];
  const isPublic = incident.visibility === "Public";
  const isResolved = incident.status === "Resolved" || incident.isResolved;

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "Incidents", onClick: () => navigate(ROUTES.INCIDENTS.LIST) },
          { label: `#${incident.id}`, onClick: () => navigate(ROUTES.INCIDENTS.DETAIL(incident.id)) },
          { label: "Timeline" },
        ]}
        actions={
          <>
            <IncidentStatusBadge status={incident.status} />
            {isPublic ? (
              <Badge variant="outline" className="gap-1 text-green-600 dark:text-green-400">
                <Globe data-icon="inline-start" /> Public
              </Badge>
            ) : (
              <Badge variant="outline" className="gap-1 text-muted-foreground">
                <Lock data-icon="inline-start" /> Private
              </Badge>
            )}
            {!isResolved && incident.acknowledgedAt && (
              <Badge className="gap-1.5 border-green-500/30 bg-green-500/10 px-3 py-1.5 text-green-600 dark:text-green-400">
                <CheckCheck size={13} />
                <span>Acked by <strong>{incident.acknowledgedBy}</strong></span>
              </Badge>
            )}
          </>
        }
      />

      <div className="rounded-xl border border-border bg-card px-6 py-5 mb-4">
        <h1 className="text-xl font-bold">{incident.title}</h1>
        <div className="flex items-center gap-2 mt-2 flex-wrap text-xs text-muted-foreground">
          <span>Started {formatTimestamp(incident.startDateTime)}</span>
          {incident.endDateTime && <span>· Resolved {formatTimestamp(incident.endDateTime)}</span>}
        </div>
      </div>

      <div className="rounded-xl border border-border bg-card">
        {/* `<main>` in AdminLayout is already the page's own scroll container (overflow-y-auto),
            so this height is a fixed cap relative to the viewport, not 100vh minus chrome —
            using 100vh here would exceed the space <main> actually has, causing double scrollbars. */}
        <ScrollArea className="h-[70vh]">
          <div className="flex flex-col gap-4 p-6">
            {timeline.length === 0 ? (
              <Empty className="border-0 py-8">
                <EmptyHeader>
                  <EmptyTitle className="text-muted-foreground font-normal">No events yet.</EmptyTitle>
                </EmptyHeader>
              </Empty>
            ) : (
              timeline.map((e, i) => {
                const prev = timeline[i - 1];
                const tightSpacing = i > 0 && prev.type !== "CommentPosted" && e.type !== "CommentPosted";

                if (e.type === "CommentPosted") {
                  return (
                    <Message key={e.id} align="start">
                      <MessageContent>
                        <MessageHeader className="gap-1.5 px-0">
                          <MessageSquareText className="size-3.5 text-muted-foreground" />
                          <span>{formatDateTime(e.occurredAt)}</span>
                          {e.visibility === "Public" && (
                            <Badge variant="outline" className="gap-1 text-green-600 dark:text-green-400">
                              <Globe data-icon="inline-start" /> Public
                            </Badge>
                          )}
                        </MessageHeader>
                        <Bubble variant="muted" align="start">
                          <BubbleContent>
                            <div
                              className="prose prose-sm max-w-none"
                              dangerouslySetInnerHTML={{ __html: marked(e.comment ?? "", { async: false }) as string }}
                            />
                          </BubbleContent>
                        </Bubble>
                      </MessageContent>
                    </Message>
                  );
                }

                return (
                  <Marker key={e.id} variant="separator" className={tightSpacing ? "-mt-1" : undefined}>
                    <MarkerContent className="flex items-center gap-1.5">
                      <MarkerIcon>{SYSTEM_EVENT_ICON[e.type] ?? <FlagTriangleRight />}</MarkerIcon>
                      {describeSystemEvent(e)}
                      <span className="text-muted-foreground/70">· {formatDateTime(e.occurredAt)}</span>
                    </MarkerContent>
                  </Marker>
                );
              })
            )}
            {/* Infinite scroll sentinel — fetches the next page once it enters the viewport. */}
            <div ref={sentinelRef} className="h-px" />
            {isFetchingNextPage && (
              <p className="py-2 text-center text-xs text-muted-foreground">Loading more…</p>
            )}
          </div>
        </ScrollArea>
      </div>
    </PageContainer>
  );
}

export default IncidentTimelinePage;
