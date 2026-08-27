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

- **Level:** `spike` (2026-08-26, EM-45). `spikes/greenhouse/response.json` + `NOTES.md`
  committed: live `GET /v1/boards/gitlab/jobs?content=true` → `200`, 217 real jobs.
- **Blast radius:** high. One of the two Tier A adapters in the MVP.
- **Legal status: cleared.** Fixed a firewall gap (`www.greenhouse.io`/`.com`,
  `developers.greenhouse.io`, `docs.greenhouse.io` weren't allowlisted) and read the developer
  docs live: they state the API exists so callers "can build careers pages with a unique look
  and feel" — Greenhouse's own words for exactly what this project does. See
  `spikes/greenhouse/NOTES.md` for the quote.
- **Fallback:** Lever (A-002) alone at Tier A, plus Ashby (A-008) pulled forward.
- **Expiry:** 22 Feb 2027.

### A-002 — Lever postings API is public, unauthenticated, and displayable

- **Level:** `spike` (2026-08-26, **re-run 2026-08-27**, EM-46). `spikes/lever/response.json` +
  `NOTES.md` committed. The original sample was `GET /v0/postings/palantir?mode=json` → `200`,
  305 postings — but `palantir` was a guessed token and is excluded from the registry as a
  prestige-filtered employer, so the spike was re-run against EM-50's three live-verified tokens:
  `qonto` → `200`/40, `remofirst` → `200`/1, `peerspace` → `200`/3, for 44 postings across three
  boards.
  (Also confirmed a valid *empty* response — `lever` and `plaid` site tokens returned `200` with
  `[]` — worth handling as a non-error case in the adapter, not retried as a failure. Note the
  spread: one board of 40 and one of 1 in the same registry, so "the board is live" and "the board
  has volume" are separate facts.)
- **Confirmed by the re-run, and load-bearing for any Lever adapter:** the root is a flat JSON
  array on every board tested, and *no* posting carries an employer name — `Vacancy.Company` must
  come from the target-company registry row, not the payload.
- **Blast radius:** high. The other Tier A adapter in the MVP.
- **Legal status: cleared.** Same firewall fix as A-001 (`hire.lever.co` added), then read
  live: Lever's own developer docs name "create a custom job site" as the Postings API's
  intended use. See `spikes/lever/NOTES.md` for the quote.
- **Fallback:** Greenhouse (A-001) alone at Tier A, plus Ashby (A-008) pulled forward.
- **Expiry:** 22 Feb 2027.

### A-003 — Himalayas API is free, keyless, and displayable with attribution

- **Level:** `spike` (2026-08-26, EM-47) for the technical claim. `spikes/himalayas/
  response.json` + `NOTES.md` committed: live `GET /jobs/api` → `200`, cursor-paginated,
  `totalCount` 100,592.
- **Blast radius:** medium. One of two Tier B adapters in the MVP.
- **Legal status: FALSIFIED, not just unverified.** `https://himalayas.app/terms`, read live on
  2026-08-26, explicitly bars scraping/data-mining/redistribution **"without Himalayas' prior
  written approval"** — a real quote, not an inference. Nothing in the API's own OpenAPI spec
  carves the `/jobs/api` endpoint out of that restriction. This directly contradicts what this
  entry previously assumed ("displayable with attribution") — that claim was `docs`-level,
  inferred from the tier's general pattern, and turned out wrong on inspection. **Do not set
  `public_deploy_enabled = true` for this source without Himalayas' written approval.** This is
  the same failure shape as A-000 (hh.ru) at smaller scale, caught before an adapter was written
  instead of after.
- **Resolved specifics:** rate limit still not exposed via headers; refresh cadence is 24h per
  the OpenAPI spec (`min_poll_interval` should be set to 24h, not the CDN's 2h cache window);
  attribution wording is moot until the approval question is resolved.
- **Fallback:** Jobicy (A-004) plus Arbeitnow (A-005) — both cleared. Do not backfill Himalayas
  into the "≥4 cleared for public display" gate count.
- **Expiry:** on written approval, or 22 Feb 2027, whichever is first.

### A-004 — Jobicy API is free, keyless, and displayable at ≤1 poll/hour

- **Level:** `spike` (2026-08-26, EM-48). `spikes/jobicy/response.json` + `NOTES.md` committed:
  live `GET /api/v2/remote-jobs?count=5` → `200`, 5 real jobs, full untruncated response.
- **Blast radius:** medium. The other Tier B adapter in the MVP.
- **Legal status: cleared.** The API embeds its own terms in every response's `friendlyNotice`
  field (live quote in `NOTES.md`): credit Jobicy, and application buttons must redirect to the
  original job URL — which the `url` field already provides directly. Stronger evidence than a
  scraped ToS page, since Jobicy states it on every call.
- **The constraint that bites:** polling faster than hourly risks a ban. It lives in
  `sources.min_poll_interval` and the scheduler reads it from there — never a constant. Jobicy's
  own docs page (`jobi.cy/apidocs`) 404'd when checked live — the 1h figure is carried forward
  from the original desk research, not re-confirmed today; flagged as a small follow-up, not a
  blocker.
- **Fallback:** Himalayas (A-003, currently blocked) plus Arbeitnow (A-005).
- **Expiry:** 22 Feb 2027.

### A-005 — Arbeitnow's job-board API is free, keyless, and displayable

- **Level:** `spike` (2026-08-26, EM-49). `spikes/arbeitnow/response.json` + `NOTES.md`
  committed: live `GET /api/job-board-api` → `200`, 175 real postings on page 1.
- **Blast radius:** low. Phase I follow-on (EM-19), not an MVP dependency.
- **Legal status: cleared.** The "no terms found during desk research" gap is resolved: the
  standalone `/terms` page is client-rendered JS (unfetchable via plain `curl` from this
  environment), but the API response itself carries `meta.terms` (live quote in `NOTES.md`):
  free public API, don't abuse it, link-back appreciated, full site ToS incorporated by
  reference. Pagination (`?page=`) and refresh cadence (hourly, per `meta.info`) are both
  resolved the same way — stated directly in the live response.
- **Fallback:** drop EU/DACH coverage from Phase I; the two Tier B MVP adapters stand alone.
- **Expiry:** 22 Feb 2027.

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

- **Level:** `spike` (2026-08-26) — **partially falsified.** EM-52/53's first live ingest pulled
  1,021 postings from four sources; field coverage of the normalized model measured across them:

  | Source | Rows | company | location | workFormat | salary | publishedAt |
  |---|---:|---:|---:|---:|---:|---:|
  | Greenhouse | 227 | 100% | 100% | **0%** | **0%** | 100% |
  | Lever | 44 | 100% | 100% | 100% | **0%** | 100% |
  | Jobicy | 100 | 100% | 100% | 100% | 77% | 100% |
  | Arbeitnow | 649 | 100% | 96% | **5%** | **0%** | 100% |

  Title, company, URL, location and publication date survive normalization everywhere. The two
  fields Phase III would most want do not: **salary exists on Jobicy alone** (77% of its rows,
  and only where `salaryPeriod` is yearly — an hourly figure in the same column would make it
  meaningless), and **work format is absent from Greenhouse entirely** and near-absent from
  Arbeitnow, whose `remote: false` means "not flagged", not "known to be onsite". Lever carries
  no employer name in its payload at all; the target-company registry supplies it.
- **Blast radius:** high. It is the core of EM-52 and the part of this project most worth
  discussing in an interview.
- **The risk:** confirmed in the shape above, not in the shape feared. The mapping does not
  collapse to title/company/URL — but a fit-score that weights salary or remote-ness would be
  scoring Jobicy and Lever against everyone else, not scoring roles. Phase III must either derive
  these from the description via LLM extraction (EM-31) or exclude them from the score.
- **Fallback:** `raw_postings` stores every fetch before mapping, so a mapping decision can be
  revisited and replayed without re-hitting a rate-limited source. Exercised in this run: 1,021
  raw rows written against 1,020 distinct vacancies (one Arbeitnow slug arrived twice in a single
  page set).
- **Expiry:** re-measure when a fifth source lands (EM-19). Resolved for the MVP's four.

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
