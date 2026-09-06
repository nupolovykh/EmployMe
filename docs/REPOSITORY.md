# Repository settings and GitHub features

Naming — branches, commits, PR titles, when a GitHub issue is appropriate — lives in
[`docs/CONVENTIONS.md`](./CONVENTIONS.md) and is not repeated here. *(That file arrives with
the Phase I branch, PR #16; until it merges, this link is dead on `main`.)*

This file covers the other half: the GitHub-side configuration of the repository — protection
rules, which product features are used and which are deliberately not, and how phases are
represented on GitHub. Where that configuration can be committed it is
(`.github/labels.yml`, `.github/dependabot.yml`, `.github/pull_request_template.md`); where
GitHub only stores it as settings state, the intent is recorded below so at least the decision
is reviewable.

---

## State this was written against (2026-09-01)

Facts, so a later reader can tell what has since changed:

- `main` is **unprotected** — a stray `git push` lands unreviewed, a force-push rewrites it.
- **No tags, no releases.** Nothing marks the moment Phase 0's gate was met; that fact exists
  only as a paragraph in `PLAN.md`.
- **No milestones**, although the plan is already six phases with exit criteria.
- Labels are ad hoc: GitHub's stock set (including `help wanted`, which on a
  single-maintainer repository is misleading) plus `backend` / `frontend` / `deploy`.
- Five merged branches were never deleted; automatic branch deletion is off.
- `claude/repo-vulnerability-audit` contained no vulnerability audit — one commit adding a
  six-line Dependabot config. That commit is the ancestor of this branch; the misleading name
  is retired with it.
- CI is a single `dotnet build`, not a required check.

---

## Phases on GitHub: milestones and tags

`PLAN.md` is the plan; GitHub should show what actually shipped against it.

**Milestones — one per phase.** `Phase 0 — Foundation and source qualification` … `Phase V —
Portfolio polish`, each with that phase's exit criterion as its description, each PR assigned
to its phase's milestone. A phase is a milestone rather than a label because an item belongs
to exactly one phase, and because GitHub renders a milestone as a completion bar — which is
the "is Phase I done?" question the phase gate asks, answered without reading anything.

**Tags and releases — one per phase exit.** At each exit criterion, an annotated tag
`phase-<n>` and a GitHub Release whose notes quote the criterion and link the evidence that
satisfies it. This is `PLAN.md` §01.3 — "a checkbox needs a link to a commit, PR or CI run" —
applied to phases instead of checkboxes, and it gives the repository a legible timeline for
anyone reading it as a portfolio piece. Phase 0's tag is retroactive, on the merge commit of
PR #14, where the gate was met on 2026-08-27.

## Labels

Defined in [`.github/labels.yml`](../.github/labels.yml) and applied by the `Sync labels`
workflow, so the taxonomy is a reviewable file rather than UI state. Two axes — `area/*` and
`type/*` — plus a rare `status/*`. Old names map as `backend` → `area/api`, `frontend` →
`area/web`, `deploy` → `area/infra`, `documentation` → `area/docs`, `enhancement` →
`type/feature`.

Two labels exist because of this project's own process: `type/compliance` (terms of use,
tiering, attribution — the surface that killed revision 1) and `status/needs-evidence` (a PR
making a claim with no URL, date or live response, per §01.1).

**The taxonomy applies to pull requests only.** GitHub Issues keep the stock set — that is
what `docs/CONVENTIONS.md`'s "no custom label scheme" is about, and that line is scoped to
Issues on the Phase I branch so the two documents say one thing rather than two.

---

## Deliberately not used

**No Wiki.** A GitHub wiki is a separate git repository: not reviewed in pull requests, not
covered by CI, not diffable against the code that made it wrong, and not subject to the
ruleset below. Every rule this project runs on — evidence with a date, assumptions with expiry
dates, docs updated in the same PR as the code — depends on documentation moving through the
same review path as the code. `docs/` does that; a wiki would create a second, unreviewed copy
that drifts silently. Keep the feature off.

**No GitHub Project board.** Linear holds the backlog, mirrors `PLAN.md`, and is where the EM
ids come from. A second board needs manual synchronisation, and a stale board is worse than no
board. Milestones give the GitHub-side visibility for free, from data that already exists.
Revisit only if Linear is dropped.

**No CODEOWNERS, no required reviewers.** Single maintainer, and GitHub does not let you
approve your own pull request — a review requirement would block every merge. The substitute
is an automated reviewer on PRs (Copilot code review, or a Claude review action) plus the
required `build` check.

**No `CONTRIBUTING.md`.** `docs/CONVENTIONS.md` and this file are the equivalent, and are
honest about the repository being single-maintainer.

**GitHub Issues are not a second backlog** — see `docs/CONVENTIONS.md`. The consequence on the
PR sidebar is that "Development" reads *None yet* and always will: the work items are Linear
tickets, and the link to them is the branch name and PR title, not that field.

---

## Settings that cannot be committed

Apply once in the GitHub UI.

### Ruleset on `main` (Settings → Rules → Rulesets)

| Rule | Setting | Why |
|---|---|---|
| Target | `main` | |
| Restrict deletions | on | |
| Block force pushes | on | |
| Require a pull request before merging | on, **0 required approvals** | A solo maintainer cannot approve their own PR; the value is that everything lands as a reviewable diff with CI attached |
| Require conversation resolution | on | Review threads, including automated ones, cannot be merged past silently |
| Require status checks | on: `build`, plus "require branches to be up to date" | |
| Require linear history | **off** | Merge commits are the chosen strategy, and the commits here are atomic enough that squashing would destroy real history |
| Bypass list | **empty** | A bypass for the only maintainer makes the ruleset decorative. For a genuine emergency, disable the ruleset — which is logged — and re-enable it |

When Phase II adds test and lint jobs (EM-23, EM-24), add them to the required checks. The
`build` workflow deliberately has no `paths` filter: a required check that skips on some PRs
leaves those PRs permanently unmergeable.

### Repository settings

- **Automatically delete head branches:** on. Then delete the merged strays by hand:
  `claude/devcontainer-setup`, `claude/phase-0-api-foundation`,
  `devpolovykh/em-46-spike-lever-postings-api-tier-a`,
  `devpolovykh/em-51-sources-schema-rebuild`.
- **Wiki:** off.
- **Allow merge commits:** on. **Squash / rebase merging:** off, so the strategy is not a
  per-merge decision.
- **Topics:** currently none. `dotnet`, `aspnetcore`, `csharp`, `postgresql`, `pgvector`,
  `embeddings`, `semantic-search`, `job-search`, `ollama`, `react`, `typescript`. Free
  discoverability for a repository whose purpose is to be found.
- **Homepage:** `https://employme-4uql.onrender.com` — the deployed frontend, not the API
  (`employme-api.onrender.com`). Railway was the target when EM-17 was written; the trial
  expired and the deploy landed on Render + Neon instead (`docs/ASSUMPTIONS.md` A-011,
  which arrives with PR #16).
- **Security → Secret scanning + push protection:** on (free on public repositories). This
  repository is public and `.env` is gitignored rather than absent; push protection is the net
  for the day that fails.
- **Security → Dependabot alerts:** on, to pair with `.github/dependabot.yml`.
