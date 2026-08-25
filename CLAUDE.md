# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

EmployMe (working title) — a personal job vacancy aggregator that ingests postings from employers' own ATS boards (Greenhouse, Lever, Ashby) and public remote-job APIs (Himalayas, Jobicy, Arbeitnow), ranks them against the author's own CV via semantic embeddings, deduplicates cross-source postings, and tracks the application pipeline (viewed → applied → interview → rejected → offer). Built as a portfolio project that also drives the author's actual job search — see `README.md` for the pitch and `PLAN.md` for the phased roadmap and current status.

**Current state:** Phase 0 is done; Phase I is in progress. `src/Api` (ASP.NET Core Web API, EF Core, targets `net10.0` to match the Dev Container's SDK — see the note below if you see it targeting `net8.0` again) has the Postgres schema for `vacancies`/`sources`/`applications`/`embeddings` and a `VacanciesController` (`GET /api/vacancies` with keyword/location/date filters, `GET /api/vacancies/{id}`). `src/Web` is scaffolded (React + TS + Vite) with a basic vacancy list + filter form calling the API through the dev-server proxy. **There is no ingest path yet** — the hh.ru client and ingest endpoint were removed when that source was disqualified (see below); the source-agnostic replacement is EM-52. Not yet done: Railway deployment (EM-17). Check `PLAN.md`'s checkboxes for what's actually been done before assuming a feature/service exists.

**hh.ru is disqualified — Tier D, do not re-add.** Falsified on two independent grounds: `403` to unauthorized callers since April 2026, *and* a developer agreement forbidding transfer of retrieved data to third-party services. The legal ground survives any technical workaround, and this project's premise is a public deployment. Post-mortem: Linear EM-9.

## Intended architecture (per PLAN.md, not yet all implemented)

- **Backend**: ASP.NET Core Web API + EF Core, at `src/Api`.
- **Database**: PostgreSQL with the `pgvector` extension, run as the `db` compose service.
- **Embeddings**: Ollama (bge-m3 / e5 models) run as the `ollama` compose service; embeddings for vacancies and CV are compared via cosine similarity for the fit-score.
- **Frontend**: React + TypeScript via Vite, at `src/Web`.
- **Scheduling**: `BackgroundService` / Hangfire for scheduled ingest (Phase II+).
- **Resilience**: `HttpClient` + Polly for external API calls. Poll intervals come from the source row, never a constant — Jobicy caps polling at once per hour.
- **Testing**: xUnit for unit tests, Testcontainers for integration tests against a real Postgres.
- **Deployment**: Railway. **Error monitoring**: Sentry. **CI**: GitHub Actions, intended to build on the same base image as the Dev Container so local and CI stay identical.

Data flows: external sources → background ingest service → Postgres (+pgvector) ← Ollama embeddings; LLM structured extraction writes into Postgres too → ASP.NET Core API → React frontend.

Follow the phase gate in `PLAN.md`: each phase must be working (and, where applicable, deployed) before starting the next — don't build Phase III (semantic layer) work ahead of a deployed Phase I, for example.

## Dev environment

This project is developed inside a Dev Container (`.devcontainer/`) — do not assume tools are available outside it.

- `devcontainer.json` provisions .NET 10 SDK, Node 24, GitHub CLI, and Claude Code (via `ghcr.io/anthropics/devcontainer-features/claude-code`) as devcontainer features.
- `docker-compose.yml` defines three services: `app` (the workspace container), `db` (`pgvector/pgvector:pg18`, port 5432, user/pass `postgres`/`postgres`, database `employme`), and `ollama` (port 11434). **The `db` service's named volume mounts at `/var/lib/postgresql`, not the old `/var/lib/postgresql/data` convention** — Postgres 18's official image relocated `PGDATA` to a version-specific path (`/var/lib/postgresql/18/docker`) and changed its declared `VOLUME` to the parent dir to support fast `pg_upgrade` via hard-links. Mounting at the old `.../data` path leaves the volume unused, so the healthcheck never passes and `depends_on: condition: service_healthy` blocks forever — "Rebuild Container" hangs with no clear error. Don't revert this to `.../data`.
- `post-create.sh` runs once on container creation: fixes bind-mount/volume ownership for the `vscode` user, copies `.env.example` → `.env` if missing, runs `dotnet tool update --global dotnet-ef`, and runs `npm install` in `src/Web` if it exists.
- `init-firewall.sh` runs on every container start (`postStartCommand`, via `sudo`) and applies a default-deny outbound firewall with an explicit domain allowlist (GitHub, npm, NuGet, Anthropic API, Sentry, and the MCP endpoints `mcp.linear.app` / `mcp.slack.com` / `mcp.sentry.dev` / `mcp.railway.com`). **If a new external dependency or MCP server is added, its domain must be added to the `for domain in ...` list in this script**, or outbound traffic to it will be silently rejected.
- Config: copy `.env.example` to `.env` (gitignored) and fill in `SENTRY_DSN` and connection strings. Defaults in `.env.example` target `localhost`; the `app` container instead gets `db`/`ollama` hostnames injected via `docker-compose.yml`'s `environment:` block.
- **Keep every `.csproj`'s `TargetFramework` on `net10.0`, matching the SDK `devcontainer.json` provisions.** Only the .NET 10 SDK/runtime is installed (no `net8.0` runtime) — a project targeting `net8.0` builds fine (compiling only needs reference assemblies) but fails at `dotnet run` with "You must install or update .NET to run this application." `src/Api/Api.csproj` hit exactly this after being scaffolded before the SDK was bumped from 8 to 10; it's been retargeted and its EF Core/Npgsql/Swashbuckle packages bumped to versions that actually support `net10.0` (Swashbuckle 6.6.2 throws a `TypeLoadException` on `net10.0` — only a `dotnet run`, not `dotnet build`, surfaces that, so build success alone doesn't prove a package set is compatible).
- **NuGet/npm/marketplace CDN domains in the firewall allowlist can be intermittently unreachable even though they're on the list.** `init-firewall.sh` resolves each domain to whatever IP(s) `dig` returns *once*, at container start, and allowlists only those. Domains like `api.nuget.org` are served by Akamai/Fastly with many rotating edge IPs, so a later request can land on an IP outside that snapshot and get dropped ("Network is unreachable" / `EHOSTUNREACH`) even though the domain itself is allowed. This has been observed causing a `dotnet restore` to fail outright. It's usually transient — retry the command a few times, or re-run `sudo bash .devcontainer/init-firewall.sh` to refresh the snapshot — not a sign the allowlist is missing the domain.


## Commands

```bash
# Backend — build/run/migrate
dotnet build EmployMe.sln
dotnet run --project src/Api
dotnet ef database update --project src/Api   # dotnet-ef installed globally by post-create.sh

# Frontend — dev server proxies /api/* to http://localhost:5000 (see src/Web/vite.config.ts),
# so run the API alongside it for vacancy data to load.
cd src/Web && npm install && npm run dev
```

No test project exists yet — that's Phase II (xUnit + Testcontainers per `PLAN.md`).

## Tooling map (inside vs. outside the Dev Container)

From `PLAN.md` — worth checking before assuming something needs to be installed or configured locally:

- **Inside**: .NET SDK, Node.js, Postgres+pgvector, Ollama, git/GitHub CLI/`dotnet-ef`, Claude Code.
- **Outside**: Railway (deploy target, pushed to via CLI/CI — not run in-container), Sentry/Linear/Slack (SaaS, only API keys/DSNs are stored as env vars), the MCP endpoints for those services (remote servers a Claude client connects to), GitHub Actions (hosted runners), and Claude-adjacent tools (Cowork, Claude Design, Claude in Chrome) that live entirely outside the repo.
