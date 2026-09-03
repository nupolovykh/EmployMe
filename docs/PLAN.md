# Development Plan

**Revision 2 — 22 Aug 2026.** Revision 1 was planned around the hh.ru API. Its public vacancy
search has returned `403` to unauthorized callers since April 2026, and its developer agreement
separately forbids transferring retrieved data to third-party services — so a publicly deployed
aggregator over hh.ru was never shippable. The plan was rebuilt accordingly. The post-mortem is
Linear EM-9; the falsified assumption is A-000 in [`docs/ASSUMPTIONS.md`](./ASSUMPTIONS.md).

**Phase gate:** each phase must be working — and, where applicable, deployed — before the next one
starts. Do not go deep into Phase III while Phase I isn't deployed; this guards against the common
"wide and shallow" failure pattern.

**Running in parallel with every phase:** actual job applications (10–15/week), not gated on the
project's completion.

The Linear project (team `EM`) mirrors this file as the working backlog. This file holds the plan;
Linear holds the work items.

---

## §01 — Process rules

These came out of the hh.ru incident. They are the part of this plan that is not about job boards.

1. **Evidence over assertion.** A claim about an external service needs a URL, a date and a live
   response. "The docs say" is level `docs`, and level `docs` is never enough to schedule work
   against.
2. **Spike gate.** No integration is scheduled without a committed `spikes/<source>/response.json`,
   a terms-of-use verdict quoting the terms and linking them, and a live test. An integration issue
   may not leave Backlog without a link to that artifact.
3. **Definition of done.** A checkbox needs a link to a commit, PR or CI run. Work that exists only
   on an unpushed local branch is In Review, not Done.
4. **Assumption register with expiry dates.** [`docs/ASSUMPTIONS.md`](./ASSUMPTIONS.md).
   Every load-bearing claim carries a verification level, a blast radius, a fallback and an expiry.
5. **Blast radius — N≥3.** No phase may depend on a single external source. The MVP ships with at
   least four connectors across at least two tiers.
6. **Sources are rows, not code.** A source is a row in `sources` plus an adapter class. Losing one
   is a flipped boolean, not a rewritten phase. This is the modelling fix for the incident's root
   cause.
7. **Detection, not hope.** From Phase II, a nightly source contract test makes the next upstream
   closure visible within 24 hours instead of three weeks.

---

## Tooling map

What lives **inside** the Dev Container vs. what runs **outside** it.

**Inside the Dev Container** (`devcontainer.json` / `Dockerfile` / `docker-compose`):
- .NET SDK, Node.js — language runtimes
- PostgreSQL + `pgvector` — as a compose service, for local development only
- Ollama — as a compose service, local embedding model, so matching can be tested from day one without depending on the cloud
- git, GitHub CLI, `dotnet-ef`, other CLI tooling
- Claude Code — via the official `ghcr.io/anthropics/devcontainer-features/claude-code` feature. This sandboxes Claude Code to `/workspace` and applies a network firewall with a domain allowlist. **If Claude Code itself (not just the chat client) should reach the MCP servers below, their domains must be explicitly added to that allowlist** — the firewall blocks anything not listed by default. The same applies to every source domain in `docs/SOURCES.md`.

**Outside the Dev Container:**
- **Render** (API as a Docker web service, frontend as a static site) and **Neon** (Postgres with
  pgvector) — the deployment target. Chosen over Railway on 2026-08-28 when its trial expired and
  left no free path; see A-011. Neither runs "inside" the local container.
- **Sentry, Linear, Slack** — SaaS products. The project only holds an API key/DSN as an environment variable; the services themselves are never self-hosted.
- **MCP endpoints** for the above (`mcp.linear.app`, `mcp.slack.com`, `mcp.sentry.dev`, `mcp.railway.com`) — remote servers that a Claude client connects to (the chat interface, or Claude Code if configured separately). Nothing to install or deploy for these.
- **GitHub Actions** — runs on GitHub-hosted runners, separate from the local machine. Worth building CI on the same base image as the Dev Container, so "works locally" and "works in CI" stay identical.
- **Cowork, Claude Design, Claude in Chrome** — Claude interfaces entirely outside the repository; nothing to provision.

---

## Phase 0 — Foundation and source qualification

Goal: working environment, data schema, and — new in Revision 2 — proof that the sources exist
before a line of integration code is written.

