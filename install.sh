#!/usr/bin/env bash
set -euo pipefail

# ── Piro installer ────────────────────────────────────────────────────────────
# Usage:
#   bash install.sh --mode api
#   bash install.sh --mode worker --api-url https://... --token <token>
#   bash install.sh --mode update

REPO_URL="https://github.com/Heva-Co/piro.git"
INSTALL_DIR="${PIRO_DIR:-$HOME/.piro}"

# ── Colors ────────────────────────────────────────────────────────────────────
if [ -t 1 ]; then
  BOLD="\033[1m"; GREEN="\033[0;32m"; YELLOW="\033[0;33m"; RED="\033[0;31m"; RESET="\033[0m"
else
  BOLD=""; GREEN=""; YELLOW=""; RED=""; RESET=""
fi

info()    { echo -e "${GREEN}✔${RESET} $*"; }
warn()    { echo -e "${YELLOW}⚠${RESET} $*"; }
heading() { echo -e "\n${BOLD}$*${RESET}"; }
die()     { echo -e "${RED}✖${RESET} $*" >&2; exit 1; }

# ── Parse arguments ───────────────────────────────────────────────────────────
MODE=""
API_URL=""
WORKER_TOKEN=""
WORKER_REGION="default"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)    MODE="$2";          shift 2 ;;
    --api-url) API_URL="$2";       shift 2 ;;
    --token)   WORKER_TOKEN="$2";  shift 2 ;;
    --region)  WORKER_REGION="$2"; shift 2 ;;
    --dir)     INSTALL_DIR="$2";   shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) die "Unknown argument: $1" ;;
  esac
done

usage() {
  cat <<EOF
Usage: install.sh --mode <api|worker|update> [options]

Modes:
  api       Install and start the full Piro stack
  worker    Run a worker connected to an existing API
  update    Pull latest images and restart

Options:
  --api-url   Worker mode: URL of your Piro API
  --token     Worker mode: worker token
  --region    Worker mode: region label (default: "default")
  --dir       Installation directory (default: ~/.piro)
EOF
}

[ -z "$MODE" ] && { usage; die ""; }
[[ "$MODE" =~ ^(api|worker|update)$ ]] || die "Invalid mode '$MODE'. Must be: api, worker, or update."

# ── Step 1: Check dependencies ────────────────────────────────────────────────
check_deps() {
  heading "Checking dependencies"

  # Git
  command -v git &>/dev/null || die "git is required but not installed."
  info "git $(git --version | awk '{print $3}')"

  # Docker
  if ! command -v docker &>/dev/null; then
    if [[ "$(uname -s)" == "Linux" ]]; then
      warn "Docker not found. Installing via get.docker.com..."
      curl -fsSL https://get.docker.com | sh
      info "Docker installed."
    else
      die "Docker is not installed. Download Docker Desktop: https://docs.docker.com/get-docker/"
    fi
  fi
  docker info &>/dev/null || die "Docker is not running. Start Docker and re-run this script."
  info "Docker $(docker --version | awk '{print $3}' | tr -d ',')"

  # Docker Compose v2
  if docker compose version &>/dev/null 2>&1; then
    info "Docker Compose $(docker compose version --short 2>/dev/null || echo 'v2')"
  elif command -v docker-compose &>/dev/null; then
    die "docker-compose v1 found. Piro requires Docker Compose v2: https://docs.docker.com/compose/install/"
  else
    die "Docker Compose v2 not found: https://docs.docker.com/compose/install/"
  fi
}

# ── Step 2: Clone repo ────────────────────────────────────────────────────────
clone_repo() {
  heading "Fetching Piro"

  if [ -d "$INSTALL_DIR/.git" ]; then
    info "Repository already present at $INSTALL_DIR — pulling latest..."
    git -C "$INSTALL_DIR" pull --ff-only --quiet
    return
  fi

  if [ -d "$INSTALL_DIR" ] && [ "$(ls -A "$INSTALL_DIR" 2>/dev/null)" ]; then
    die "Directory $INSTALL_DIR exists and is not empty. Remove it or use --dir to specify another location."
  fi

  info "Cloning repository into $INSTALL_DIR..."
  git clone --depth 1 --quiet "$REPO_URL" "$INSTALL_DIR" \
    || die "Clone failed. Make sure you have access to $REPO_URL (SSH key or GitHub credentials)."
  info "Repository cloned."
}

# ── Step 3: Configure ─────────────────────────────────────────────────────────
gen_secret() {
  local len="${1:-48}"
  if command -v openssl &>/dev/null; then
    openssl rand -base64 "$len" | tr -d '=/+' | head -c "$len"
  else
    tr -dc 'A-Za-z0-9' < /dev/urandom | head -c "$len"
  fi
}

