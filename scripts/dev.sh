#!/usr/bin/env bash
# dev.sh — start API + apps/web locally for development
# Usage: ./scripts/dev.sh [--api-only | --web-only]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

start_api() {
  echo "▶  Starting API (http://localhost:5117)..."
  cd "$REPO_ROOT/src/Piro.Api"
  ASPNETCORE_ENVIRONMENT=Development dotnet run &
  API_PID=$!
  echo "   API PID: $API_PID"
}

start_web() {
  echo "▶  Starting web (http://localhost:3000)..."
  cd "$REPO_ROOT/apps/web"
  pnpm dev &
  WEB_PID=$!
  echo "   Web PID: $WEB_PID"
}

cleanup() {
  echo ""
  echo "⏹  Stopping services..."
  [[ -n "${API_PID:-}" ]] && kill "$API_PID" 2>/dev/null || true
  [[ -n "${WEB_PID:-}" ]] && kill "$WEB_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

API_PID=""
WEB_PID=""

case "${1:-}" in
  --api-only)  start_api ;;
  --web-only)  start_web ;;
  *)           start_api; start_web ;;
esac

echo ""
echo "✓  Services started. Press Ctrl+C to stop."
echo ""
echo "   API  → http://localhost:5117"
[[ -z "${1:-}" || "${1:-}" == "--web-only" ]] && echo "   Web  → http://localhost:3000"
echo ""

wait
