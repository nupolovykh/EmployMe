# EmployMe — Personal Job Vacancy Aggregator

*(working title)*

A personal tool that collects job postings from employers' own ATS boards and from public
remote-job APIs, and — in the phases still ahead — ranks them against my own CV, collapses
duplicates across sources and tracks my applications.

Built as a portfolio project to practise a production-shaped engineering process, not only a
language, while it drives my actual job search.

## Why this exists

Generic aggregators show the same list to everyone. This one is meant to be personal:

- It reads **employers' own ATS boards** directly, against a target-company registry that is mine,
  so it sees postings that never reach a job board.
- Every source's terms of use are **read and recorded before an integration is written**, and
  every card credits its source ([`docs/SOURCES.md`](./docs/SOURCES.md)).
- It is planned to rank vacancies against **my** stack with an explainable score, to deduplicate
  across sources and languages, and to replace my application spreadsheet — with matching quality
  **measured** against a hand-labelled sample before anything is claimed about it.

## What works today

- Ingest from four sources across two tiers — Greenhouse and Lever (employer ATS boards, fetched per
  company) and Jobicy and Arbeitnow (public remote-job APIs). Sources are database rows with an
  adapter class, not an enum: adding one is a class plus a row, losing one is a flipped boolean.
- A vacancy list with keyword, location, date and seniority filters, crediting its source on every
  card.
- Scheduled ingest that honours each source's minimum poll interval, retries behind a Polly
  pipeline, structured logs, a readiness check that names failing sources, and Sentry.
- A nightly contract test against every live source, so an upstream endpoint that closes shows up
  within a day.

Deployed on Render and Neon. Status per item: [`docs/PLAN.md`](./docs/PLAN.md).

## Planned

- Semantic fit-score between my CV and each vacancy, with an explanation (Phase III).
- Cross-source semantic deduplication and LLM extraction into structured fields (Phase III).
- Application tracker: viewed → applied → interview → rejected → offer (Phase IV).

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API, EF Core |
| Database | PostgreSQL with pgvector |
| Frontend | React + TypeScript (Vite) |
| Scheduling | `BackgroundService`, woken hourly by a GitHub Actions cron on the free host |
| Resilience | `HttpClient` + Polly |
| Logging and errors | Serilog, Sentry |
| Testing | xUnit, integration tests against a real Postgres |
| CI | GitHub Actions, inside the Dev Container |
| Deployment | Render (API and static frontend), Neon (Postgres) |
| Embeddings (planned) | Ollama with a multilingual model |
| Dev environment | Dev Container |

How the pieces fit: [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md).

## Getting started

The project runs inside a [Dev Container](https://containers.dev/): open the repository in VS Code
and choose **Reopen in Container**. Running, testing and everything else:
[`docs/DEVELOPMENT.md`](./docs/DEVELOPMENT.md).

## Project status

Phase II (reliability and source health) is in review; Phases 0 and I are done. The plan is at
Revision 2: the original design, built on hh.ru, was falsified and rebuilt around multiple sources —
the post-mortem is [`docs/history/2026-08-22-hh-ru.md`](./docs/history/2026-08-22-hh-ru.md) and
`docs/ASSUMPTIONS.md` entry A-000, kept in the repository on purpose.

## License

[MIT](./LICENSE).

## Author

**Nikita Polovykh** — Junior Software Developer, Tbilisi, Georgia
[github.com/nupolovykh](https://github.com/nupolovykh) · devpolovykh@protonmail.com
