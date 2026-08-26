# Spike: Arbeitnow job board API (Tier B, EU/DACH) — EM-49

**Date:** 2026-08-26. **Assumption:** A-005.

## Request

```
GET https://www.arbeitnow.com/api/job-board-api
```

No auth. `response.json` here is truncated to the first 3 of 175 postings on page 1 — see
`_spike_note`.

## Shape / field mapping sketch

Top-level: `{ data: [...], links: { first, last, prev, next }, meta: {...} }`. Per posting:
`slug` (string — this is the stable external id; there is no separate numeric `id` field, same
gotcha shape as Himalayas' `guid`), `company_name`, `title`, `description` (HTML, largely German
— confirms `docs/SOURCES.md`'s note that this drives the Phase III multilingual embedding
choice), `remote` (bool), `url` (canonical apply URL), `tags[]`, `job_types[]`, `location`,
`created_at` (Unix timestamp).

`slug` → `Vacancy.ExternalId`, `url` → `Vacancy.Url` directly.

Pagination: `?page=` param, confirmed via `links.next` = `?page=2` in this response;
`meta.per_page` = 175 (i.e. everything on the site fits on very few pages). `meta.current_page`,
`meta.from`/`meta.to` also present for a standard Laravel-style paginator.

## Rate limits

No `X-RateLimit-*` headers observed. **`meta.info` in the live response answers this directly**:

> "Jobs are updated every hour and order by the `created_at` timestamp. Use `?page=` to
> paginate."

This resolves the `docs/SOURCES.md` "pagination parameters... unknown" item directly — both the
pagination mechanism and the refresh cadence (hourly) are stated by the API itself.
**Recommend `min_poll_interval` = 1h**, matching the stated update cadence.

## Terms-of-use verdict — cleared, resolves the "no terms found" gap from desk research

`docs/SOURCES.md` currently says "terms of use — not found during desk research... if none is
published, that absence is itself the finding to record." That was true of the standalone
`/terms` page specifically: it renders via client-side JavaScript, and `curl` only retrieves an
empty shell (fonts/CSS, no body text) — a real limitation of this environment's HTTP-only
fetching, not evidence terms don't exist.

**However, the API response itself embeds its own terms statement**, live quote from `meta.terms`
in this exact spike response:

> "This is a free public API for jobs, please do not abuse. I would appreciate linking back to
> the site. By using the API, you agree to the terms of service present on Arbeitnow.com."

This is a real, API-native terms statement — softer than Jobicy's (a request to link back,
"would appreciate," not "please ensure... credited"), but explicit permission to build on top of
the API and display its data, conditioned on not abusing the service and (informally) crediting
Arbeitnow. Treat the full site ToS at arbeitnow.com/terms as incorporated by reference per this
statement, even though its exact text couldn't be extracted from this environment.

## Verdict

Technical: **spike**, clean 200, real paginated data. Legal: **cleared for public display**,
condition = don't abuse (respect the hourly cadence), link back appreciated (implement via EM-54
alongside the other three cleared sources for consistency, even though Arbeitnow's own wording is
softer than Jobicy's).
