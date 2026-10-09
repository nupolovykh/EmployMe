# Deployment

Where EmployMe runs, how a merge becomes a running service, the staging stand, rollback, and what
breaks easily. What each setting means is in [`CONFIGURATION.md`](./CONFIGURATION.md); this file
says where it has to be set.

Render's service settings live only in its dashboard — there is no `render.yaml` — so the values
below were read from the Render API on 2026-10-09. Re-check them there before relying on them, and
record any change made in the dashboard in the PR that needs it.

---

## What runs where

| Piece | Platform | Built from | URL |
|---|---|---|---|
| API | Render web service `EmployMe.Api`, Docker, free, Frankfurt | `src/Api/Dockerfile` | https://employme-api.onrender.com |
| Frontend | Render static site `EmployMe.Vite` | `npm ci && npm run build` in `src/Web`, publishes `dist` | https://employme-4uql.onrender.com |
| Staging API | Render web service `EmployMe.Api.Staging`, Docker, free, Frankfurt | `src/Api/Dockerfile` | https://employme-api-staging.onrender.com |
| Database | Neon project `cool-frog-59285410`, Postgres 18 + pgvector, `eu-central-1`; branches `production` and `staging` | — | — |
| Errors | Sentry, organisation `employme-inc`, project `employme-api` (EU) | SDK inside the API | — |
| Alerts | Slack, through `SLACK_WEBHOOK_URL` | GitHub Actions | — |

| Service | Branch | Auto-deploy | Root directory | Health check | PR previews |
|---|---|---|---|---|---|
| `EmployMe.Api` | `main` | after CI checks pass | `src/Api` | `/health` | off |
| `EmployMe.Vite` | `main` | after CI checks pass | `src/Web` | — | off |
| `EmployMe.Api.Staging` | `staging` | on every commit | `src/Api` | `/health` | off |

**Inside the Dev Container:** .NET SDK, Node.js, Postgres + pgvector and Ollama as compose services,
`gh`, `dotnet-ef`, Claude Code. **Outside it:** Render, Neon, Sentry, Linear, Slack and their MCP
endpoints — the project holds only keys and DSNs as environment variables. GitHub Actions runs the
same Dev Container image on GitHub's runners, so "works locally" and "works in CI" stay the same.

---

## From merge to running service

```
merge into main
  ├─ build.yml on main ─ Dev Container: restore, build, format check, tests, lint, vite build
  │                      red main → Slack
  ├─ EmployMe.Api, once the checks pass
  │    clone main → cd src/Api → docker build
  │      build stage   sdk:10.0   COPY . . → restore Api.csproj → publish -c Release
  │      runtime stage aspnet:10.0 COPY the published output, ENV, ENTRYPOINT
  │    start → Render sets PORT=10000 → listen on 0.0.0.0:$PORT
  │    GET /health passes → traffic moves to the new deploy
  └─ EmployMe.Vite, once the checks pass
       cd src/Web → npm ci && npm run build (VITE_API_URL baked into the bundle) → serve dist/
```

When the API starts, in order (`Program.cs`): configuration is assembled (files, then environment
variables); Sentry starts if a DSN is set; Serilog writes JSON lines outside Development; EF Core is
registered with `EnableRetryOnFailure`, which absorbs Neon waking up; `Database.Migrate()` applies
pending migrations — on every start; Kestrel starts listening only after that; `IngestScheduler`
ticks 30 s later and then every 15 minutes with `force=false`. If `Migrate()` fails, the process
exits and the deploy fails its health check.

The free instance sleeps after about 15 idle minutes and takes about a minute to wake.
`ingest.yml` calls `POST /api/ingest` hourly, which wakes it (`ASSUMPTIONS.md` A-014).

### Workflows that touch the deployment

| Workflow | When | What it does | Needs |
|---|---|---|---|
| `build.yml` | every PR and every push to `main` | builds and tests in the Dev Container; red `main` → Slack | `SLACK_WEBHOOK_URL` (optional) |
| `ingest.yml` | hourly (`17 * * * *`) and by hand | `POST $API_URL/api/ingest` with `X-Ingest-Token`, up to 5 retries 20 s apart, 600 s budget | `INGEST_TRIGGER_TOKEN`; `API_URL` (optional) |
| `contract.yml` | nightly 03:43 UTC and by hand | runs every enabled adapter against its live source; opens, updates or closes a GitHub issue; Slack on failure | `SLACK_WEBHOOK_URL` (optional) |

Scheduled workflows run only from the default branch: until a phase lands on `main`, its new
schedules do not fire.

CI never runs `docker build`. A broken Dockerfile, or a package that builds but fails at start-up,
shows up only when Render deploys — build the image on the host before a risky change:
`docker build -t employme-api src/Api` (`DEVELOPMENT.md` → "Containers").

---

## What has to be set

Meanings and defaults: [`CONFIGURATION.md`](./CONFIGURATION.md). The Render MCP cannot read
environment variables, so this list is what the code requires, not a readout of the dashboard.