- [x] Repository, `LICENSE` (MIT), `.gitignore`, README skeleton — EM-5
- [x] Dev Container: .NET SDK, Node.js, Postgres+pgvector service, Ollama service, Claude Code feature — EM-6
- [x] Draft Postgres schema: `vacancies`, `sources`, `applications`, `embeddings` — EM-7
- [x] GitHub Actions skeleton: build on push, same base image as the Dev Container — EM-8
- [x] Linear project set up, one issue per task (this plan becomes the initial backlog) — EM-10
- [x] EF Core migrations against the Phase 0 schema — EM-12
- [x] Source registry and assumption register committed to `docs/` — EM-44
- [x] Rebuild the sources schema: rows with compliance columns, not an enum — EM-51 (model +
      `RebuildSourcesSchema` migration, applied and verified against a live Postgres)
- [x] Spike: Greenhouse boards API (Tier A) — EM-45 (`spikes/greenhouse/`, cleared)
- [x] Spike: Lever postings API (Tier A) — EM-46 (`spikes/lever/`, cleared; **re-run 2026-08-27**
      against the three registry tokens `qonto`/`remofirst`/`peerspace` — 200/200/200, 44 postings
      — replacing the original guessed `palantir` sample. The live-endpoint assertion is assigned
      to EM-55, since the test infrastructure is EM-23 in Phase II.)
