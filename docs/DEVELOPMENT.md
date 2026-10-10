# Development runbook

The commands this project actually uses, and the procedures that are not obvious from the code:
migrations, adding a company or a source, calling the API, building the containers. Everything
runs **inside the Dev Container** unless a step says otherwise.

How the pieces fit: [`ARCHITECTURE.md`](./ARCHITECTURE.md). Settings:
[`CONFIGURATION.md`](./CONFIGURATION.md). Naming of branches and commits:
[`CONVENTIONS.md`](./CONVENTIONS.md).

---

## Environment

Open the repository in VS Code → **Reopen in Container**, or `devcontainer up`. Compose starts three
services:

| Service | Address from the `app` container | Purpose |
|---|---|---|
| `app` | — | The workspace: .NET 10 SDK, Node 24, `gh`, `dotnet-ef`, Claude Code |
| `db` | `db:5432`, `postgres`/`postgres`, database `employme` | Postgres 18 + pgvector |
| `ollama` | `ollama:11434` | Local embeddings — unused until Phase III |

`post-create.sh` runs once: fixes volume ownership, copies `.env.example` → `.env`, installs
`dotnet-ef`, runs `npm install` in `src/Web`. `init-firewall.sh` runs on every start and allows
only listed domains — a new external domain must be added to its `for domain in …` list, or calls
to it fail as network errors (see `CLAUDE.md`).

If a NuGet or npm download fails with `Network is unreachable` even though the domain is
allowlisted, refresh the firewall's IP snapshot and retry:

```bash
sudo bash .devcontainer/init-firewall.sh
```

### Traps

Each of these has broken a build or a container start at least once.

- **Postgres 18 volume path.** The `db` service's named volume mounts at `/var/lib/postgresql`,
  not the old `/var/lib/postgresql/data`. Postgres 18's image moved `PGDATA` to a version-specific
  path (`/var/lib/postgresql/18/docker`) and declares the parent as its `VOLUME`, for `pg_upgrade`
  with hard links. Mounted at `.../data`, the volume goes unused, the healthcheck never passes,
  `depends_on: condition: service_healthy` waits forever, and "Rebuild Container" hangs with no
  clear error.
- **Firewall allowlist.** `init-firewall.sh` runs on every start (`postStartCommand`, via `sudo`)
  and denies outbound traffic except to the domains in its `for domain in …` list: GitHub, npm,
  NuGet, the Anthropic API, Sentry and the MCP endpoints. A new dependency, MCP server or source
  domain has to be added there. A spike against a domain that is not listed fails as a network
  error, which is easy to misread as the source being down.
- **Rotating CDN addresses.** The script resolves each domain once, at container start, and allows
  only those addresses. NuGet and npm sit behind CDNs with many edge addresses, so a later request
  can land on one outside the snapshot and fail with `Network is unreachable` / `EHOSTUNREACH`
  although the domain is listed. Retry, or refresh the snapshot with the command above. CI retries
  its downloads the same way.
- **`net10.0` only.** The container has the .NET 10 SDK and runtime and nothing older. A project
  targeting `net8.0` builds — compiling needs only reference assemblies — but `dotnet run` fails
  with "You must install or update .NET to run this application". Package sets need the same proof:
  Swashbuckle 6.6.2 builds on `net10.0` and throws a `TypeLoadException` only at `dotnet run`. Prove
  a package change by running the API, not only by building it.
- **`dotnet run` is always Development.** `Properties/launchSettings.json` sets
  `ASPNETCORE_ENVIRONMENT`, whatever the shell says. To exercise the production guards (ingest
  token, Sentry, JSON logs) locally, use `--no-launch-profile` or run the built `Api.dll`. A
  "Production" run that accepts ingest without a token is this, not a bug.
- **`.env` is loaded by nothing.** There is no `env_file:` in compose and no loader in code, so
  the `.env` that `post-create.sh` copies from `.env.example` changes nothing. Inside the
  container the connection string and Ollama URL come from `docker-compose.yml`'s `environment:`
  block; `SENTRY_DSN` is read from the process environment. Every setting is in
  [`CONFIGURATION.md`](./CONFIGURATION.md).

---

## Run it

Two terminals.

```bash
# API — http://localhost:5000, Swagger at http://localhost:5000/swagger (Development only)
dotnet run --project src/Api
```

