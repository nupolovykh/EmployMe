# CLAUDE.md

Entry point for Claude Code in this repository. It holds this project's hard rules, where to read
before acting, and the environment traps that break a build. Everything else lives in `docs/`.

How Claude behaves — mandate, truth, decisions, reporting — is in `.claude/rules/`, a mirror of
`~/.claude/rules/`. What Claude may do freely and what needs the maintainer's yes is enforced by
`.claude/settings.json` and `.claude/hooks/guard.py`, mirrors of their `~/.claude` originals. A
session-start check warns when the mirrors drift; `~/.claude` is the reference copy.

## Before you act, read

| Before you… | Read |
|---|---|
| name a branch, write a PR title, merge | `docs/CONVENTIONS.md` |
| start, finish or tick anything; fix, hotfix; check by hand | `docs/PROCESS.md` |
| assume a feature or service exists | `docs/PLAN.md` checkboxes |
| touch a source, an adapter or a poll interval | `docs/SOURCES.md`, `docs/ASSUMPTIONS.md` — update both with the change |
| write a migration | `docs/DATA-MODEL.md` — update it in the same PR |
| touch configuration or an environment variable | `docs/CONFIGURATION.md` |
| touch deployment, Render, Neon, Sentry or staging | `docs/DEPLOYMENT.md` |
| run, test or add something locally | `docs/DEVELOPMENT.md` |
| change `src/Web` | `docs/FRONTEND.md` |
| change how the code fits together | `docs/ARCHITECTURE.md` |
| change GitHub settings, rulesets or labels | `docs/REPOSITORY.md` |
| touch dependency updates or the `deps` branch | `docs/dependency-updates.md` |

## Project

EmployMe — a personal job-vacancy aggregator. It ingests postings from employers' own ATS boards
(Tier A) and public remote-job APIs (Tier B), and is planned to rank them against the author's CV,
deduplicate them across sources and track applications. Layout: `src/Api` (ASP.NET Core + EF
Core), `src/Web` (React + TypeScript + Vite), `tests/Api.Tests`, `spikes/<source>/`, `docs/`.

## Hard rules

- **hh.ru is Tier D: never re-add it, and never enable a Tier D source in a deployed
  environment.** Its API refuses unauthorised callers and its terms forbid passing data to third
  parties (`ASSUMPTIONS.md` A-000).
- **Follow the phase gate and the process rules** in `docs/PROCESS.md`; they bind Claude too.
- **Poll intervals come from `sources.min_poll_interval`, never a constant.** Jobicy allows one
  poll an hour and bans projects that ignore it.
- **Do not assume a feature or service exists** — check the `PLAN.md` checkboxes.

## Environment traps

Details and the reasons for each are in `docs/DEVELOPMENT.md` → "Traps".

- Work inside the Dev Container; tools are not assumed to exist outside it.
- The `db` volume mounts at `/var/lib/postgresql`, not `.../data` (Postgres 18). Do not revert it.
- A new external domain must go into `init-firewall.sh`'s `for domain in …` list, or calls to it
  fail as network errors that look like an unavailable source.
- `Network is unreachable` on NuGet/npm with the domain allowlisted: retry, or
  `sudo bash .devcontainer/init-firewall.sh`.
- Keep every `TargetFramework` on `net10.0`, and prove a package change with `dotnet run`, not
  only `dotnet build`.
- `dotnet run --project src/Api` is always Development; use `--no-launch-profile` to exercise the
  production guards. Nothing loads `.env`.

## Commands

```bash
dotnet build EmployMe.sln
dotnet run --project src/Api
dotnet test tests/Api.Tests --filter "Category!=Contract"   # what CI runs
cd src/Web && npm install && npm run dev                    # proxies /api to :5000
```
