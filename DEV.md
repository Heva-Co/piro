# Piro — Dev Setup

## Quick start (local, no Docker)

### 1. API

```bash
cd src/Piro.Api
dotnet run
# Listening on http://localhost:5117
```

SQLite DB is created automatically at `piro.db` on first run.

On first request the API returns `{ "setup_required": true }` — complete setup via the frontend.

### 2. Frontend

```bash
cd frontend
cp .env.example .env        # PIRO_API_URL defaults to http://localhost:5117
npm install
npm run dev
# http://localhost:5173
```

Open http://localhost:5173 → you'll be redirected to `/setup` to create the owner account.

---

## Quick start (Docker Compose)

```bash
docker compose up --build
```

| Service  | URL                    |
|----------|------------------------|
| Frontend | http://localhost:3000  |
| API      | http://localhost:5117  |
| Postgres | localhost:5432         |

On first startup navigate to http://localhost:3000/setup.

---

## Testing the flow

1. **Setup** — `/setup`: set instance name + create owner account
2. **Sign in** — `/auth/sign-in`
3. **Create a service** — `/admin/services` → "+ Add service"
4. **Add a check** — click the service → "+ Add check" (use HTTP type with a real URL)
5. **Run check immediately** — click "Run now" on the check row
6. **Status page** — navigate to `/` to see the service status
7. **Create incident** — `/admin/incidents` → "+ New incident"
8. **Schedule maintenance** — `/admin/maintenances` → "+ Schedule maintenance"

---

## Run unit tests

```bash
cd tests/Piro.UnitTests
dotnet test
```

21 tests covering status propagation, cycle detection, and DAG cascade.

---

## Environment variables

| Variable | Default | Description |
|---|---|---|
| `Database__ConnectionString` | `Data Source=piro.db` | SQLite or Postgres connection string |
| `Auth__JwtSecret` | (generated) | Secret for JWT signing — set explicitly in production |
| `PIRO_API_URL` | `http://localhost:5117` | Frontend → API base URL |
