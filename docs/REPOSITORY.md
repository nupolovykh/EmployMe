# Repository settings and GitHub features

Naming — branches, commits, PR titles, when a GitHub issue is appropriate — lives in
[`docs/CONVENTIONS.md`](./CONVENTIONS.md) and is not repeated here.

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

## What has changed since (2026-09-29)

- `main` is protected by a ruleset, and phase branches by a second one — see below. `build` is a
  required check on both.
- Phase 0 and Phase I are tagged (`phase-0`, `phase-1`) with releases, and every phase has a
  milestone. Labels come from `.github/labels.yml`.
- **History was rewritten on 2026-09-26.** Author and committer identity was unified to
  `Nikita Polovykh <96892429+nupolovykh@users.noreply.github.com>`, commit messages were brought
  in line with `docs/CONVENTIONS.md`, and every commit was signed. File contents and the shape of
  the history did not change — each new commit has the same tree and parents as the one it
  replaces. The pre-rewrite `main` is kept on `polovykh/backup-main-2026-09-25`, and every old
  commit stays reachable from `refs/pull/<n>/head` as well, so SHAs quoted in closed PRs, in
  Linear and in Render's deploy history still resolve.
- The author branch namespace was renamed from `devpolovykh/` to `polovykh/` on 2026-09-29.
- Render deploys both services from `main`. Until 2026-09-29 it deployed from the Phase I
  branch.

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

The same limit applies to Claude. A Claude session works through the maintainer's own token, so
GitHub sees the maintainer, and no ruleset can make it wait for an approval it could not give
itself. The rule that nothing merges into `main` or a phase branch without the maintainer's
explicit go-ahead therefore lives in `CLAUDE.md`. Enforcing it on GitHub's side needs Claude to
have an account of its own — a machine user or GitHub App with write access — and one required
approval with the maintainer on the bypass list. That is planned, not done.

**No `CONTRIBUTING.md`.** `docs/CONVENTIONS.md` and this file are the equivalent, and are
honest about the repository being single-maintainer.

**GitHub Issues are not a second backlog** — see `docs/CONVENTIONS.md`. The consequence on the
PR sidebar is that "Development" reads *None yet* and always will: the work items are Linear
tickets, and the link to them is the branch name and PR title, not that field.

---

## Settings that cannot be committed

Apply once in the GitHub UI.

### Rulesets (Settings → Rules → Rulesets)

Two rulesets with the same rules: `main` targets the default branch, and `phases` targets
`refs/heads/*/phase-*` — every phase branch, whatever namespace owns it.

| Rule | Setting | Why |
|---|---|---|
| Restrict deletions | on | |
| Block force pushes | on | |
| Require a pull request before merging | on, **0 required approvals** | A solo maintainer cannot approve their own PR; the value is that everything lands as a reviewable diff with CI attached |
| Allowed merge methods | **merge, squash** | Which one a pull request uses follows from its kind — the table under "Merging" in `docs/CONVENTIONS.md` |
| Require conversation resolution | on | Review threads, including automated ones, cannot be merged past silently |
| Require status checks | on: `build`, plus "require branches to be up to date" | Also enforced when a branch is created, so a phase branch can only be cut from a commit whose `build` passed |
| Require linear history | **off** | A phase lands on `main` as a merge commit, which carries its ticket commits in with it |
| Bypass list | **empty** | A bypass for the only maintainer makes the ruleset decorative. For a genuine emergency, disable the ruleset — which is logged — and re-enable it |

When Phase II adds test and lint jobs (EM-23, EM-24), add them to the required checks. The
`build` workflow deliberately has no `paths` filter: a required check that skips on some PRs
leaves those PRs permanently unmergeable.

### Repository settings

- **Automatically delete head branches:** deliberately **off**, and not a rule here. A merged
  branch is the only thing that still resolves the SHAs in a closed PR's review comments and in
  a deploy history — this repository has already had both, with Render serving commits that a
  history rewrite had orphaned. Deleting branches on merge is cheap tidiness bought with the
  ability to answer "what was actually deployed on that date". Branches are pruned by hand when
  they stop being useful, which is a judgement rather than a setting. Commits that went through
  a pull request also stay reachable from `refs/pull/<n>/head`, which GitHub never deletes. A
  squash-merged ticket branch looks unmerged to git, so ticket branches are pruned by hand once
  their phase has landed on `main`.
- **Wiki:** off.
- **Allow merge commits:** on. **Allow squash merging:** on, with the commit title taken from the
  commit or PR title and the body from the commit messages. **Rebase merging:** off. Which of the
  two a pull request uses is fixed by what kind of pull request it is, so the strategy is still
  not a per-merge decision.
- **Topics:** `asp-net-core`, `csharp`, `dotnet`, `postgresql`, `pgvector`, `embeddings`,
  `semantic-search`, `job-search`, `ollama`, `react`, `typescript`. Free discoverability for a
  repository whose purpose is to be found.
- **Homepage:** `https://employme-4uql.onrender.com` — the deployed frontend, not the API
  (`employme-api.onrender.com`). Railway was the target when EM-17 was written; the trial
  expired and the deploy landed on Render + Neon instead (`docs/ASSUMPTIONS.md` A-011). Both
  Render services deploy from `main`.
- **Security → Secret scanning + push protection:** on (free on public repositories). This
  repository is public and `.env` is gitignored rather than absent; push protection is the net
  for the day that fails.
- **Security → Dependabot alerts:** on, to pair with `.github/dependabot.yml`. **Dependabot
  security updates:** off — they always target `main` and would bypass `deps`;
  `security-audit.yml` reports advisories instead.
- **Actions → Workflow permissions:** read-only by default, and *Allow GitHub Actions to create
  and approve pull requests* ticked, so the dependency promotion can open its pull request.
- **Actions secret `DEPS_PAT`:** the token both dependency workflows act with. The full list of
  what the pipeline needs is in `docs/dependency-updates.md`.
