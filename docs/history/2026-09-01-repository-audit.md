# 2026-09-01 — The repository's GitHub side is audited

What the repository looked like before `REPOSITORY.md` existed, so a later reader can tell what has
changed:

- `main` was unprotected — a stray push landed unreviewed, a force-push rewrote it.
- No tags, no releases, no milestones, although the plan already had six phases with exit criteria.
  The fact that Phase 0's gate was met existed only as a paragraph in `PLAN.md`.
- Labels were ad hoc: GitHub's stock set, including `help wanted` on a single-maintainer
  repository, plus `backend` / `frontend` / `deploy`.
- Five merged branches were never deleted.
- `claude/repo-vulnerability-audit` contained no audit — one commit adding a six-line Dependabot
  config. It became the ancestor of the hardening branch and the misleading name was retired.
- CI was a single `dotnet build`, not a required check.

PR #17 (2026-09-05) answered it: Dependabot, CI hardening, the `area/*` / `type/*` label taxonomy
in `.github/labels.yml`, and `REPOSITORY.md`. By 2026-09-29 `main` and every phase branch were
protected by rulesets with `build` required, Phases 0 and I were tagged with releases, and every
phase had a milestone.
