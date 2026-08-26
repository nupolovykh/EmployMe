# Source Registry

The authoritative list of job sources this project may ingest from, what each one
permits, and how far each claim has actually been verified.

This file exists because of the hh.ru incident (see [Disqualified sources](#disqualified-sources)
and `ASSUMPTIONS.md` entry A-000). A source is not "available" because its docs say so.
It is available when a committed response body and a written terms-of-use verdict say so.

**Companion file:** [`ASSUMPTIONS.md`](./ASSUMPTIONS.md) tracks the verification level and
expiry date of every claim below.

---

## Verification levels

Every source carries a level. The level, not the tier, decides whether work may be scheduled
against it.

| Level | Meaning |
|---|---|
| `assumed` | Someone believes this works. No evidence. Never sufficient to schedule work. |
| `docs` | The provider's own documentation was read. Nobody has called the endpoint. |
| `spike` | A real request was made and `spikes/<source>/response.json` is committed, alongside a `NOTES.md` carrying the terms-of-use verdict. |
| `live` | An adapter runs against it in a deployed environment and the nightly contract test covers it. |

**Phase gate:** Phase I may not start with fewer than four sources at level `spike`, at least
two of them Tier A, at least four cleared for public display.

**Status as of 2026-08-26 (EM-45–49 spikes run): gate met.** 5/5 sources reached `spike`
(technical) — Greenhouse, Lever, Himalayas, Jobicy, Arbeitnow, 2 of them Tier A. 4/5 are cleared
for public display: **Greenhouse, Lever, Jobicy, Arbeitnow.** Himalayas is the one exception —
**not cleared**, its terms explicitly require prior written approval (found live during the
spike) — and stays excluded from both the adapter set and the "cleared" count until that
approval exists. All three gate sub-conditions (≥4 spike, ≥2 Tier A, ≥4 cleared for public
display) are satisfied without Himalayas.

---

## Tiers

Tiers describe *what kind of thing* a source is, and therefore what the legal and
operational risk profile looks like. They do not describe how trustworthy the integration is —
that is the verification level.

| Tier | What it is | Risk profile |
|---|---|---|
| **A** | An employer's own ATS board (Greenhouse, Lever, Ashby, Workable, Recruitee, Personio). Public, unauthenticated, published by the hiring company itself. | Lowest. The employer publishes it to be read. No search layer, so we supply the query from the target-company registry. |
| **B** | A public remote-job API run by a job board (Himalayas, Jobicy, Remotive, RemoteOK, Arbeitnow). Free, no key. | Low, *conditional on attribution*. Display is permitted only while credit and a link back are rendered. |
| **C** | Requires registration, an API key, or partner approval before use. | Medium. Usable, but access is revocable by a third party and gated on an account. Out of scope for the MVP. |
| **D** | Restricted or disqualified: the terms forbid redisplay on a public deployment, or access has been closed. | Unacceptable for this project's premise. **`public_deploy_enabled` is false. A Tier D connector is never enabled in a deployed environment.** |

**Blast-radius rule (N≥3):** no phase may depend on a single external source. The MVP ships with
at least four connectors across at least two tiers.

---

## Tier A — Employer ATS boards

Fetched per company, not searched. The [target-company registry](#target-company-registry) is
the query.

### Greenhouse

| | |
|---|---|
| Endpoint | `GET https://boards-api.greenhouse.io/v1/boards/{board_token}/jobs?content=true` |
| Auth | None |
| Adapter type | `greenhouse` |
| Assumption | A-001 |
| Level | `spike` (2026-08-26) — `spikes/greenhouse/`. **Legal: cleared** |

**Known caveat:** no native search or filtering. The search layer is ours to build.

**Display condition:** none found — Greenhouse's developer docs describe the API as existing so
callers "can build careers pages with a unique look and feel," with no attribution requirement
stated. See `spikes/greenhouse/NOTES.md` for the live quote.

### Lever

| | |
|---|---|
| Endpoint | `GET https://api.lever.co/v0/postings/{site}?mode=json` |
| Auth | None |
| Adapter type | `lever` |
| Assumption | A-002 |
| Level | `spike` (2026-08-26) — `spikes/lever/`. **Legal: cleared** |

**Display condition:** none found — Lever's developer docs name "create a custom job site" as
the Postings API's intended use, with no attribution requirement stated. See
`spikes/lever/NOTES.md` for the live quote.

### Ashby

| | |
|---|---|
| Adapter type | `ashby` |
| Assumption | A-008 |
| Level | `assumed` — scheduled as a Phase I follow-on (EM-19) |

### Workable · Recruitee · Personio

Candidate Tier A adapters, no assumption entry opened yet. Not scheduled. They enter the
register when a company in the target registry actually uses one.

---

## Tier B — Public remote-job APIs

Searchable, but each one imposes conditions. **Every condition below is a display condition, not
a nicety** — see EM-54.

### Himalayas

| | |
|---|---|
| Endpoints | `GET https://himalayas.app/jobs/api`, `GET https://himalayas.app/jobs/api/search` |
| Auth | None |
| OpenAPI | `https://himalayas.app/docs/openapi.json` (3.1) — generate the client, don't hand-roll DTOs |
| Adapter type | `json_api` |
| Assumption | A-003 |
| Level | `spike` (2026-08-26) — `spikes/himalayas/`. **⚠️ Legal: FALSIFIED, not cleared** |

**Resolved by the spike:** rate limit still not exposed via headers (429 behavior unconfirmed);
refresh cadence is 24h per the OpenAPI spec, so `min_poll_interval` = 24h.

**Display condition — do not trust the line below, kept for history.** ~~visible link back to
himalayas.app plus a source credit~~ — **wrong.** `himalayas.app/terms`, read live 2026-08-26,
explicitly bars scraping/redistribution "without Himalayas' prior written approval." See
`spikes/himalayas/NOTES.md` for the quotes. **`public_deploy_enabled` must stay `false` for this
source until written approval exists.** Do not build EM-53's Himalayas adapter against this
source before that happens.

### Jobicy

| | |
|---|---|
| Endpoint | `GET https://jobicy.com/api/v2/remote-jobs` |
| Auth | None |
| Params | `count` (1–100), `geo`, `industry`, `tag` |
| Discovery | `?get=locations`, `?get=industries` return valid filter values |
| Adapter type | `json_api` |
| Assumption | A-004 |
| Level | `spike` (2026-08-26) — `spikes/jobicy/`. **Legal: cleared** |

**Binding constraint: polling must not exceed once per hour.** This lives in
`sources.min_poll_interval` and the scheduler reads it from there, never from a constant. A
scheduler that ignores it gets us banned. (Jobicy's own docs page 404'd when re-checked
2026-08-26 — the 1h figure carries forward from the original desk research, not re-confirmed
today.)

**Display condition, confirmed live from the API's own `friendlyNotice` field (stronger than a
scraped terms page):** Jobicy stays named as the original source, and application buttons must
redirect to the original Jobicy job URL — the `url` field already provides this directly. See
`spikes/jobicy/NOTES.md`.

*Noted for later, out of scope:* Jobicy also exposes an MCP server at `jobicy.com/mcp`.

### Arbeitnow (EU/DACH)

| | |
|---|---|
| Endpoint | `GET https://www.arbeitnow.com/api/job-board-api` |
| Auth | None |
| Adapter type | `json_api` |
| Assumption | A-005 |
| Level | `spike` (2026-08-26) — `spikes/arbeitnow/`. **Legal: cleared** |

**Observed fields:** `slug`, `company_name`, `title`, `description` (HTML), `remote`, `url`,
`tags`, `job_types`, `location`, `created_at`. **`slug` is the external id — there is no separate
numeric `id` field**, same shape gotcha as Himalayas' `guid`.

**Resolved by the spike:** pagination is `?page=` (Laravel-style `links`/`meta`, confirmed via a
live `links.next`); refresh cadence is hourly per `meta.info`, so `min_poll_interval` = 1h.
Visa-sponsorship flag still unconfirmed — inferred from `tags` for now. **Terms of use: the
`/terms` page itself is client-rendered JS and couldn't be read via `curl`, but the API response
carries its own `meta.terms` field** — "free public API... please do not abuse... agree to the
terms of service present on Arbeitnow.com" — treated as the operative statement. See
`spikes/arbeitnow/NOTES.md`.

Descriptions are largely German. This drives the multilingual embedding model choice in Phase III.

### Remotive

| | |
|---|---|
| Assumption | A-006 |
| Level | `assumed` — no spike scheduled yet |

**Display condition:** credit and link back required; **Remotive terminates access for
non-compliance.** Do not enable this adapter before attribution rendering (EM-54) ships.

### RemoteOK

| | |
|---|---|
| Assumption | A-007 |
| Level | `assumed` — no spike scheduled yet |

**Display condition:** credit and link back required.

---

## Disqualified sources

### hh.ru — Tier D, permanently

**Do not reopen this without new evidence dated later than the entries below.** The full
post-mortem is Linear EM-9; `ASSUMPTIONS.md` A-000 records the falsification.

Disqualified on three independent grounds:

1. **Technical: DDoS-Guard blocks this environment.** The live endpoint `api.hh.ru` returns `403` 
   when accessed from this project's egress IP (Dev Container and Railway). Confirmed on 2026-08-25 
   against `GET https://api.hh.ru/vacancies` — DDoS-Guard rate-limit response. This alone blocks 
   development; mitigation via proxy/VPN violates the terms below.

2. **Technical: Public API sealed.** Since April 2026, `GET /vacancies`, `GET /vacancies/{id}` and
   `GET /employers/{id}` return `403` to unauthorized callers. Only the reference endpoints
   (`/areas`, `/dictionaries`, `/suggests`) remain openly available. Access is oriented to
   verified employer accounts and moderated applications.

3. **Legal — and this ground survives any technical workaround.** The developer agreement
   restricts use to recruitment purposes and forbids transferring retrieved data to third-party
   services; only the registered user who initiated the retrieval may use it. A publicly deployed
   site rendering hh.ru vacancies breaches that regardless of how the data was obtained. **This
   project's entire premise is a public deployment.** Even a working token would not have made
   Phase I shippable.

Sources: <https://habr.com/ru/news/1069286/> · <https://dev.hh.ru/admin/developer_agreement> · DDoS-Guard block confirmed 2026-08-25

### jobs.ge / headhunter.ge

Dropped from the plan in Revision 2. Was scheduled as a scraped second source in Revision 1; no
terms-of-use verdict was ever written, and Revision 2 does not schedule scraping. Reconsider only
via a spike with a terms verdict, like any other source.

---

## Target-company registry

Tier A has no search — it fetches per company, so the registry *is* the query. `TargetCompany`
rows FK to the adapter-level `Sources` row for their ATS (`greenhouse`/`lever`/...); the ATS
itself isn't duplicated per company.

**Selection criteria, derived from the author's own CV (EM-50):** junior level, .NET/C# primary
stack (polyglot backend a secondary fit), working-proficiency English. **The hard structural
filter is hiring geography:** the author is based in Tbilisi, Georgia — outside the EU/EEA — so
a company only qualifies if it hires through (1) global remote/contractor/EOR with no country
restriction, (2) a remote region that explicitly includes Georgia, or (3) relocation with visa
sponsorship. "Remote (EU only)" is a rejection, not a match, however good the stack fit. Rejected
companies stay recorded with a reason rather than being silently dropped, so they aren't
re-evaluated later.

**Growth mechanism — resolved 2026-08-26, hybrid (revises the original "30 up front" scope):**
collecting 30 companies before any spike ran was backwards — EM-45/46's spikes had no registry to
draw from yet and ended up validated against whatever token answered (GitLab, Palantir), not
against a real target. The model instead is a **small live-verified seed now, organic growth
after**: enough real companies to unblock the Tier A spikes with genuine evidence (3 per ATS, not
30), then every further entry comes from an application actually being sent — a company is never
collected speculatively.

**Seed batch — verified live 2026-08-26** (`SeedGreenhouseLeverTargetCompanies` migration):

| Company | ATS | Token | Jobs seen | Status | Why |
|---|---|---|---|---|---|
| Remote (remote.com) | Greenhouse | `remotecom` | 100 | watch | Global EOR/payroll platform, hires backend; none open in this snapshot |
| Remote People | Greenhouse | `remotepeople` | 17 | active | Global remote-hiring platform; open Back-End Engineer (Python) + junior-titled roles elsewhere |
| Xapo Bank | Greenhouse | `xapo61` | 12 | watch | Crypto/digital bank, explicit "Remote - Work from Anywhere"; hiring model is Georgia-compatible |
| Qonto | Lever | `qonto` | 5 | watch | French fintech, EU-remote; current postings are non-eng |
| RemoFirst | Lever | `remofirst` | 1 | watch | Global remote-first HR platform; thin volume right now |
| Peerspace | Lever | `peerspace` | 3 | watch | US marketplace startup, remote-friendly; current opening is senior-only |

`status: watch` means a live, verified board with nothing currently relevant — kept rather than
discarded, since the point of a registry is to notice when that changes. Tier B/C sources
(Jobicy, Arbeitnow) don't use this registry at all: Jobicy filters server-side, Arbeitnow returns
its whole inventory across a handful of `?page=` calls.

---

## How a source is added

1. Open an assumption entry in `ASSUMPTIONS.md` at level `assumed` or `docs`.
2. Run a timeboxed spike (2 hours). Commit `spikes/<source>/response.json` and
   `spikes/<source>/NOTES.md` — endpoint, auth, observed rate limits, pagination, field-mapping
   sketch, and a **terms-of-use verdict with a quote and a link** answering: may this data be
   shown on a public deployment?
3. Add one test that hits the live endpoint and asserts the response shape.
4. Raise the assumption to level `spike` and add the source here.
5. Insert the `sources` row — including `min_poll_interval`, `attribution_html` and
   `public_deploy_enabled`. Write the adapter (EM-52's `IJobSource`). Adding a source is a class
   plus a row; the pipeline does not change.
6. Add it to the nightly contract test (EM-55).
