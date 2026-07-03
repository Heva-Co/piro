#!/usr/bin/env bash
# echo-server.sh — start the local echo HTTP server for testing Piro checks
# Usage: ./scripts/echo-server.sh [port]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

node "$REPO_ROOT/scripts/echo-server.js" "$@"