- [x] Spike: Himalayas remote jobs API (Tier B) — EM-47 (`spikes/himalayas/`, **not cleared** —
      terms require Himalayas' prior written approval; source stays disabled)
- [x] Spike: Jobicy remote jobs API (Tier B) — EM-48 (`spikes/jobicy/`, cleared)
- [x] Spike: Arbeitnow job board API (Tier B, EU/DACH) — EM-49 (`spikes/arbeitnow/`, cleared)
- [x] Target-company registry — EM-50 (scope revised 2026-08-26: a 6-company live-verified seed
      against the criteria derived from the author's own CV — junior level, .NET/C# primary
      stack, Tbilisi/Georgia hiring-geography constraint — not 30 collected up front; see
      `docs/SOURCES.md`. Registry now grows only from applications actually sent.)
- ~~hh.ru API access registered~~ — EM-9, **falsified**. See A-000.

**Exit criterion:** `devcontainer up` brings up an empty API and database with no errors, and
`spikes/` contains real postings from at least four sources across two tiers, each with a written
terms-of-use verdict. **Met 2026-08-26** — see `docs/SOURCES.md`'s gate status note.

**Gate:** do not start Phase I with fewer than four sources at level `spike`, at least two of them
Tier A, at least four cleared for public display. **Met**, without Himalayas: Greenhouse, Lever
(Tier A) + Jobicy, Arbeitnow (Tier B) are all `spike` and cleared.

**Phase 0 status, 2026-08-27:** gate met and **all Phase 0 items closed.** EM-46 was the last one
outstanding — it had been reopened for sampling a guessed token (`palantir`) instead of the three
registry companies its own criteria required. Re-run and re-committed on 2026-08-27 against
`qonto`/`remofirst`/`peerspace`.

One acceptance criterion was deliberately *not* satisfied in place: EM-46 asked for a committed
live-endpoint test, but the test infrastructure is EM-23, a Phase II item. Satisfying it here would
mean pulling xUnit into Phase 0 and breaking the phase gate in the other direction, so the
obligation is assigned to EM-55 (nightly source contract test).

**How salvaged pre-Revision-2 code is treated against this gate** (the clarification EM-56 asked
for and never got): EM-11/15/16 were written before Revision 2 existed, against the old hh.ru
plan, and were carried forward rather than rewritten. They are read-path code with no source
integration in them, so they cannot violate a gate about source qualification. The gate binds
**ingest** work — EM-52/53 — and those were genuinely not started until the four spikes landed.
A future phase gate should be read the same way: it constrains new work against external sources,
not the salvage of code that touches none.

---

## Phase I — MVP: multi-source discovery

Goal: a deployed vertical slice, multi-sourced from the first commit. No single external source
can block it.

- [x] ASP.NET Core Web API: endpoints for listing vacancies — EM-11
- [x] REST endpoints: list, filter by keyword/stack/date — EM-15
- [x] Frontend: React + TS + Vite, vacancy list, basic filters — EM-16
- [x] `IJobSource` connector abstraction + source-agnostic ingest command — EM-52
- [x] Four adapters: Greenhouse, Lever, **Arbeitnow**, Jobicy — EM-53 (swapped in for Himalayas,
      2026-08-26: Himalayas' spike found its terms require prior written approval Himalayas
      hasn't given, so it doesn't qualify as "cleared for public display." Arbeitnow was already
      spiked and cleared as part of the same gate check, so it moves up from its EM-19 follow-on
      slot rather than leaving the MVP at three adapters. Revisit if Himalayas' approval comes
      through — see A-003 in docs/ASSUMPTIONS.md.)
- [x] Render source attribution on every vacancy card — EM-54
- [x] Initial deployment — EM-17 (**Render + Neon, live 2026-08-28.** API
      `employme-api.onrender.com` as a Docker web service, frontend `employme-4uql.onrender.com`
      as a static site, Postgres on Neon `eu-central-1` with pgvector 0.8.6. Railway was the
      original target and was dropped when its trial expired — see A-011. **Required environment
      variables:** `ConnectionStrings__Default` in Npgsql key-value form, *not* the URI Neon hands
      you — Npgsql rejects `channel_binding`; `Ingest__TriggerToken`, without which the manual
      ingest endpoint refuses to run at all on a public deployment; and `Cors__AllowedOrigins__0`,
      the frontend's origin, without which the API allows no cross-origin caller.
      `Ingest__PublicDeployment` is deliberately *not* required — unset resolves to "public unless
      Development", so forgetting it keeps the compliance guards on rather than silently switching
      them off.)
- ~~hh.ru API client~~ — EM-13, cancelled with A-000.
- ~~Manual hh.ru ingest job~~ — EM-14, cancelled; replaced by the source-agnostic command in EM-52.

- [x] Bound `RawPostings` to one row per posting — EM-58 (it appended a row per *fetch*, so the
      table grew with polling frequency: ~6 MB a run against Neon's 0.5 GB, about four days once
      EM-18 schedules it hourly. The newest fetch now overwrites the previous one, enforced by a
      unique index rather than by the service. Verified on the deployed instance: three
      consecutive full runs, count unchanged at 1,730, the third reporting `created=0`.)
- [x] Spike: Ashby boards API (Tier A) — EM-57 (`spikes/ashby/`, A-008 at `spike`. **Not cleared**
      — the API's stated purpose addresses the Ashby customer publishing their own careers page,
      not a third party aggregating boards. Compensation measured at **94% coverage** against
      A-009's 0% on Greenhouse and 0% on Lever, which is why it is worth returning to.)
- [x] Map seniority from fields the sources already return — EM-59 (Jobicy `jobLevel`, Arbeitnow
      `job_types`. 388 of 1,730 rows classified; the rest stay `Unknown`, and the filter excludes
      `Unknown` rather than counting it as a match. `hiring_geo` was split out to EM-63 after
      measurement showed Arbeitnow carries **no** eligibility field — 0 of 650 — so 63% of the
      database cannot be answered without EM-31.)
- [ ] One more adapter: Ashby (A) — EM-19 (**moved to Phase III.** Blocked on the target-company
      registry, not on the source: no Ashby board found hires from Georgia. Its value is the
      compensation data EM-29 needs, and the route is ingest with `public_deploy_enabled = false`
      rather than a terms reinterpretation.)

**Exit criterion:** the site is live, shows real postings from at least four sources across two
tiers, filtering works, every card credits its source per that source's terms, and no Tier D
connector is enabled in the deployed environment. **Met 2026-08-28**, verified against the
deployed instance rather than a local run:

- 995 vacancies from four sources across two tiers — Greenhouse 229 and Lever 37 (Tier A),
  Arbeitnow 629 and Jobicy 100 (Tier B).
- Filtering answers on the deployed API: `keyword=engineer` → 336, `location=Berlin` → 116. A
  keyword of a bare `%` returns 225 rather than all 995, so the ILIKE escaping holds in production.
- Every row carries its source name, home URL and `attributionRequired`.
- The guards that only a deployed environment can exercise: ingest returns `401` with no token and
  with a wrong one, and Himalayas — enabled `false`, cleared for public display `false` — is
  skipped with its reason named in the report. No Tier D row exists in the deployed database at
  all.

**Still open in this phase:** nothing. EM-19 moved to Phase III; EM-63 to Phase III.

---

## Phase II — Reliability and source health

Goal: stops being a script, becomes a service — and gains the immune system Revision 1 lacked.

- [ ] Nightly source contract test with alerting — EM-55
- [ ] Scheduler: `BackgroundService`/Hangfire — scheduled ingest, honoring each source's `min_poll_interval` — EM-18
- [ ] `HttpClient` + Polly: retries and rate-limit handling for external API resilience — EM-20
- [ ] Serilog structured logging + health checks — EM-21
- [ ] Sentry SDK integrated for error monitoring — EM-22
- [ ] Tests: xUnit (unit) + Testcontainers (integration, against a real Postgres) — EM-23
- [ ] GitHub Actions: tests + lint + build on every PR — EM-24
- [ ] Slack channel (or Claude Tag) wired to CI/deploy notifications — EM-25

**Exit criterion:** the service refreshes on schedule, survives an external API outage without data
loss, detects an upstream endpoint closure within 24 hours, is covered by tests and CI, and errors
surface in Sentry.

---

## Phase III — Personalization and the semantic layer

Goal: the layer that differentiates this from a generic aggregator.

- [x] `pgvector` extension enabled in Postgres — EM-26 (landed with the Phase 0 schema, not as
      Phase III work: `HasPostgresExtension("vector")` + `InitialCreate`'s `vector(1024)`
      column, on `main`. Closed by the 2026-08-27 sync audit. Nothing writes to the column
      yet — EM-27/EM-28 remain Phase III.)
- [ ] Ollama deployed with a multilingual embedding model (bge-m3 or e5) — EM-27
- [ ] Embeddings for vacancies and for my own CV/profile, with caching to avoid recomputation — EM-28
- [ ] Fit-score: cosine similarity between CV and vacancy, with an explanation (matched vs. missing requirements) — EM-29
- [ ] Semantic deduplication: the same vacancy across sources/languages collapses into one card — EM-30
- [ ] LLM extraction into strict JSON: seniority, stack, work format, language requirement — EM-31
- [ ] Manual labeling of 50 vacancies + a script measuring extraction/matching precision — EM-32
- [ ] UI for the fit-score explanation mocked in Claude Design before implementation — EM-33

**Exit criterion:** the vacancy list is sorted by personal relevance, duplicates are collapsed, and
there is a measured precision figure. Until that number exists, the README claims nothing about
matching quality (A-010).

---

## Phase IV — Application tracker (personal CRM layer)

Goal: a tool used daily, not a demo for a screenshot.

- [ ] Status model: viewed / applied / interview / rejected / offer, plus notes and dates — EM-34
- [ ] Endpoints for status changes — EM-35
- [ ] UI: list or kanban board with status management, mocked in Claude Design first — EM-36
- [ ] Dashboard: applications per week, conversion by stage — EM-37
- [ ] Manual QA pass on the deployed Render environment via Claude in Chrome — EM-38

**Exit criterion:** the spreadsheet is retired — the entire job-search workflow runs through this
tool.

---

## Phase V — Portfolio polish

Goal: the project survives 20 minutes of interview questions.

- [ ] Full README, architecture diagram, setup instructions, screenshots — EM-39
- [ ] Write-up: why ATS boards over aggregators, why self-hosted embeddings, why the host changed — and the hh.ru post-mortem — EM-40
- [ ] Metrics section documenting the precision figures from Phase III — EM-41
- [ ] Repository cleanup: personal application data removed, seed data added if needed for demos — EM-42
- [ ] Interview talking points: architectural decisions and their trade-offs, written down in advance — EM-43

**Exit criterion:** the project is ready to be linked from a CV and defended live.

---

## Open questions

1. ~~How does the target-company registry grow?~~ **Resolved 2026-08-26, EM-50.** Hybrid: a
   small live-verified seed (3 Greenhouse + 3 Lever companies) just to unblock EM-45/46's spikes
   with real evidence instead of spike-of-convenience tokens (GitLab, Palantir) — not 30 collected
   up front. From here on the registry grows only when an application is actually sent to a new
   company. See `docs/SOURCES.md`'s "Target-company registry" section.
2. **Does normalization across six upstream shapes preserve the structured signal Phase III needs?**
   A lowest-common-denominator mapping would leave the fit-score with title, company and URL. A-009.
3. **Is a self-hosted multilingual model good enough across English, German and Russian postings?**
   Unmeasured until EM-32. A-010.

---

## Estimated timeline (3–4 hours/day)

Revision 1's estimate assumed one source with a search endpoint. Revision 2 adds source
qualification up front and six adapters instead of one.

| Phase | Content | Duration |
|---|---|---|
| 0 | Foundation + source qualification | ~1 week |
| I | MVP multi-source discovery | ~2.5 weeks |
| II | Reliability + source health | ~2 weeks |
| III | Personalization + semantic layer | ~2 weeks |
| IV | Application tracker | ~4–5 days |
| V | Portfolio polish | ~3–4 days |
| **Total** | | **~8–9 weeks** |
