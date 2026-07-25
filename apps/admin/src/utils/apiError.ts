import axios from "axios";

/**
 * Pulls a human-readable message out of an API error — the ProblemDetails
 * `title`/`detail` when it's an axios error, otherwise the given fallback.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}
