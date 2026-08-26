# Spike: Jobicy remote jobs API (Tier B) — EM-48

**Date:** 2026-08-26. **Assumption:** A-004.

## Request

```
GET https://jobicy.com/api/v2/remote-jobs?count=5
```

No auth. `count` accepts 1–100. `response.json` here is the full, untruncated live response.

## Shape / field mapping sketch

Top-level: `{ apiVersion, documentationUrl, friendlyNotice, jobCount, lastUpdate, appliedFilters,
jobs: [...], statusCode, success }`. Per job: `id` (int — clean stable external id, unlike
Himalayas), `url` (canonical job URL — matches docs/SOURCES.md's "canonical Jobicy job URL"
requirement directly), `jobSlug`, `jobTitle`, `companyName`, `companyLogo`, `jobIndustry[]`,
`jobType[]`, `jobGeo`, `jobLevel`, `jobExcerpt`, `jobDescription` (HTML), `pubDate`.

`id` → `Vacancy.ExternalId`, `url` → `Vacancy.Url` directly satisfies the canonical-URL
requirement with no transformation needed.

**Discovery endpoints confirmed live** (not tested with a full call, but the params are as
documented): `?get=locations`, `?get=industries` are additional filter-discovery calls per
`docs/SOURCES.md`.

## Rate limits

No `X-RateLimit-*` headers on this response. `documentationUrl` (`https://jobi.cy/apidocs`)
returned `HTTP 404` when fetched directly — Jobicy's own docs link is currently dead or requires
a different path; the **binding "≤1 poll/hour" constraint from docs/SOURCES.md could not be
re-confirmed from Jobicy's own docs page today** (it was presumably sourced from that page during
the original `docs`-level desk research, before the page moved or 404'd). Treat the existing
`min_poll_interval` = 1h as still authoritative pending a working docs link, since the API's own
behavior gives no reason to weaken it, but flag the dead link as a small follow-up.

## Terms-of-use verdict — cleared, and better evidence than a ToS page

**The API embeds its own terms in every response**, live quote from `friendlyNotice` in this
exact spike response:

> "Thanks for using Jobicy API! Please ensure Jobicy is clearly credited with a direct link to
> the source, and all application buttons redirect to the original job URL provided in this
> feed. That's all! You might be building something amazing, we wish you the best of luck!"

This is stronger evidence than a scraped terms page: it's Jobicy's own API actively telling every
caller, on every call, exactly what's required — credit + link to Jobicy, and preserve the
original job URL on the "apply" action (which the `url` field already supports directly). This
confirms and slightly sharpens the existing `docs/SOURCES.md` display condition (which said "a
visible link back" — the live wording is more specific: application buttons must redirect to the
*original job URL*, not just link back to jobicy.com generally).

## Verdict

Technical: **spike**, clean 200, real data, canonical URL field makes mapping trivial. Legal:
**cleared for public display**, condition = attribution naming Jobicy + application flow
redirects to the original Jobicy job URL (already the `url` field) — matches EM-54's scope
directly.
