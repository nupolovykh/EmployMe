# CLAUDE.md

Guidance for Claude Code (claude.ai/code) working in this repository.

This file is the entry point, not the whole story: it carries the hard rules and the
environment gotchas that break a build if you don't know them. Everything narrative lives in
`docs/`.

## Documents

Read the one that governs what you are about to touch. These are binding, not background.

| Document | Governs |
|---|---|
| [`docs/CONVENTIONS.md`](./docs/CONVENTIONS.md) | **Naming — branches, commits, PRs, issues. Follow it for anything you name.** |
| [`docs/PLAN.md`](./docs/PLAN.md) | Phased roadmap, §01 process rules, per-phase exit criteria. Its checkboxes are the source of truth for what is actually done. |
| [`docs/SOURCES.md`](./docs/SOURCES.md) | Source registry: tiers, endpoints, auth, rate limits, terms of use, verification level, disqualified sources. |
| [`docs/ASSUMPTIONS.md`](./docs/ASSUMPTIONS.md) | Assumption register: every load-bearing claim with a verification level (`assumed` → `docs` → `spike` → `live`), blast radius, fallback, expiry. |
| [`README.md`](./README.md) | The pitch, for a human landing on the repo. |

`docs/SOURCES.md` and `docs/ASSUMPTIONS.md` must be updated alongside any source work — a source
change that touches neither is incomplete.

## Project

EmployMe (working title) — a personal job vacancy aggregator that ingests postings from employers'
own ATS boards (Tier A: Greenhouse, Lever, Ashby, Workable, Recruitee, Personio) and public
remote-job APIs (Tier B: Himalayas, Jobicy, Remotive, RemoteOK, Arbeitnow), ranks them against the
author's own CV via semantic embeddings, deduplicates cross-source postings, and tracks the
application pipeline (viewed → applied → interview → rejected → offer). A portfolio project that
also drives the author's actual job search.

Layout: `src/Api` (ASP.NET Core Web API + EF Core), `src/Web` (React + TS + Vite), `spikes/<source>/`
(committed live responses that qualify a source), `docs/` (everything above). Postgres with
`pgvector` and Ollama run as compose services. Railway is the deploy target, Sentry the error
monitor, Linear the backlog — all outside the container; see `docs/PLAN.md`'s tooling map.

## Hard rules

- **hh.ru is Tier D: never re-add it, and never enable a Tier D source in a deployed environment.**
  `docs/PLAN.md` is at Revision 2 because Revision 1 was built on hh.ru and falsified — 403 to
  unauthorized callers since April 2026, *and* a developer agreement forbidding transfer of
  retrieved data to third parties, a legal ground that survives any technical workaround. The
  post-mortem is Linear EM-9 and `docs/ASSUMPTIONS.md` entry A-000.
- **Follow the phase gate.** Each phase must be working — and where applicable deployed — before
  the next starts. Don't build Phase III semantic-layer work ahead of a deployed Phase I.
- **§01 process rules bind Claude too.** Evidence over assertion: a claim about an external service
  needs a URL, a date and a live response. No integration is scheduled without a committed
  `spikes/<source>/response.json` and a terms-of-use verdict. A checkbox needs a link to a commit,
  PR or CI run — unpushed work is In Review, not Done. No phase may depend on a single external
  source (N≥3).
- **Poll intervals come from `sources.min_poll_interval`, never a constant.** Jobicy caps polling
  at once per hour and ignoring it gets the project banned.
- **Never assume a feature or service exists.** Check `docs/PLAN.md`'s checkboxes first.

## Dev environment

This project is developed inside a Dev Container (`.devcontainer/`) — do not assume tools are available outside it.

