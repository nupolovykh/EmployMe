# Deployment

Where EmployMe runs, the staging stand, rollback, and what breaks easily. What each setting means is
in [`CONFIGURATION.md`](./CONFIGURATION.md). Render keeps its service settings — branch, auto-deploy
trigger, root directory, health check — only in its dashboard (there is no `render.yaml`, EM-72);
read them there, and note any change in the PR that needed it.

## What runs where

| Piece | Where | Built from |
|---|---|---|
| API | Render web service `EmployMe.Api` — https://employme-api.onrender.com, deploys `main` | `src/Api/Dockerfile` |
| Frontend | Render static site `EmployMe.Vite` — https://employme-4uql.onrender.com, deploys `main` | `npm ci && npm run build` in `src/Web` |
| Staging API | Render web service `EmployMe.Api.Staging` — https://employme-api-staging.onrender.com, deploys `staging` | `src/Api/Dockerfile` |
| Database | Neon project `cool-frog-59285410`, branches `production` and `staging` | — |
| Errors | Sentry `employme-inc` / `employme-api` (EU) | SDK in the API |

`src/Web/Dockerfile` is not used by any of them; it builds the frontend as a container on the host,
and is the fallback for a host without static sites.

The free API instance sleeps after about 15 idle minutes and takes about a minute to wake;
`ingest.yml` wakes it hourly (`ASSUMPTIONS.md` A-014). On start the API runs `Database.Migrate()`
before it listens, so a failed migration fails the deploy's health check.

## What has to be set

| Where | Variable |
|---|---|
| `EmployMe.Api` | `ConnectionStrings__Default` (Npgsql key-value, not Neon's `postgresql://` URI), `Ingest__TriggerToken` (= GitHub secret `INGEST_TRIGGER_TOKEN`), `Cors__AllowedOrigins__0`, `SENTRY_DSN`; leave `Ingest__PublicDeployment` unset |
| `EmployMe.Vite` | `VITE_API_URL` — read at build time, so a change needs a redeploy |
| GitHub | secrets `INGEST_TRIGGER_TOKEN`, `SLACK_WEBHOOK_URL`, `DEPS_PAT`; variable `API_URL` |

## Staging

Staging checks a phase on a real host before it reaches `main`.

- Point it at a phase by fast-forwarding the `staging` branch to the phase branch's tip — never by
  switching the service's branch.
- Its database is the Neon branch `staging`, a child of `production`; reset it from its parent when
  the phase needs current data. Connect over the direct host, not `-pooler`: ingest takes
  session-scoped advisory locks.
- `ASPNETCORE_ENVIRONMENT=Staging` behaves like Production (token required, JSON logs, Sentry
  environment `Staging`); it has its own `Ingest__TriggerToken` and no CORS origin.
- Jobicy is disabled in the staging database, so staging and production do not both poll it inside
  its one-hour limit.

## Rollback

Render's Deploys → an earlier deploy → Rollback is fast; a revert PR into `main` (`Hotfix:`) keeps
`main` matching production. `Migrate()` only applies forward, so rolling code back does not roll a
migration back.

## What breaks easily

| Situation | What happens |
|---|---|
| Neon's URI as the connection string | start-up fails in `Migrate()` |
| `VITE_API_URL` changed without a redeploy | the frontend keeps calling the old address |
| CORS origin not exactly the frontend's | the browser blocks requests while `curl` works |
| GitHub and Render ingest tokens differ | `ingest.yml` gets 401; without the secret it stays green and does nothing |
| A package builds but fails at start-up | caught by CI's image step (`build.yml`), not by `dotnet build` |
| Neon slow to wake | first request slow; `/health` stays green — look at `/health/ready` |
| `force=true` on a manual ingest | bypasses `min_poll_interval` — Jobicy allows one poll an hour |
