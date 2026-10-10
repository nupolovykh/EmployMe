# Development Plan

The roadmap: phases, their items and their exit criteria. **Revision 2** — Revision 1 was built
around hh.ru and falsified (`history/2026-08-22-hh-ru.md`).

How work moves, what "done" means and how a box gets ticked are in [`PROCESS.md`](./PROCESS.md).
Linear (team `EM`) holds the work items; each phase is a Linear milestone and a GitHub milestone.
A ticked box links the PR that did the work.

**Running alongside every phase:** actual job applications, 10–15 a week, not gated on the
project.

---

## Phase 0 — Foundation and source qualification

Goal: a working environment, a data schema, and proof that the sources exist before any
integration code is written.

- [x] Repository, `LICENSE` (MIT), `.gitignore`, README skeleton — EM-5 — `f47ba3e` (initial commit)
- [x] Dev Container: .NET SDK, Node.js, Postgres + pgvector, Ollama, Claude Code — EM-6 — #2
- [x] Draft Postgres schema: `vacancies`, `sources`, `applications`, `embeddings` — EM-7 — #3
- [x] GitHub Actions skeleton on the Dev Container image — EM-8 — #3
- [x] Linear project, one issue per task — EM-10 — (Linear, outside the repository)
- [x] EF Core migrations against the Phase 0 schema — EM-12 — #3
- [x] Source registry and assumption register in `docs/` — EM-44 — #6
- [x] Sources as rows with compliance columns, not an enum — EM-51 — #6
- [x] Spike: Greenhouse boards API (Tier A) — EM-45 — #9
- [x] Spike: Lever postings API (Tier A), re-run against the registry — EM-46 — #9, #14
- [x] Spike: Himalayas (Tier B) — not cleared, source stays disabled — EM-47 — #9
- [x] Spike: Jobicy (Tier B) — EM-48 — #9
- [x] Spike: Arbeitnow (Tier B, EU/DACH) — EM-49 — #9
- [x] Target-company registry: live-verified seed — EM-50 — #9
- ~~hh.ru API access~~ — EM-9, falsified (A-000)

**Exit criterion:** `devcontainer up` brings up an empty API and database with no errors, and
`spikes/` holds real postings from at least four sources across two tiers, each with a written
terms-of-use verdict. **Gate:** at least four sources at `spike`, two of them Tier A, four cleared
for public display. **Met 2026-08-27** — `history/2026-08-27-phase-0-exit.md`, tag `phase-0`.

---

## Phase I — MVP: multi-source discovery

Goal: a deployed vertical slice, multi-sourced from the first commit.

- [x] Web API: endpoints for listing vacancies — EM-11 — #16
- [x] Filters: keyword, location, date — EM-15 — #16
- [x] Frontend: React + TS + Vite, vacancy list, basic filters — EM-16 — #16
- [x] `IJobSource` abstraction and source-agnostic ingest — EM-52 — #16
- [x] Four adapters: Greenhouse, Lever, Arbeitnow, Jobicy — EM-53 — #16
- [x] Source attribution on every vacancy card — EM-54 — #16
- [x] Initial deployment: Render + Neon — EM-17 — #16
- [x] One raw posting row per posting, not per fetch — EM-58 — #16
- [x] Spike: Ashby boards API (Tier A) — not cleared — EM-57 — #16
- [x] Seniority mapped from the fields sources return — EM-59 — #16
- ~~hh.ru API client~~ — EM-13, cancelled with A-000
- ~~Manual hh.ru ingest~~ — EM-14, cancelled; replaced by EM-52

**Exit criterion:** the site is live, shows real postings from at least four sources across two
tiers, filtering works, every card credits its source per its terms, and no Tier D connector is
enabled in the deployed environment. **Met 2026-08-28** — `history/2026-09-06-phase-1-exit.md`,
tag `phase-1`.

---

## Phase II — Reliability and source health

Goal: stops being a script, becomes a service — and gains the immune system Revision 1 lacked.

- [x] Nightly source contract test with alerting — EM-55 — #42
- [x] Scheduled ingest honouring each source's `min_poll_interval` — EM-18 — #40
- [x] `HttpClient` + Polly: retries and rate-limit handling — EM-20 — #37
- [x] Serilog structured logging and health checks — EM-21 — #38
- [x] Sentry error monitoring — EM-22 — #39
- [x] Tests: xUnit unit tests, integration tests against a real Postgres — EM-23 — #41
- [x] CI: tests, format check and lint on every PR — EM-24 — #43
- [x] Slack alerts on a red `main` and a failed contract run — EM-25 — #43
- [ ] Linear–GitHub automation drags tickets named in a PR title back to In Progress — EM-65
- [x] Development rules, Claude's rules and the documentation restructured — EM-66 — #52

