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
| Level | `docs` — spike is EM-45 |

**Known caveat:** no native search or filtering. The search layer is ours to build.

### Lever

| | |
|---|---|
| Endpoint | `GET https://api.lever.co/v0/postings/{site}?mode=json` |
| Auth | None |
| Adapter type | `lever` |
| Assumption | A-002 |
| Level | `docs` — spike is EM-46 |

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
| Level | `docs` — spike is EM-47 |

**To confirm in the spike:** the actual rate limit (documented only as "rate limited, 429 on
exceed"); that data refreshes every 24 h, so polling faster is pointless — that value goes into
`sources.min_poll_interval`; and the exact required attribution wording.

**Display condition:** visible link back to himalayas.app plus a source credit.

### Jobicy

| | |
|---|---|
| Endpoint | `GET https://jobicy.com/api/v2/remote-jobs` |
| Auth | None |
| Params | `count` (1–100), `geo`, `industry`, `tag` |
| Discovery | `?get=locations`, `?get=industries` return valid filter values |
| Adapter type | `json_api` |
| Assumption | A-004 |
| Level | `docs` — spike is EM-48 |

**Binding constraint: polling must not exceed once per hour.** This lives in
`sources.min_poll_interval` and the scheduler reads it from there, never from a constant. A
scheduler that ignores it gets us banned.

**Display condition:** Jobicy stays named as the original source, and the canonical Jobicy job
URL is preserved on display.

*Noted for later, out of scope:* Jobicy also exposes an MCP server at `jobicy.com/mcp`.

### Arbeitnow (EU/DACH)

| | |
|---|---|
| Endpoint | `GET https://www.arbeitnow.com/api/job-board-api` |
| Auth | None |
| Adapter type | `json_api` |
| Assumption | A-005 |
| Level | `docs` — spike is EM-49 |

**Observed fields:** `slug`, `company_name`, `title`, `description` (HTML), `remote`, `url`,
`tags`, `job_types`, `location`, `created_at`.

**Unknowns the spike must close:** pagination parameters; rate limits and headers; whether a
visa-sponsorship flag exists or must be inferred from `tags`; terms of use — not found during
desk research, and **if none is published, that absence is itself the finding to record**.

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

Disqualified on two independent grounds:

1. **Technical.** Since April 2026, `GET /vacancies`, `GET /vacancies/{id}` and
   `GET /employers/{id}` return `403` to unauthorized callers. Only the reference endpoints
   (`/areas`, `/dictionaries`, `/suggests`) remain openly available. Access is oriented to
   verified employer accounts and moderated applications.
2. **Legal — and this ground survives any technical workaround.** The developer agreement
   restricts use to recruitment purposes and forbids transferring retrieved data to third-party
   services; only the registered user who initiated the retrieval may use it. A publicly deployed
   site rendering hh.ru vacancies breaches that regardless of how the data was obtained. **This
   project's entire premise is a public deployment.** Even a working token would not have made
   Phase I shippable.

Sources: <https://habr.com/ru/news/1069286/> · <https://dev.hh.ru/admin/developer_agreement>

### jobs.ge / headhunter.ge

Dropped from the plan in Revision 2. Was scheduled as a scraped second source in Revision 1; no
terms-of-use verdict was ever written, and Revision 2 does not schedule scraping. Reconsider only
via a spike with a terms verdict, like any other source.

---

## Target-company registry

Tier A has no search — it fetches per company, so the registry *is* the query. Seeded at 30
companies (EM-50): name, ATS, board token, and one line on why it is a target (remote-friendly,
hires juniors, relocation support, stack match).

**Open question, to be answered while building it, not at 200 rows:** does the registry grow by
hand, from a harvestable public list of board tokens, or from applications actually sent? 30 by
hand is fine; 300 is not.

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
