# Configuration

Every setting the API and the frontend read, where each one comes from in each environment, and
what happens when it is missing. Describes `main`.

---

## How the API assembles its configuration

`WebApplication.CreateBuilder` builds one `IConfiguration` from these sources, each overriding
matching keys of the ones before it:

```
appsettings.json  →  appsettings.{Environment}.json  →  environment variables  →  command-line args
```

Nested keys are written `Section:Key` in code and JSON, and `Section__Key` (double underscore) in
environment variables. Array items take an index: `Cors__AllowedOrigins__0`.

| File | Read when | Notes |
|---|---|---|
| `src/Api/appsettings.json` | always | Base values. Contains no secrets |
| `src/Api/appsettings.Development.json` | `ASPNETCORE_ENVIRONMENT=Development` | Merged over the base file key by key, not replacing it |
| `src/Api/Properties/launchSettings.json` | `dotnet run` and IDEs only | Sets `ASPNETCORE_ENVIRONMENT=Development` and `http://0.0.0.0:5000`. **Not copied into the Docker image** — which is why the deployed API runs as `Production` |
| `src/Api/Api.csproj` | build time | MSBuild, not runtime configuration |
| `src/Api/Dockerfile` | container start | `PORT`, `ASPNETCORE_URLS`, file-watcher switch — see below |

---

## API settings

| Key (env var form) | Default | Local (Dev Container) | Render | If missing |
|---|---|---|---|---|
| `ConnectionStrings__Default` | `Host=localhost;…;Database=employme` | `Host=db;…` from `docker-compose.yml` | Neon, **Npgsql key-value form** | Can't connect; startup fails in `Database.Migrate()` |
| `Cors__AllowedOrigins__0…n` | `[]` | not needed — the Vite proxy makes it same-origin | `https://employme-4uql.onrender.com` | No cross-origin caller is allowed: the deployed frontend fails with a CORS error. Deliberate — see below |
| `Ingest__PublicDeployment` | unset → `!IsDevelopment()` | `false` from `appsettings.Development.json` | leave unset → `true` | Resolves to the **strict** mode on any non-Development host |
| `Ingest__TriggerToken` | none | not needed | secret, set on Render only | On a public deployment `POST /api/ingest` answers `503 Ingest disabled` |
| `Ingest__Scheduler__Enabled` | `true` | — | — | The in-process scheduler runs (EM-18); `false` for test hosts |
| `Ingest__Scheduler__Interval` | `00:15:00` | — | — | How often it asks whether a source is due. Not a poll interval: `MinPollInterval` per source still decides |
| `Ingest__Scheduler__StartupDelay` | `00:00:30` | — | — | Lets the host start before the first tick |
| `Ingest__MaxPagesPerSource` | `5` | — | — | Bounds paginated sources (Arbeitnow) per run |
| `Sentry__Dsn`, or `SENTRY_DSN` | empty → Sentry off | unset; from the Dev Container events also need the DSN's ingest host in `init-firewall.sh` | the project's DSN | Sentry stays disabled and logs one warning at start; the host still starts (EM-22) |
| `Serilog__MinimumLevel__Default`, `Serilog__MinimumLevel__Override__<namespace>` | `Information`; `Microsoft.AspNetCore`, EF Core commands and `Polly` at `Warning` | same, from `appsettings.Development.json` | — | Defaults apply. The output format is not a setting: console text in Development, compact JSON elsewhere (EM-21) |
| `EMPLOYME_TEST_POSTGRES` | unset | the `db` service, from `docker-compose.yml` | — | Tests only (EM-23): the admin connection for integration tests. Unset, Testcontainers starts Postgres where Docker exists; otherwise the integration tests fail with a message saying so |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` via `launchSettings.json` | unset → `Production` | Decides whether `appsettings.Development.json` loads and whether Swagger is served |

On Render the connection string must be Npgsql's key-value form
(`Host=…;Database=…;Username=…;Password=…`), **not** the `postgresql://…` URI Neon shows — Npgsql
rejects its `channel_binding` parameter (`docs/PLAN.md`, EM-17).

### `IngestOptions`

