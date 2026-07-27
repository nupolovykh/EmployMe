# Development Plan

**Rule for progressing between phases:** each phase must be working — and, where applicable, deployed — before the next one starts. Do not go deep into Phase III while Phase I isn't deployed; this guards against the common "wide and shallow" failure pattern.

**Running in parallel with every phase:** actual job applications (10–15/week), not gated on the project's completion.

---

## Tooling map

What lives **inside** the Dev Container vs. what runs **outside** it.

**Inside the Dev Container** (`devcontainer.json` / `Dockerfile` / `docker-compose`):
- .NET SDK, Node.js — language runtimes
- PostgreSQL + `pgvector` — as a compose service, for local development only
- Ollama — as a compose service, local embedding model, so matching can be tested from day one without depending on the cloud
- git, GitHub CLI, `dotnet-ef`, other CLI tooling
- Claude Code — via the official `ghcr.io/anthropics/devcontainer-features/claude-code` feature. This sandboxes Claude Code to `/workspace` and applies a network firewall with a domain allowlist. **If Claude Code itself (not just the chat client) should reach the MCP servers below, their domains must be explicitly added to that allowlist** — the firewall blocks anything not listed by default.

**Outside the Dev Container:**
- **Railway** — the deployment target itself; code is pushed there via CLI/CI, it is not run "inside" the local container.
- **Sentry, Linear, Slack** — SaaS products. The project only holds an API key/DSN as an environment variable; the services themselves are never self-hosted.
- **MCP endpoints** for the above (`mcp.linear.app`, `mcp.slack.com`, `mcp.sentry.dev`, `mcp.railway.com`) — remote servers that a Claude client connects to (the chat interface, or Claude Code if configured separately). Nothing to install or deploy for these.
- **GitHub Actions** — runs on GitHub-hosted runners, separate from the local machine. Worth building CI on the same base image as the Dev Container, so "works locally" and "works in CI" stay identical.
- **Cowork, Claude Design, Claude in Chrome** — Claude interfaces entirely outside the repository; nothing to provision.

---

## Phase 0 — Foundation

Goal: working environment and data schema; nothing user-facing yet.

- [ ] Repository, `LICENSE` (MIT), `.gitignore`, README skeleton
- [ ] Dev Container: `devcontainer.json` + `Dockerfile` with .NET SDK, Node.js, Postgres+pgvector service, Ollama service, Claude Code feature; firewall allowlist extended for MCP domains if needed
- [ ] Draft Postgres schema: `vacancies`, `sources`, `applications`, `embeddings`
- [ ] GitHub Actions skeleton: build on push, same base image as the Dev Container
- [ ] hh.ru API access registered
- [ ] Linear project set up, one issue per task below (this plan becomes the initial backlog)

**Exit criterion:** `devcontainer up` (or "Reopen in Container") brings up an empty API and database with no errors, and Claude Code runs inside it.

---

## Phase I — MVP: core discovery (RU track)

Goal: a working, deployed vertical slice — source to screen.

- [ ] ASP.NET Core Web API: endpoints for listing vacancies
- [ ] EF Core migrations against the Phase 0 schema
- [ ] hh.ru API client: search, pagination, mapping into the domain model
- [ ] Manual (not scheduled) ingest job: one command populates the database
- [ ] REST endpoints: list, filter by keyword/stack/date
- [ ] Frontend: React + TS + Vite, vacancy list, basic filters
- [ ] Initial Railway deployment

**Exit criterion:** the site is live, shows real hh.ru vacancies, and filtering works.

---

## Phase II — Reliability and a second source

Goal: stops being a script, becomes a service; expands into Georgia or international.

- [ ] Scheduler: `BackgroundService`/Hangfire — scheduled ingest, no manual trigger needed
- [ ] Second source: jobs.ge/headhunter.ge (parsed respectfully, honoring `robots.txt`) **or** one international remote board with RSS/API
- [ ] `HttpClient` + Polly: retries and rate-limit handling for external API resilience
- [ ] Serilog structured logging + health checks
- [ ] Sentry SDK integrated for error monitoring
- [ ] Tests: xUnit (unit) + Testcontainers (integration, against a real Postgres)
- [ ] GitHub Actions: tests + lint + build on every PR
- [ ] Slack channel (or Claude Tag) wired to CI/deploy notifications

**Exit criterion:** the service refreshes itself on a schedule, survives an external API outage, is covered by tests and CI, and errors surface in Sentry.

---

## Phase III — Personalization and the semantic layer

Goal: the layer that differentiates this from a generic aggregator.

- [ ] `pgvector` extension enabled in Postgres
- [ ] Ollama deployed with a multilingual embedding model (bge-m3 or e5)
- [ ] Embeddings for vacancies and for my own CV/profile, with caching to avoid recomputation
- [ ] Fit-score: cosine similarity between CV and vacancy, with an explanation (matched vs. missing requirements)
- [ ] Semantic deduplication: the same vacancy across sources/languages collapses into one card
- [ ] LLM extraction into strict JSON: seniority, stack, work format, language requirement
- [ ] Manual labeling of 50 vacancies + a script measuring extraction/matching precision against that labeled set
- [ ] UI for the fit-score explanation mocked in Claude Design before implementation

**Exit criterion:** the vacancy list is sorted by personal relevance, duplicates are collapsed, and there is a measured precision figure.

---

## Phase IV — Application tracker (personal CRM layer)

Goal: a tool used daily, not a demo for a screenshot.

- [ ] Status model: viewed / applied / interview / rejected / offer, plus notes and dates
- [ ] Endpoints for status changes
- [ ] UI: list or kanban board with status management, mocked in Claude Design first
- [ ] Dashboard: applications per week, conversion by stage
- [ ] Manual QA pass on the deployed Railway staging environment via Claude in Chrome

**Exit criterion:** the spreadsheet is retired — the entire job-search workflow runs through this tool.

---

## Phase V — Portfolio polish

Goal: the project survives 20 minutes of interview questions.

- [ ] Full README (this repo's), architecture diagram, setup instructions, screenshots
- [ ] Deployment write-up: why self-hosted embeddings, why Railway, why hh.ru's API instead of scraping
- [ ] Metrics section documenting the precision figures from Phase III
- [ ] Repository cleanup: personal application data removed, seed data added if needed for demos
- [ ] Interview talking points: architectural decisions and their trade-offs, written down in advance

**Exit criterion:** the project is ready to be linked from a CV and defended live.

---

## Estimated timeline (3–4 hours/day)

| Phase | Content | Duration |
|---|---|---|
| 0 | Foundation | 2–3 days |
| I | MVP core discovery | ~2 weeks |
| II | Reliability + second source | ~1.5 weeks |
| III | Personalization + semantic layer | ~2 weeks |
| IV | Application tracker | ~4–5 days |
| V | Portfolio polish | ~3–4 days |
| **Total** | | **~7–8 weeks** |