| Where | Variable | Note |
|---|---|---|
| `EmployMe.Api` | `ConnectionStrings__Default` | Npgsql key-value form, not Neon's `postgresql://` URI (Npgsql rejects `channel_binding`) |
| `EmployMe.Api` | `Ingest__TriggerToken` | same value as the GitHub secret `INGEST_TRIGGER_TOKEN`; rotate both together |
| `EmployMe.Api` | `Cors__AllowedOrigins__0` | `https://employme-4uql.onrender.com`, exact: scheme and host, no trailing `/` |
| `EmployMe.Api` | `SENTRY_DSN` | without it Sentry is off, by design |
| `EmployMe.Api` | `Ingest__PublicDeployment` | **leave unset** — unset means public, so forgetting it keeps the guards on |
| `EmployMe.Vite` | `VITE_API_URL` | `https://employme-api.onrender.com`; read at build time, so a change needs a redeploy |
| GitHub | `INGEST_TRIGGER_TOKEN`, `SLACK_WEBHOOK_URL`, `DEPS_PAT`; variable `API_URL` | `CONFIGURATION.md` → "GitHub Actions secrets and variables" |

Render injects `PORT` and `RENDER_GIT_COMMIT` itself. `ASPNETCORE_ENVIRONMENT` is not set in
production, so it is `Production`.

---

## Staging

Staging exists so that a phase is checked on a real host before it reaches `main`, instead of
errors being found in production.

- **Service:** `EmployMe.Api.Staging` (`srv-db2amcgm7kps73e5f260`) deploys the git branch
  `staging` on every commit. Point it at work by fast-forwarding `staging` to the phase branch's
  tip — never by switching the service to another branch.
- **Database:** Neon branch `staging` (`br-damp-rice-b2mmwidu`), a child of `production` taken on
  2026-10-06. Reset it from its parent when the phase needs current data. It is reached over the
  direct host, not the `-pooler` one: ingest takes session-scoped advisory locks, which the pooler
  in transaction mode does not hold.
- **Settings:** `ASPNETCORE_ENVIRONMENT=Staging` (behaves like Production — token required, JSON
  logs, no Swagger; Sentry environment `Staging`), its own `Ingest__TriggerToken`, the same
  `SENTRY_DSN`, no `Cors__AllowedOrigins__0` — there is no staging frontend.
- **Jobicy is disabled in the staging database** (`Enabled=false` on its row), so staging and
  production do not both poll it inside its one-hour limit.

The phase PR's "How to check manually" plan runs against staging.

---

## Rollback

| Way | How | Note |
|---|---|---|
| Render | the service's Deploys → an earlier deploy → Rollback | Fast, no rebuild. Check afterwards whether auto-deploy is still on |
| `git revert` | a revert PR into `main` (a `Hotfix:`), deployed once checks pass | Slower; `main` matches production afterwards |
| Database | — | `Migrate()` only applies forward: rolling code back does not roll a migration back. A phase that adds a migration needs its down path checked |

The API and the frontend roll back independently.

---

## Docker's roles

| Where | What | Used by |
|---|---|---|
| Dev Container | `docker-compose.yml`: `app`, `db`, `ollama` | development; CI through `devcontainers/ci` |
| `src/Api/Dockerfile` | production image of the API | Render (`EmployMe.Api`, `EmployMe.Api.Staging`) |
| `src/Web/Dockerfile` | `node:24-alpine` build, then `serve -s dist` | **nothing in the pipeline** — the frontend is a Render static site. Kept for building the frontend as a container on the host, and as the fallback for a host without static sites |
| Testcontainers | `pgvector/pgvector:pg18` | the tests, only where Docker exists and `EMPLOYME_TEST_POSTGRES` is unset |

The API image: the SDK stage copies `src/Api`, restores `Api.csproj` and publishes; the runtime
stage on `aspnet:10.0` copies only the published output. `ENTRYPOINT` runs through `sh -c` so that
`PORT` is read when the container starts, binds `0.0.0.0`, and `exec`s `dotnet` so that `SIGTERM`
reaches the app. `DOTNET_hostBuilder__reloadConfigOnChange=false` stops the config file watcher
from exhausting inotify on Render's shared hosts. `launchSettings.json` is not in the image.

---

## What breaks easily

| Situation | What happens | What to do |
|---|---|---|
| Neon's `postgresql://` URI as the connection string | start-up fails in `Migrate()` | use the key-value form |
| `VITE_API_URL` changed without a redeploy | the frontend keeps calling the old address | change it, then redeploy the static site |
| `Cors__AllowedOrigins__0` not exactly the frontend's origin | the browser blocks requests while `curl` works — easy to misread as "the API is fine" | copy the origin from the address bar |
| `INGEST_TRIGGER_TOKEN` and `Ingest__TriggerToken` differ | `ingest.yml` gets 401 and goes red; with no secret at all it stays green and does nothing | keep them equal; rotate both |
| `Ingest__TriggerToken` unset on Render | `503 Ingest disabled` | set it |
| Expecting Swagger or open ingest on a deploy | they exist in Development only | intended |
| Dockerfile broken while CI is green | CI does not build images; Render fails the deploy | build the image on the host first |
| A package builds but fails at start-up | `dotnet build` does not catch it (Swashbuckle on `net10.0`) | prove it with `dotnet run` |
| Neon slow to wake | the first request and `Migrate()` are slow; `/health` stays green because it does not touch the database | look at `/health/ready` |
| `force=true` on a manual ingest | bypasses `min_poll_interval` | avoid it — Jobicy allows one poll an hour |
| A setting changed only in the Render dashboard | not in git history | note it in the PR that needed it, and update this file |