- `devcontainer.json` provisions .NET 10 SDK, Node 24, GitHub CLI, and Claude Code (via `ghcr.io/anthropics/devcontainer-features/claude-code`) as devcontainer features.
- `docker-compose.yml` defines three services: `app` (the workspace container), `db` (`pgvector/pgvector:pg18`, port 5432, user/pass `postgres`/`postgres`, database `employme`), and `ollama` (port 11434). **The `db` service's named volume mounts at `/var/lib/postgresql`, not the old `/var/lib/postgresql/data` convention** — Postgres 18's official image relocated `PGDATA` to a version-specific path (`/var/lib/postgresql/18/docker`) and changed its declared `VOLUME` to the parent dir to support fast `pg_upgrade` via hard-links. Mounting at the old `.../data` path leaves the volume unused, so the healthcheck never passes and `depends_on: condition: service_healthy` blocks forever — "Rebuild Container" hangs with no clear error. Don't revert this to `.../data`.
- `post-create.sh` runs once on container creation: fixes bind-mount/volume ownership for the `vscode` user, copies `.env.example` → `.env` if missing, runs `dotnet tool update --global dotnet-ef`, and runs `npm install` in `src/Web` if it exists.
- `init-firewall.sh` runs on every container start (`postStartCommand`, via `sudo`) and applies a default-deny outbound firewall with an explicit domain allowlist (GitHub, npm, NuGet, Anthropic API, Sentry, and the MCP endpoints `mcp.linear.app` / `mcp.slack.com` / `mcp.sentry.dev` / `mcp.railway.com`). **If a new external dependency or MCP server is added, its domain must be added to the `for domain in ...` list in this script**, or outbound traffic to it will be silently rejected. This applies to every source domain in `docs/SOURCES.md` — a spike against a source whose domain is not allowlisted fails as a network error, which is easy to misread as the source being unavailable.
- Config: copy `.env.example` to `.env` (gitignored) and fill in `SENTRY_DSN` and connection strings. The Tier A and Tier B sources in the MVP are public and keyless, so no source credentials exist. Defaults in `.env.example` target `localhost`; the `app` container instead gets `db`/`ollama` hostnames injected via `docker-compose.yml`'s `environment:` block.
- **Keep every `.csproj`'s `TargetFramework` on `net10.0`, matching the SDK `devcontainer.json` provisions.** Only the .NET 10 SDK/runtime is installed (no `net8.0` runtime) — a project targeting `net8.0` builds fine (compiling only needs reference assemblies) but fails at `dotnet run` with "You must install or update .NET to run this application." `src/Api/Api.csproj` hit exactly this after being scaffolded before the SDK was bumped from 8 to 10; it's been retargeted and its EF Core/Npgsql/Swashbuckle packages bumped to versions that actually support `net10.0` (Swashbuckle 6.6.2 throws a `TypeLoadException` on `net10.0` — only a `dotnet run`, not `dotnet build`, surfaces that, so build success alone doesn't prove a package set is compatible).
- **NuGet/npm/marketplace CDN domains in the firewall allowlist can be intermittently unreachable even though they're on the list.** `init-firewall.sh` resolves each domain to whatever IP(s) `dig` returns *once*, at container start, and allowlists only those. Domains like `api.nuget.org` are served by Akamai/Fastly with many rotating edge IPs, so a later request can land on an IP outside that snapshot and get dropped ("Network is unreachable" / `EHOSTUNREACH`) even though the domain itself is allowed. This has been observed causing a `dotnet restore` to fail outright. It's usually transient — retry the command a few times, or re-run `sudo bash .devcontainer/init-firewall.sh` to refresh the snapshot — not a sign the allowlist is missing the domain.

## Commands

```bash
# Backend — build/run/migrate
dotnet build EmployMe.sln
dotnet run --project src/Api
dotnet ef database update --project src/Api   # dotnet-ef installed globally by post-create.sh

# Frontend — not scaffolded yet (Phase I)
cd src/Web && npm install && npm run dev
```

No frontend (`src/Web`, `package.json`) exists yet — that's Phase I. No test project exists yet
either — that's Phase II (xUnit + Testcontainers per `docs/PLAN.md`).
