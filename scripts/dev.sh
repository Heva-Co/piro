#!/usr/bin/env bash
# dev.sh — start API + apps/web + apps/admin locally for development
# Usage: ./scripts/dev.sh [--api-only | --web-only | --admin-only | --frontend-only]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

WEB_PORT=3000
ADMIN_PORT=5174

build_api() {
  echo "▶  Building API..."
  cd "$REPO_ROOT"
  dotnet build src/Piro.Api/Piro.Api.csproj -c Debug || exit 1
}

start_api() {
  echo "▶  Starting API (http://localhost:5117)..."
  cd "$REPO_ROOT/src/Piro.Api"
  ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build &
  API_PID=$!
  echo "   API PID: $API_PID"
}

start_web() {
  echo "▶  Starting web (http://localhost:3000)..."
  cd "$REPO_ROOT/apps/web"
  pnpm dev -p $WEB_PORT &
  WEB_PID=$!
  echo "   Web PID: $WEB_PID"
}

start_admin() {
  echo "▶  Starting admin (http://localhost:5174)..."
  cd "$REPO_ROOT/apps/admin"
  pnpm dev --port $ADMIN_PORT &
  ADMIN_PID=$!
  echo "   Admin PID: $ADMIN_PID"
}

cleanup() {
  echo ""
  echo "⏹  Stopping services..."
  [[ -n "${API_PID:-}"   ]] && kill "$API_PID"   2>/dev/null || true
  [[ -n "${WEB_PID:-}"   ]] && kill "$WEB_PID"   2>/dev/null || true
  [[ -n "${ADMIN_PID:-}" ]] && kill "$ADMIN_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

API_PID=""
WEB_PID=""
ADMIN_PID=""

case "${1:-}" in
  --api-only)      build_api; start_api ;;
  --web-only)      start_web ;;
  --admin-only)    start_admin ;;
  --frontend-only) start_web; start_admin ;;
  *)               build_api; start_api; start_web; start_admin ;;
esac

echo ""
echo "✓  Services started. Press Ctrl+C to stop."
echo ""
[[ -z "${1:-}" || "${1:-}" == "--api-only"                        ]] && echo "   API   → http://localhost:5117"
[[ -z "${1:-}" || "${1:-}" == "--web-only"   || "${1:-}" == "--frontend-only" ]] && echo "   Web   → http://localhost:${WEB_PORT}"
[[ -z "${1:-}" || "${1:-}" == "--admin-only" || "${1:-}" == "--frontend-only" ]] && echo "   Admin → http://localhost:${ADMIN_PORT}/admin"
echo ""

wait
