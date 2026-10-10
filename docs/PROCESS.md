# Process

How work moves from an idea to `main`: where each kind of truth lives, the rules every change
follows, the status model, and the paths a change can take. Names, titles and merge methods are in
[`CONVENTIONS.md`](./CONVENTIONS.md); the roadmap is [`PLAN.md`](./PLAN.md).

---

## Where each kind of truth lives

| What | Lives in | Not in |
|---|---|---|
| The roadmap: phases, their items and exit criteria | `PLAN.md` | Linear descriptions |
| Work items | Linear, team `EM` | GitHub issues (they only mirror) |
| Whether an item is done | the `PLAN.md` checkbox and the Linear status, kept in step (below) | prose "status" paragraphs |
| Decisions taken for a ticket | the ticket's **Decided** section in Linear | chat, memory |
| Evidence that something works | the PR, its commits and its CI run | assertions in docs |
| What a source permits and how far it is verified | `SOURCES.md` | code comments |
| Load-bearing claims and their expiry | `ASSUMPTIONS.md` | PLAN |
| What happened and why the project changed course | `history/` | every other document |

Every document other than `history/` describes the current state only.

---

## Rules

These came out of the hh.ru incident (`history/2026-08-22-hh-ru.md`, `ASSUMPTIONS.md` A-000).
They bind Claude as much as the maintainer.

1. **Evidence over assertion.** A claim about an external service needs a URL, a date and a live
   response. "The docs say" is level `docs`, and level `docs` is never enough to schedule work.
2. **Spike gate.** No integration is scheduled without a committed `spikes/<source>/response.json`,
   a terms-of-use verdict that quotes and links the terms, and a live test. An integration ticket
   does not leave Backlog without a link to that spike.
3. **Definition of done.** See [Status model](#status-model). A box is ticked only with a link to
   the PR that did the work; nothing is done on someone's word.
4. **Assumptions expire.** Every load-bearing claim is in `ASSUMPTIONS.md` with a verification
   level, a blast radius, a fallback and an expiry.
5. **Blast radius, N≥3.** No phase depends on a single external source.
6. **Sources are rows, not code.** A source is a row in `sources` plus an adapter class; losing one
   is a flipped boolean, not a rewritten phase.
7. **Detection, not hope.** The nightly contract test (`contract.yml`) makes an upstream closure
   visible within 24 hours.
8. **Every change has a ticket** in the milestone of the phase that is open. The only other path
   into `main` is the bot-only dependency promotion ([`dependency-updates.md`](./dependency-updates.md)).

---

## Phases

A phase is built on its own branch, cut from `main` when the previous phase lands, so there is
always an open phase branch to work on. Each ticket gets a branch cut from the phase branch and
returns to it through a pull request.

A phase lands on `main` when:

1. every item in `PLAN.md` is ticked or explicitly moved to a later phase;
2. its exit criterion is met, with the evidence linked in the phase PR;
3. the maintainer has checked the phase by hand on staging, against the plan in the phase PR's
   "How to check manually" section;
4. the maintainer merges it.

Then: tag `phase-<n>` with a GitHub Release quoting the exit criterion, a `history/` entry for the
phase exit, the phase's start and end dates in `PLAN.md`, and the next phase branch.

**Phase gate.** The next phase does not start until the previous one is working and, where it
applies, deployed.

---

## Status model

One model, three places, always in step:

| Event | `PLAN.md` | Linear | GitHub |
|---|---|---|---|
| Work starts on a ticket branch | `[ ]` | In Progress | — |
| Ticket PR opened into the phase branch | `[ ]` | In Review | PR in the phase's milestone |
| Ticket PR merged into the phase branch | `[x] <title> — EM-n — #PR` | **Done** | — |
| Phase PR merged into `main`, exit criterion met | phase heading gets its end date | — | tag `phase-<n>`, milestone closed |

The box is ticked in the ticket PR itself, with that PR's number. A phase PR's title carries no
ticket ids: the Linear–GitHub integration links every id it finds in a title and moves those
tickets back to In Progress (EM-65).

---

## Paths a change can take

| Situation | What to do |
|---|---|
| Planned work | Ticket branch from the phase branch → PR `EM-n: …` → squash into the phase branch |
| A gap in a ticket that already merged, while its phase is still open | Short branch from the phase branch → PR `Fix: EM-n - …` → squash into the phase branch. No new ticket; link the PR from the Linear ticket. `PLAN.md` is not touched |
| Production is broken and cannot wait for the phase | Ticket (`type/bug`) → branch from `main` → optionally deploy it to staging → PR `Hotfix: EM-n - …` → squash into `main` → check production → merge `main` into the open phase branch |
| A gap in a phase that has already merged, not urgent | A ticket in the open phase's milestone **and** a mirroring GitHub issue `EM-n: …` whose body names the phase the gap belongs to |
| A review round on an open PR | Commits on the same branch; they fold into the squash commit |
| Dependency updates | Bots only, through `deps` |

---

## Checking by hand

- Every PR has a **How to check manually** section: what to run or click, and what you should see.
- Before a phase PR merges, its description holds the combined plan for the phase, and the
  maintainer runs it on staging (`DEPLOYMENT.md`).
- CI and Claude's own runs are evidence, not the check. A phase is not closed on either.

---

## Decisions

A choice with more than one reasonable option, or one that touches process, conventions,
architecture, data, permissions or an external system, is made in an interview: every open item
put as a question with options and a recommendation, answered by the maintainer. The answers go
into the ticket's **Decided** section straight away, then into the document they change. A task
that resumes in a new session starts by reading Decided.

---

## Housekeeping

- A ticket's worktree is removed once its PR merges.
- Ticket branches, local and remote, are pruned in one pass after their phase lands on `main`.
  Their commits stay reachable from `refs/pull/<n>/head`.
- Personal notes and session transcripts go to the gitignored `notes/`, never to `docs/`.