configure() {
  heading "Configuration"

  if [ -f "$INSTALL_DIR/.env" ]; then
    warn ".env already exists — skipping. Edit $INSTALL_DIR/.env to change settings."
    ORIGIN_URL="$(grep '^ORIGIN=' "$INSTALL_DIR/.env" | cut -d= -f2- 2>/dev/null || echo 'http://localhost:3000')"
    return
  fi

  echo "Press Enter to accept the default shown in [brackets]."
  echo ""
  read -rp "  Public URL of your status page [http://localhost:3000]: " ORIGIN_INPUT
  ORIGIN_URL="${ORIGIN_INPUT:-http://localhost:3000}"
  echo ""

  local pg_pass jwt_secret
  pg_pass="$(gen_secret 32)"
  jwt_secret="$(gen_secret 48)"

  cat > "$INSTALL_DIR/.env" <<EOF
# Database
POSTGRES_PASSWORD=${pg_pass}

# Authentication — do not share this value
JWT_SECRET=${jwt_secret}

# Public URL of the frontend
ORIGIN=${ORIGIN_URL}

# Worker token — set after first login under Configuration → Workers
WORKER_TOKEN=

# Email alerts (optional)
# EMAIL_HOST=smtp.example.com
# EMAIL_PORT=587
# EMAIL_USERNAME=noreply@example.com
# EMAIL_PASSWORD=
# EMAIL_FROM=Piro <noreply@example.com>
EOF

  info ".env created at $INSTALL_DIR/.env"
}

# ── Step 4: Start stack ───────────────────────────────────────────────────────
start_stack() {
  heading "Starting Piro"

  cd "$INSTALL_DIR"

  # Start core services only — worker requires a token and uses a profile
  docker compose up -d --build db api frontend

  # If a worker token is already configured, start the worker too
  local worker_token
  worker_token="$(grep '^WORKER_TOKEN=' "$INSTALL_DIR/.env" | cut -d= -f2- | tr -d '[:space:]')"
  if [ -n "$worker_token" ]; then
    info "Worker token found — starting worker..."
    docker compose --profile worker up -d worker
  fi

  local setup_url="${ORIGIN_URL%/}/setup"
  echo ""
  echo -e "${GREEN}${BOLD}Piro is running!${RESET}"
  echo ""
  echo -e "  Complete setup at: ${BOLD}${setup_url}${RESET}"
  echo ""
  echo -e "  Installation: ${BOLD}$INSTALL_DIR${RESET}"
  echo -e "  Config:       ${BOLD}$INSTALL_DIR/.env${RESET}"
  echo ""
  if [ -z "$worker_token" ]; then
    echo -e "  To start a local worker after setup:"
    echo -e "    1. Go to Configuration → Workers → Register Worker and copy the token"
    echo -e "    2. Edit $INSTALL_DIR/.env and set WORKER_TOKEN=<your-token>"
    echo -e "    3. Run: docker compose --profile worker -f $INSTALL_DIR/docker-compose.yml up -d worker"
    echo ""
  fi
}

# ── Worker mode ───────────────────────────────────────────────────────────────
start_worker() {
  heading "Starting Piro Worker"

  [ -z "$API_URL" ]      && die "--api-url is required for worker mode."
  [ -z "$WORKER_TOKEN" ] && die "--token is required for worker mode."

  local image="ghcr.io/heva-co/piro-worker:latest"
  local name="piro-worker-${WORKER_REGION}"

  if docker ps -aq --filter "name=^${name}$" | grep -q .; then
    warn "Removing existing container ${name}..."
    docker rm -f "${name}"
  fi

  info "Pulling ${image}..."
  docker pull "${image}"

  docker run -d \
    --name "${name}" \
    --restart unless-stopped \
    -e "PIRO_API_URL=${API_URL%/}" \
    -e "PIRO_WORKER_TOKEN=${WORKER_TOKEN}" \
    -e "PIRO_WORKER_REGION=${WORKER_REGION}" \
    "${image}"

  echo ""
  echo -e "${GREEN}${BOLD}Worker started!${RESET}"
  echo ""
  echo -e "  Container: ${BOLD}${name}${RESET}  |  Region: ${BOLD}${WORKER_REGION}${RESET}"
  echo -e "  Logs: docker logs -f ${name}"
  echo ""
}

# ── Update mode ───────────────────────────────────────────────────────────────
update_stack() {
  heading "Updating Piro"

  [ -d "$INSTALL_DIR" ] || die "No installation found at $INSTALL_DIR. Run --mode api first."

  git -C "$INSTALL_DIR" pull --ff-only --quiet && info "Repository updated."

  cd "$INSTALL_DIR"
  docker compose pull
  docker compose up -d --build

  echo ""
  echo -e "${GREEN}${BOLD}Piro updated!${RESET}"
  echo ""
}

# ── Main ──────────────────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}Piro Installer${RESET}"
echo "────────────────────────────────────────"

case "$MODE" in
  api)
    check_deps
    clone_repo
    configure
    start_stack
    ;;
  worker)
    check_deps
    start_worker
    ;;
  update)
    check_deps
    update_stack
    ;;
esac