A typed view of the `Ingest` section, registered in `Program.cs`:

```csharp
builder.Services.AddOptions<IngestOptions>()
    .Bind(builder.Configuration.GetSection(IngestOptions.SectionName))            // "Ingest"
    .PostConfigure<IHostEnvironment>((o, env) => o.PublicDeployment ??= !env.IsDevelopment());
```

`PostConfigure` runs after binding and fills `PublicDeployment` only if configuration left it
`null`.

| Environment | `Ingest:PublicDeployment` in config | Result |
|---|---|---|
| Development | `false` | `false` — guards off, no token needed |
| Render, nothing set | `null` | `true` — guards on, token required |
| Any host where `ASPNETCORE_ENVIRONMENT` was forgotten | `null` | `true` |

`PublicDeployment` is `bool?` on purpose. A forgotten variable must produce the strictest
behaviour — Tier-restricted sources skipped, ingest token mandatory — never an open `force=true`
endpoint on the internet.

### CORS

The default policy allows exactly the origins in `Cors:AllowedOrigins`, and nobody when the list
is empty. It matters only in production, where the frontend (`employme-4uql.onrender.com`) and the
API (`employme-api.onrender.com`) are different origins. `AllowAnyOrigin()` is never used: the API
has a mutating endpoint.

---

## Container settings

Both Dockerfiles start through a shell so values are read when the container **starts**, not when
the image is built:

| Setting | Where | Why |
|---|---|---|
| `PORT` | injected by Render (`10000`); unset elsewhere | The process must listen where the platform expects it |
| `ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}` | API `ENTRYPOINT` | Binds `0.0.0.0` (reachable by the load balancer, unlike `localhost`), falls back to `8080` |
| `serve -s dist -l ${PORT:-8080}` | Web `CMD` | Same idea for the static frontend; `-s` serves `index.html` for unknown paths (SPA mode) |
| `DOTNET_hostBuilder__reloadConfigOnChange=false` | API `ENV` | The appsettings file watcher costs an inotify instance per file; Render's free hosts share 128 across neighbours, and exhaustion crashes the app before its own code runs |

`exec` in the API entrypoint hands the shell's process over to .NET, so `SIGTERM` reaches it and
shutdown is graceful.

---

## GitHub Actions secrets and variables

Set under Settings → Secrets and variables → Actions; read only by workflows.

| Name | Kind | Used by | If missing |
|---|---|---|---|
| `INGEST_TRIGGER_TOKEN` | secret | `ingest.yml`, the hourly wake-up ingest (EM-18). Same value as Render's `Ingest__TriggerToken` | The workflow warns and exits without calling the API |
| `API_URL` | variable | `ingest.yml` | Defaults to `https://employme-api.onrender.com` |
| `DEPS_PAT` | secret | the dependency workflows — see [`dependency-updates.md`](./dependency-updates.md) | Both fail with 401 |

---

## Frontend settings

| Variable | When it is read | Value |
|---|---|---|
| `VITE_API_URL` | **at build time** — Vite inlines `import.meta.env.VITE_*` into the bundle | Production: `https://employme-api.onrender.com`. Locally: unset, so `api.ts` calls the relative `/api/…` and the Vite dev proxy forwards it to `http://localhost:5000` |

Changing `VITE_API_URL` requires a rebuild; it cannot be set on a running container. In Docker it is
passed as `--build-arg VITE_API_URL=…`.

---

## `.env` and `.env.example`

`.env.example` lists `SENTRY_DSN`, `ConnectionStrings__Default` and `Ollama__BaseUrl`. On `main`:

- **nothing loads `.env`.** There is no `env_file:` in `docker-compose.yml` and no loader in
  `Program.cs`. Inside the Dev Container the connection string comes from the compose
  `environment:` block, not from `.env`;
- **`SENTRY_DSN` is read from the process environment, not from `.env`.** Putting it in `.env`
  does nothing; export it in the shell, or set it on Render. `Ollama__BaseUrl` is read by no code
  yet — Ollama arrives with Phase III (EM-27).

So copying `.env.example` to `.env` is harmless but currently changes nothing.
