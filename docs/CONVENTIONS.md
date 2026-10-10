# Conventions

Names for branches, pull requests, commits and issues, and how each kind of pull request merges.
How work flows between them is in [`PROCESS.md`](./PROCESS.md). Ticket numbers are Linear issues
in team `EM`.

---

## Branches

| Branch | Name | Cut from |
|---|---|---|
| Phase | `<namespace>/phase-<n>-<slug>`, e.g. `polovykh/phase-2-reliability` | `main` |
| Ticket | `<namespace>/em-<n>-<short>`, e.g. `polovykh/em-20-polly-resilience` | its phase branch |
| Fix after a ticket merged | `<namespace>/em-<n>-<short>` | the phase branch |
| Hotfix | `<namespace>/em-<n>-<short>` | `main` |
| Dependency pipeline | `deps` — bots only, never by hand | `main` |
| Staging | `staging` — what the staging service deploys | — |

Namespaces:

- `polovykh/` — branches the maintainer owns, however much of the work a Claude session did.
- `claude/` — branches a Claude session opened on its own.

Every branch except `deps` and `staging` carries a namespace from the moment it is created. Keep
`<short>` to two or three words. A worktree directory is named after its branch with `/` replaced
by `+`: `.claude/worktrees/polovykh+em-66-rules`.

---

## Pull request titles

The title becomes the squash commit's subject on the phase branch, so it is the line people read in
`git log`. CI rejects any other shape (`pr-title.yml`).

| Pull request | Title |
|---|---|
| Ticket work | `EM-<n>: <imperative summary>` — several tickets: `EM-24, EM-25: …` |
| Fix after the ticket merged, phase still open | `Fix: EM-<n> - <what was missing>` |
| Hotfix into `main` | `Hotfix: EM-<n> - <what was broken>` |
| Phase into `main` | `Phase <0\|I\|II\|III\|IV\|V>: <summary>` — no ticket ids |
| Dependency pipeline | the titles the bots give them |

- No ranges (`EM-20–25`): Linear reads only the first number of a range, and search cannot find the
  rest. List every ticket.
- Imperative summary, no trailing period.
- The kind of work is a label, not a prefix: one `type/*` and one or more `area/*` from
  `.github/labels.yml`. The milestone is the phase.

---

## Commits

- Inside a ticket branch the subject is free-form: the squash commit takes the PR title.
- Every commit body says **why**, and ends with a `Verified:` line describing what was actually run
  and what it showed. A commit with nothing to verify (a typo) says so.
- Subject and body fit on one screen: about 20–25 lines, wrapped at ~72 characters. More belongs in
  the PR description or in `ASSUMPTIONS.md` / `SOURCES.md`.
- Commits Claude authored keep the `Co-Authored-By:` trailer the session supplies.
- Merge commits keep git's default message, plus a paragraph when a conflict was resolved.

---

## Merging

| Pull request | Into | Merged as |
|---|---|---|
| Ticket, or `Fix:` after the ticket merged | its phase branch | squash |
| `main`, to bring a phase up to date | a phase branch | merge commit |
| Phase | `main` | merge commit |
| Hotfix | `main` | squash |
| Dependabot update | `deps` | squash, by the pipeline once CI is green |
| Dependency promotion `deps` → `main` | `main` | squash |

The method follows from the kind of pull request; it is never chosen at merge time. A phase lands
as a merge commit so each ticket's squash commit reaches `main` intact. `main` comes into a phase
branch as a merge commit because a squash would copy its changes as a new commit, and the phase
would later deliver them to `main` a second time.

When squashing, trim GitHub's default body to the one-screen rule. Nothing is merged without the
maintainer's go-ahead for that pull request.

---

## GitHub issues

Linear is the backlog. A GitHub issue exists only to mirror a Linear ticket, titled like it
(`EM-<n>: …`), with GitHub's stock labels — the `area/*` / `type/*` taxonomy is for pull requests.
The one standing reason to mirror is a gap found in a phase that has already merged into `main`:
the issue's body names the phase the gap belongs to. Workflows open their own issues for failures
(`contract.yml`, the dependency pipeline).
