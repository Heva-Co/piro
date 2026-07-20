import { Badge } from "@/components/ui/badge";
import type { PostmortemStatus } from "@/lib/actions/postmortems";

interface Props {
  status: PostmortemStatus;
}

// "Published" here means the review is finalized (internal-only) — NOT public status-page
// visibility. See PostmortemStatus.cs on the backend.
function PostmortemStatusBadge(props: Props) {
  const { status } = props;
  const isPublished = status === "Published";
  return (
    <Badge variant={isPublished ? "default" : "secondary"}>
      {isPublished ? "Finalized" : "Draft"}
    </Badge>
  );
}

export default PostmortemStatusBadge;
