<!--
  Kept deliberately short. Delete any section that does not apply, but do not
  delete the Evidence section — PLAN.md §01 makes it the point of the template.
-->

<!-- Title: `Phase <phase>: EM-<tickets> <summary>` — see docs/CONVENTIONS.md. -->

## What and why

<!-- One paragraph. What changes, and which problem it solves. -->

**Linear:** EM-
**Phase:** <!-- 0 / I / II / III / IV / V — must match the milestone on this PR -->

## Evidence

<!--
  PLAN.md §01.1 and §01.3: a claim about an external service needs a URL, a date
  and a live response; a completed checkbox needs a link to a commit, PR or CI run.
  Fill in what applies:
-->

- Commands run and their result (build / migration / spike):
- CI run:
- For work touching an external source: `spikes/<source>/response.json` committed, live-tested on YYYY-MM-DD, HTTP status:
- Terms-of-use verdict (quote + link), if a source is added, enabled or re-tiered:

## Source and assumption bookkeeping

- [ ] `docs/SOURCES.md` updated (tier, endpoint, rate limit, ToU verdict, verification level) — or N/A
- [ ] `docs/ASSUMPTIONS.md` updated (new/changed assumption, verification level, blast radius, fallback, expiry) — or N/A
- [ ] `PLAN.md` checkboxes updated with links to the evidence above — or N/A
- [ ] No Tier D source is enabled anywhere in this diff
- [ ] New outbound domain? added to the allowlist in `.devcontainer/init-firewall.sh` — or N/A

## Risk and rollback

<!-- What breaks if this is wrong, and how it is reverted. Migrations: is the down path tested? -->
