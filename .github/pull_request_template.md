<!--
  Title (checked by CI, docs/CONVENTIONS.md):
    EM-<n>: …   ·   Fix: EM-<n> - …   ·   Hotfix: EM-<n> - …   ·   Phase <N>: … (no ticket ids)
  Labels: one type/*, one or more area/*. Milestone: the phase.
  Delete a section only when it truly does not apply; never delete "How to check manually".
-->

## What and why

<!-- One paragraph: what changes and which problem it solves. -->

**Linear:** EM-  ·  **Decided:** <!-- link to the ticket's Decided section, if the ticket has one -->

## How to check manually

<!-- Steps the maintainer runs or clicks, and what each should show. For a phase PR: the combined
     plan, run on staging (docs/DEPLOYMENT.md). -->

1.

## Evidence

<!-- docs/PROCESS.md rules 1 and 3: what was actually run and what it showed. -->

- Commands run and their result:
- CI run:
- External source touched: `spikes/<source>/response.json` committed, live-tested on YYYY-MM-DD, HTTP status:
- Terms-of-use verdict (quote + link), if a source is added, enabled or re-tiered:

## Bookkeeping

- [ ] `docs/PLAN.md` item ticked with this PR's number (ticket PRs) — or N/A
- [ ] `docs/SOURCES.md` / `docs/ASSUMPTIONS.md` updated — or N/A
- [ ] `docs/DATA-MODEL.md` updated with the migration — or N/A
- [ ] No Tier D source enabled anywhere in this diff
- [ ] New outbound domain added to `.devcontainer/init-firewall.sh` — or N/A

## Risk and rollback

<!-- What breaks if this is wrong, and how it is reverted. Migrations: is the down path tested? -->