**Exit criterion:** the service refreshes on schedule, survives an external API outage without data
loss, detects an upstream endpoint closure within 24 hours, is covered by tests and CI, and errors
surface in Sentry. **Not yet met.** Every mechanism exists and has been checked locally and on
staging; what remains is evidence from `main`: a scheduled `ingest.yml` run, a scheduled
`contract.yml` run, and a Sentry event from the production instance. The maintainer's manual
check runs against the plan in the phase PR.

---

## Phase III — Personalization and the semantic layer

Goal: the layer that sets this apart from a generic aggregator.

- [x] `pgvector` enabled in Postgres — EM-26 — #3
- [ ] Ollama with a multilingual embedding model (bge-m3 or e5) — EM-27
- [ ] Embeddings for vacancies and the CV, cached — EM-28
- [ ] Fit-score: CV–vacancy cosine similarity with an explanation — EM-29
- [ ] Semantic deduplication across sources and languages — EM-30
- [ ] LLM extraction into strict JSON: seniority, stack, work format, language — EM-31
- [ ] 50 hand-labelled vacancies and a precision measurement — EM-32
- [ ] Fit-score explanation UI, mocked in Claude Design first — EM-33
- [ ] Ashby adapter, ingested with `public_deploy_enabled = false` — EM-19
- [ ] `country_access`: passport-level reachability per country — EM-60
- [ ] `hiring_geo`: what the employer offers, from structured fields — EM-63
- [ ] Frontend: router and a vacancy page with the description — EM-67
- [ ] Frontend: filters in the URL, `AbortController`, `App.tsx` split, vitest — EM-68
- [ ] Frontend: list redesign in one Claude Design pass with EM-33 — EM-69
- [ ] API: source filter, sort and facet counts — EM-70
- [ ] Search: `pg_trgm` index for the keyword filter — EM-71

**Exit criterion:** the list is sorted by personal relevance, duplicates are collapsed, and there
is a measured precision figure. Until that number exists, the README claims nothing about matching
quality (A-010).

---

## Phase IV — Application tracker

Goal: a tool used daily, not a demo for a screenshot.

- [ ] Status model: viewed / applied / interview / rejected / offer, notes and dates — EM-34
- [ ] Endpoints for status changes — EM-35
- [ ] List or kanban UI, mocked in Claude Design first — EM-36
- [ ] Dashboard: applications per week, conversion by stage — EM-37
- [ ] Manual QA pass on the deployed environment via Claude in Chrome — EM-38
- [ ] Registry grows from applications sent — EM-61
- [ ] Manual channel for vacancies the ingest cannot see — EM-62

**Exit criterion:** the spreadsheet is retired — the whole job search runs through this tool.

---

## Phase V — Portfolio polish

Goal: the project survives 20 minutes of interview questions.

- [ ] Full README, architecture diagram, setup instructions, screenshots — EM-39
- [ ] Write-up: ATS boards over aggregators, self-hosted embeddings, the host change, hh.ru — EM-40
- [ ] Metrics section with Phase III's precision figures — EM-41
- [ ] Repository cleanup: personal data removed, demo seed data if needed — EM-42
- [ ] Interview talking points written down in advance — EM-43
- [ ] `render.yaml`: the Render services as a Blueprint — EM-72

**Exit criterion:** the project is ready to be linked from a CV and defended live.

---

## Open questions

1. **Does normalisation across the upstream shapes keep the signal Phase III needs?** Salary and
   work format survive on some sources only (A-009).
2. **Is a self-hosted multilingual model good enough across English, German and Russian
   postings?** Unmeasured until EM-32 (A-010).

---

## Timeline

| Phase | Started | Finished | Estimate |
|---|---|---|---|
| 0 | 2026-07-28 | 2026-08-27 | — |
| I | 2026-08-27 | 2026-09-06 (deployed 2026-08-28) | — |
| II | 2026-09-18 | — | in review |
| III | — | — | ~2 weeks |
| IV | — | — | 4–5 days |
| V | — | — | 3–4 days |
