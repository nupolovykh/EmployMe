# Process

How work moves from an idea to `main`. Names, titles and merge methods: [`CONVENTIONS.md`](./CONVENTIONS.md).
The roadmap: [`PLAN.md`](./PLAN.md).

## Where each kind of truth lives

| What | Lives in |
|---|---|
| Phases, their items and exit criteria | `PLAN.md` |
| Work items | Linear, team `EM` (GitHub issues only mirror) |
| Whether an item is done | the `PLAN.md` checkbox and the Linear status, kept in step |
| Decisions taken for a ticket | the ticket's **Decided** section |
| Evidence that something works | the PR, its commits and its CI run |
| What a source permits, how far it is verified | `SOURCES.md` |
| Load-bearing claims and their expiry | `ASSUMPTIONS.md` |
| What happened and why the project changed course | `history/` — every other document describes the current state |

## Rules

They came out of the hh.ru incident (`ASSUMPTIONS.md` A-000) and bind Claude too.

1. **Evidence over assertion.** A claim about an external service needs a URL, a date and a live response.
2. **Spike gate.** No integration without a committed `spikes/<source>/response.json` and a terms-of-use verdict.
3. **Definition of done.** A box is ticked only with a link to the PR that did the work.
4. **Assumptions expire.** Every load-bearing claim is in `ASSUMPTIONS.md` with a level and an expiry.
5. **N≥3.** No phase depends on a single external source.
6. **Sources are rows, not code.** Losing a source is a flipped boolean.
7. **Detection, not hope.** The nightly contract test shows an upstream closure within 24 hours.
8. **Every change has a ticket** in the open phase's milestone; the only other path into `main` is the bot-only dependency promotion.

## Phases

A phase is built on its own branch, cut from `main` when the previous phase lands. Each ticket's branch
is cut from the phase branch and returns to it through a PR. A phase lands on `main` when every item is
ticked or moved, the exit criterion is met with evidence, and the maintainer has checked it by hand on
staging against the phase PR's plan. Then: tag `phase-<n>` with a release, a `history/` entry, the dates
in `PLAN.md`. The next phase does not start before the previous one works.

## Status model

| Event | `PLAN.md` | Linear |
|---|---|---|
| Work starts on a ticket branch | `[ ]` | In Progress |
| Ticket PR opened | `[ ]` | In Review |
| Ticket PR merged into the phase branch | `[x] <title> — EM-n — #PR` (ticked in that PR) | Done |
| Phase PR merged into `main` | end date on the phase | — |

A phase PR's title names no ticket ids: Linear links every id in a title and moves those tickets back to
In Progress.

## Paths a change can take

| Situation | Path |
|---|---|
| Planned work | ticket branch → `EM-n: …` → squash into the phase branch |
| Gap in a merged ticket, phase still open | `Fix: EM-n - …` → squash into the phase branch; no new ticket |
| Production broken, cannot wait | `Hotfix: EM-n - …` from `main` into `main`, then merge `main` into the phase |
| Gap in a phase already on `main` | a ticket in the open phase, mirrored as a GitHub issue naming the old phase |

## Checking by hand

Every PR has a **How to check manually** section. Before a phase merges, the maintainer runs the phase
PR's combined plan on staging. CI and Claude's own runs are evidence, not the check.

## Housekeeping

A ticket's worktree is removed once its PR merges; ticket branches are pruned after their phase lands.
Personal notes go to the gitignored `notes/`, never to `docs/`.
