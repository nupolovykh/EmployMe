# Spike: Greenhouse boards API (Tier A) — EM-45

**Date:** 2026-08-26. **Assumption:** A-001.

## Request

```
GET https://boards-api.greenhouse.io/v1/boards/gitlab/jobs?content=true
```

No auth. `content=true` includes the full HTML job description; omit it for a lighter listing
call and fetch full content per-job on demand if that turns out cheaper.

**Result:** `HTTP 200`, 217 jobs, 3,463,400 bytes with `content=true`. `response.json` in this
directory is truncated to the first 3 jobs — see `_spike_note`.

## Shape / field mapping sketch

Top-level: `{ "jobs": [...] }`. Per job: `id` (int, stable per-posting external id), `title`,
`company_name`, `absolute_url` (public job-boards.greenhouse.io URL — this is the canonical URL
a candidate lands on), `location.name`, `content` (HTML-escaped description when
`content=true`), `updated_at`, `first_published`, `requisition_id`, `metadata[]` (company-defined
custom fields, varies per board), `data_compliance[]` (GDPR flags).

`id` → `Vacancy.ExternalId`. No pagination in this response — the whole board comes back in one
call; `raw_postings` stores it, so re-mapping doesn't require re-fetching.

## Rate limits

No `X-RateLimit-*` or `Retry-After` headers observed on this response. Nothing found in desk
research beyond "don't hammer it" style guidance. Treat as unknown and conservative
(`min_poll_interval` ≥ 1h) until a 429 is actually observed or Greenhouse publishes a number.

## Terms-of-use verdict — cleared, after fixing a firewall gap mid-spike

Initially blocked: `www.greenhouse.io` (where Greenhouse's terms live) wasn't in the firewall
allowlist, only `boards-api.greenhouse.io` was. Added `www.greenhouse.io`,
`www.greenhouse.com` (the marketing site redirects there — Greenhouse rebranded from .io to
.com), `developers.greenhouse.io` and `docs.greenhouse.io` (the actual API docs live behind a
further redirect) to `.devcontainer/init-firewall.sh` and re-ran it. A dedicated "Job Board API
Terms of Use" legal page could not be located post-rebrand (checked `/legal`, several guessed
paths, and the sitemap — all 404 or no match), but the **developer documentation states the
API's purpose directly**, live quote from `developers.greenhouse.io/job-board.html`,
2026-08-26:

> "[The Job Board API returns] a simple JSON representation of your company's offices,
> departments, and published jobs. Since we give you access to the raw data, **you can build
> careers pages with a unique look and feel**, construct department-level pages, and more!"

This is Greenhouse's own documentation stating the intended use is exactly what this project
does — build a page displaying the raw job data. Combined with every job's `absolute_url`
already being a public, unauthenticated `job-boards.greenhouse.io` page, this is enough to clear
the source. No explicit attribution requirement was found (unlike Jobicy/Arbeitnow) — nothing
in the docs asks for a credit line or link-back, so none is currently modeled as required in
`docs/SOURCES.md`, though adding one costs nothing and is good practice regardless.

## Verdict

Technical: **spike**, clean 200, real data, field mapping is straightforward. Legal:
**cleared** — Greenhouse's own developer docs state the Job Board API exists to let callers
build their own display of the data.