```bash
# Frontend — http://localhost:5173; /api/* is proxied to http://localhost:5000
cd src/Web
npm install          # first time, or after package.json changes
npm run dev
```

The frontend shows an empty list until the API is running **and** the database has postings —
on a fresh database run an ingest first ([Calling the API](#calling-the-api)).

The API applies pending migrations on startup, so a fresh `db` volume gets its schema and seed
rows from the first `dotnet run`.

### Checks before pushing

```bash
dotnet build EmployMe.sln
dotnet format EmployMe.sln --verify-no-changes            # fix with: dotnet format EmployMe.sln
dotnet test tests/Api.Tests --filter "Category!=Contract"
cd src/Web && npm run lint && npm run build     # oxlint; then tsc -b && vite build
```

`npm run build` is the real type-check: `npm run dev` strips types without checking them.
These are the steps CI's `build` check runs (EM-24), in the same container.

---

## Tests

`tests/Api.Tests` (xUnit, EM-23):

| Folder | What | Needs |
|---|---|---|
| `Adapters/` | each adapter maps the committed `spikes/<source>/response.json` | nothing |
| `Ingest/` | `HtmlText`, `SeniorityMap`, the Polly pipeline's retry rules | nothing |
| `Integration/` | `IngestService` and the ingest endpoint against a real Postgres, through `WebApplicationFactory` | Postgres |
| `Contract/` | each enabled source's real adapter against its real endpoint, from the seeded rows (EM-55) | Postgres **and the network** |

```bash
dotnet test tests/Api.Tests --filter "Category!=Contract"     # everything but Contract
dotnet test tests/Api.Tests --filter "Category=Contract"      # live endpoints
```

Integration tests get Postgres from `EMPLOYME_TEST_POSTGRES`, which `docker-compose.yml` points at
the `db` service — the container has no Docker socket, so Testcontainers cannot start its own.
Where Docker exists and the variable is unset, Testcontainers starts `pgvector/pgvector:pg18`. Each
test creates and drops its own database; `employme` is never touched.

**Run `Contract` by hand sparingly:** it calls every enabled source for real, and each call counts
against that source's limits — Jobicy's is one poll an hour. `contract.yml` runs it nightly
(03:43 UTC); a failure opens or updates the issue *EM-55: nightly source contract test failed*,
posts to Slack when `SLACK_WEBHOOK_URL` is set, and the next green run closes the issue.

---

## Migrations

All commands take `--project src/Api` (or run them from `src/Api` without it).

```bash
dotnet ef migrations list --project src/Api                     # which exist, which are applied
dotnet ef database update --project src/Api                     # apply everything pending
dotnet ef migrations add <Name> --project src/Api               # after changing Models/ or AppDbContext
dotnet ef migrations remove --project src/Api                   # drop the last one, if not applied yet
dotnet ef database update <PreviousName> --project src/Api      # roll back to a named migration
dotnet ef database update 0 --project src/Api                   # roll back everything
dotnet ef migrations script --project src/Api                   # SQL for review, nothing applied
dotnet ef migrations script <From> <To> --project src/Api       # SQL for one range
dotnet ef dbcontext info --project src/Api                      # provider, database, connection
```

Rules this project follows:

- **One migration per decision**, named for what it does (`BoundRawPostingsToOneRowPerPosting`,
  not `Update3`). The list in [`DATA-MODEL.md`](./DATA-MODEL.md#migration-history) reads as the
  project's history.
- **Reference data goes in a data migration** (`migrationBuilder.InsertData` / `Sql("UPDATE …")`)
  with a comment saying why — not in a script outside git.
- **Never reorder or renumber enum members.** They are stored as integers.
- **Commit the migration together with the model change** and `AppDbContextModelSnapshot.cs`.
- Update [`DATA-MODEL.md`](./DATA-MODEL.md) in the same PR.

---

## Adding a target company

Tier A boards have no search; the registry is the list of boards that get fetched. A company enters
it only after its board answered `200` live — never on a guessed token (`doist`, `automattic`,
`revolut` and `netflix` all returned `404` when guessed).

1. **Find the real board URL**, then take the token from it:
   `site:boards.greenhouse.io <company>` → `boards.greenhouse.io/<token>`;
   `site:jobs.lever.co <company>` → `jobs.lever.co/<token>`. Keep the exact casing.
2. **Verify it live**, from inside the container:

   ```bash
   curl -s -o /dev/null -w "%{http_code}\n" "https://boards-api.greenhouse.io/v1/boards/<token>/jobs"
   curl -s -o /dev/null -w "%{http_code}\n" "https://api.lever.co/v0/postings/<token>?mode=json"
   ```

   Anything but `200` → no row.
3. **Add a data migration:**

   ```bash
   dotnet ef migrations add Add<Company>TargetCompany --project src/Api
   ```

   In `Up()`, `migrationBuilder.InsertData("TargetCompanies", …)` with `SourceId` (Greenhouse = 2,
   Lever = 3), `CompanyName`, `BoardToken`, `WhyTarget` (one sentence: why this company fits the
   criteria), `HiringGeo`, `Status` (`0` Active, `1` Watch, `2` Rejected), `VerifiedAt`,
   `JobsSeen`. In `Down()`, `DeleteData` for the same id. `RecordSezzleRejection` is the template
   for one row: its whole `Up()`/`Down()` is exactly this.
4. **A company that fails the criteria is still recorded**, as `Rejected` with the reason in
   `WhyTarget` — see `RecordSezzleRejection`. The registry exists so the same company is never
   evaluated twice.
5. Apply, then ingest that source ([Calling the API](#calling-the-api)) and check that its
   postings appear.

This manual path is temporary. EM-61 (Phase IV) replaces it: marking an application `Applied`
against an unknown company resolves and verifies the board and inserts the row.

---

## Calling the API

Swagger UI (`/swagger`) covers the same calls interactively in Development.

### Ingest

```bash
curl -X POST http://localhost:5000/api/ingest                               # every enabled source
curl -X POST "http://localhost:5000/api/ingest?source=greenhouse"           # one source, by slug
curl -X POST "http://localhost:5000/api/ingest?source=lever&force=true"     # ignore MinPollInterval
```

Locally no token is needed (`Ingest:PublicDeployment=false`). Against the deployment:

```bash
curl -X POST "https://employme-api.onrender.com/api/ingest" \
     -H "X-Ingest-Token: <value of Ingest__TriggerToken on Render>"
```

The free Render instance sleeps after 15 idle minutes; the first request takes about a minute.

| Response | Meaning |
|---|---|
| `200` + report | Ran. Each source has an outcome: `ok` (with fetched / created / updated), `skipped` (with the reason: tier, disabled, not cleared for public display, no adapter, poll interval not elapsed, locked by another run) or `failed` (details in the server log only) |
| `404` | `source` matched no row |
| `401` | Public deployment, token missing or wrong |
| `503` | Public deployment, `Ingest:TriggerToken` not configured at all |

Example report (values illustrative):

```json
{
  "sources": [
    { "slug": "greenhouse", "outcome": "ok", "fetched": 236, "created": 0, "updated": 236, "detail": null },
    { "slug": "jobicy", "outcome": "skipped", "fetched": 0, "created": 0, "updated": 0,
      "detail": "min poll interval not elapsed; next due 2026-09-18 21:00:00Z" }
  ],
  "fetched": 236, "created": 0, "updated": 236
}
```

**Use `force=true` sparingly.** It is the one thing that can break Jobicy's once-an-hour cap, and a
ban would land on this project.

### Vacancies

```bash
curl "http://localhost:5000/api/vacancies"                                   # page 1, 20 per page
curl "http://localhost:5000/api/vacancies?keyword=backend&location=berlin"
curl "http://localhost:5000/api/vacancies?seniority=Junior&pageSize=50&page=2"
curl "http://localhost:5000/api/vacancies?publishedAfter=2026-09-01T00:00:00Z"
curl "http://localhost:5000/api/vacancies/42"
curl "http://localhost:5000/health"                                         # liveness
curl "http://localhost:5000/health/ready"                                   # database + failing sources
```

| Parameter | Notes |
|---|---|
| `keyword` | substring of title, company or description; `%`, `_`, `\` are matched literally |
| `location` | substring of location |
| `publishedAfter`, `publishedBefore` | ISO 8601; compared with `PublishedAt`, or `FetchedAt` when the source gave no date |
| `seniority` | `Intern`, `Junior`, `Mid`, `Senior`, `Lead`, `Unknown`. A level never matches `Unknown` rows |
| `page`, `pageSize` | `pageSize` is clamped to 1–100 |

Response: `{ "items": [VacancyDto…], "total": n, "page": n, "pageSize": n }`, newest first.

### Looking at the data directly

The container has no `psql`. From the host, or any client pointed at `localhost:5432`:

```sql
SELECT s."Slug", count(v.*) FROM "Sources" s LEFT JOIN "Vacancies" v ON v."SourceId" = s."Id" GROUP BY 1;
SELECT "Seniority", count(*) FROM "Vacancies" GROUP BY 1;            -- 0 = Unknown … 5 = Lead
SELECT "Payload" FROM "RawPostings" WHERE "ExternalId" = '<id>';      -- what the source actually sent
SELECT * FROM "__EFMigrationsHistory";
```

Table and column names are PascalCase and must be quoted.

---

## Adding a source

A source is a row plus an adapter class (`PROCESS.md` rule 6), and no integration is written before
its spike (rule 2). In order:

1. **Spike.** Call the real endpoint, commit the response as `spikes/<source>/response.json` with
   `spikes/<source>/NOTES.md`: URL, date, what came back, the terms-of-use verdict quoting and
   linking the terms. If the domain is not allowlisted, add it to `init-firewall.sh` first.
2. **Registers.** Add the source to `docs/SOURCES.md` (tier, endpoint, auth, rate limit, terms) and
   its load-bearing claims to `docs/ASSUMPTIONS.md` at level `spike`. A source change that touches
   neither is incomplete.
3. **Adapter.** `src/Api/Ingest/Adapters/<Name>JobSource.cs` implementing `IJobSource`:
   `AdapterType` equal to the row's value; `FetchAsync` yields `FetchedPosting(NormalizedVacancy,
   rawJson)`; drop postings missing required fields instead of throwing; strip HTML from
   descriptions with `HtmlText.ToPlainText`; map a level field through `SeniorityMap` only if the
   source supplies one; bound pagination with `MaxPagesPerSource`; for Tier A, loop over
   `context.TargetCompanies` and `continue` past a failing company.
4. **Register** it in `Program.cs`: `builder.Services.AddSingleton<IJobSource, <Name>JobSource>();`
5. **Seed the row** with a data migration: `Slug`, `AdapterType`, `Tier`, `BaseUrl`,
   `MinPollInterval` from the source's terms, attribution flags, `TermsUrl`, `TermsReviewedAt`,
   `Enabled`, and `PublicDeployEnabled` — `false` unless the terms clear public display.
6. **Never Tier D in a deployed environment**, and never hh.ru at all (`CLAUDE.md`).
7. Ingest it locally with `?source=<slug>`, check the report and a few rows, and update
   `docs/PLAN.md`.

---

## Containers

The Dev Container has no `docker`/`podman` CLI and no socket, so these run **on the host**. Render
builds `src/Api/Dockerfile` itself; the frontend on Render is a static site and does not use
`src/Web/Dockerfile` (`DEPLOYMENT.md`). This is for reproducing a deployment problem locally.
`host.docker.internal` resolves on Docker Desktop; on Linux add `--add-host=host.docker.internal:host-gateway`,
and with Podman use `host.containers.internal`.

```bash
# API — context is src/Api (the Dockerfile copies it and restores Api.csproj)
docker build -t employme-api src/Api
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=host.docker.internal;Port=5432;Database=employme;Username=postgres;Password=postgres" \
  -e Ingest__TriggerToken=local-secret \
  -e Cors__AllowedOrigins__0=http://localhost:8081 \
  employme-api
# → http://localhost:8080/health. Runs as Production: no Swagger, token required for ingest.

# Frontend — VITE_API_URL is baked in at build time
docker build -t employme-web --build-arg VITE_API_URL=http://localhost:8080 src/Web
docker run --rm -p 8081:8080 employme-web
# → http://localhost:8081
```

| Stage | API | Frontend |
|---|---|---|
| build | `dotnet/sdk:10.0`: `restore`, `publish -c Release` | `node:24-alpine`: `npm ci`, `npm run build` |
| runtime | `dotnet/aspnet:10.0`, only the published output | `node:24-alpine` + `serve -s dist` |
| port | `${PORT:-8080}`, read at start | `${PORT:-8080}`, read at start |
| ignored context | `bin/`, `obj/` | `node_modules/`, `dist/` |
