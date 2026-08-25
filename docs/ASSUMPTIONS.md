# Assumption Register

Every load-bearing claim this project rests on, with how far it has been verified, what breaks if
it turns out false, what we do instead, and when the claim goes stale.

**Why this file exists.** Revision 1 was planned end to end around one source whose availability
nobody had tested. When it turned out to be unavailable — on two independent grounds — three
weeks of planned work went with it. An assumption that is written down with an expiry date can
fail loudly. One that lives in someone's head fails silently, weeks later, by accident.

**Companion file:** [`SOURCES.md`](./SOURCES.md) holds the endpoints, terms and tiers.

---

## Verification levels

| Level | Meaning |
|---|---|
| `assumed` | Believed. No evidence. Never sufficient to schedule work against. |
| `docs` | The provider's documentation was read. Nobody has called the endpoint. |
| `spike` | A real request was made; `spikes/<source>/response.json` and a terms verdict are committed. |
| `live` | An adapter runs against it in a deployed environment, covered by the nightly contract test. |
| `falsified` | Tested and found false. Kept, not deleted. |

**Rule:** an integration issue may not leave Backlog without a link to its spike artifact — a
committed live response, not a claim.

**On expiry.** An expired entry is not automatically false; it is unverified again. Re-run the
spike or drop the level back to `docs`. From Phase II the nightly contract test (EM-55) does this
continuously for `live` sources, and expiry dates matter mainly for the ones it does not cover.

---

## A-000 — hh.ru's public API is usable for a public aggregator

- **Level:** `falsified` (22 Aug 2026)
- **Blast radius:** total. Phase I was planned entirely around it; EM-13 and EM-14 were built
  against it and cancelled.
- **What actually happened:** the claim was marked done without anyone having seen a response
  body. Falsified on two independent grounds — `403` to unauthorized callers since April 2026,
  *and* a developer agreement that forbids transferring retrieved data to third-party services.
  The legal ground survives any technical workaround.
- **Fallback taken:** the source tiering in `SOURCES.md` — employer ATS boards (Tier A) and public
  remote-job APIs (Tier B), four connectors minimum, no single point of failure.
- **Expiry:** none. This entry is permanent. Do not reopen without evidence dated later than
  April 2026 that addresses *both* grounds.
- **Post-mortem:** Linear EM-9.

---

## Source assumptions

### A-001 — Greenhouse boards API is public, unauthenticated, and displayable

- **Level:** `docs` → `spike` via EM-45
- **Blast radius:** high. One of the two Tier A adapters in the MVP.
- **Fallback:** Lever (A-002) alone at Tier A, plus Ashby (A-008) pulled forward.
- **Expiry:** 22 Feb 2027

### A-002 — Lever postings API is public, unauthenticated, and displayable

- **Level:** `docs` → `spike` via EM-46
- **Blast radius:** high. The other Tier A adapter in the MVP.
- **Fallback:** Greenhouse (A-001) alone at Tier A, plus Ashby (A-008) pulled forward.
- **Expiry:** 22 Feb 2027

### A-003 — Himalayas API is free, keyless, and displayable with attribution

- **Level:** `docs` → `spike` via EM-47
- **Blast radius:** medium. One of two Tier B adapters in the MVP.
- **Unverified specifics:** the actual rate limit; the 24 h refresh interval that sets
  `min_poll_interval`; the exact required attribution wording.
- **Fallback:** Jobicy (A-004) plus Arbeitnow (A-005).
- **Expiry:** 22 Feb 2027

### A-004 — Jobicy API is free, keyless, and displayable at ≤1 poll/hour

- **Level:** `docs` → `spike` via EM-48
- **Blast radius:** medium. The other Tier B adapter in the MVP.
- **The constraint that bites:** polling faster than hourly risks a ban. It lives in
  `sources.min_poll_interval` and the scheduler reads it from there — never a constant.
  Attribution must name Jobicy and preserve the canonical Jobicy job URL.
- **Fallback:** Himalayas (A-003) plus Arbeitnow (A-005).
- **Expiry:** 22 Feb 2027

### A-005 — Arbeitnow's job-board API is free, keyless, and displayable

- **Level:** `docs` → `spike` via EM-49
- **Blast radius:** low. Phase I follow-on (EM-19), not an MVP dependency.
- **Weakest point:** no terms of use were found during desk research. If none is published, that
  absence is the finding, and it must be recorded rather than read as permission. Pagination and
  rate limits are also unknown.
- **Fallback:** drop EU/DACH coverage from Phase I; the two Tier B MVP adapters stand alone.
- **Expiry:** 22 Feb 2027

### A-006 — Remotive permits redisplay with credit and a link back

- **Level:** `assumed` — no spike scheduled
- **Blast radius:** low today; would be high if enabled before attribution rendering ships.
- **The risk:** Remotive terminates access for non-compliance. **Do not enable this adapter
  before EM-54 (per-card attribution) is in place.**
- **Fallback:** omit the source. Nothing depends on it.
- **Expiry:** on scheduling — this entry must reach `spike` before any Remotive work starts.

### A-007 — RemoteOK permits redisplay with credit and a link back

- **Level:** `assumed` — no spike scheduled
- **Blast radius:** low. Nothing depends on it.
- **Fallback:** omit the source.
- **Expiry:** on scheduling.

### A-008 — Ashby exposes a public per-company board API

- **Level:** `assumed` — Phase I follow-on (EM-19)
- **Blast radius:** low. A third Tier A adapter, not an MVP dependency, but the designated
  fallback if A-001 or A-002 fails.
- **Fallback:** Workable, Recruitee or Personio, whichever the target-company registry actually
  needs.
- **Expiry:** on scheduling.

---

## Architectural assumptions

### A-009 — Six upstream shapes normalize into one vacancy model without losing what matters

- **Level:** `assumed`
- **Blast radius:** high. It is the core of EM-52 and the part of this project most worth
  discussing in an interview.
- **The risk:** a lowest-common-denominator mapping that reduces every source to title, company
  and URL, discarding exactly the structured signal Phase III's fit-score needs.
- **Fallback:** `raw_postings` stores every fetch before mapping, so a mapping decision can be
  revisited and replayed without re-hitting a rate-limited source.
- **Expiry:** resolved by EM-52, not by a date.

### A-010 — A self-hosted multilingual embedding model is good enough for the fit-score

- **Level:** `assumed`
- **Blast radius:** medium. Phase III only; Phases I–II do not depend on it.
- **The risk:** bge-m3 / e5 quality across English, German and Russian postings is unmeasured.
  Arbeitnow's German descriptions make this a real question, not a theoretical one.
- **Fallback:** a hosted embedding API. Costs money and adds an external dependency, which is why
  it is not the default.
- **Verification:** EM-32 — 50 hand-labeled vacancies and a measured precision figure. Until that
  number exists, no claim about matching quality goes in the README.
- **Expiry:** on Phase III start.
