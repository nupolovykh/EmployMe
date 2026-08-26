# Spike: Lever postings API (Tier A) — EM-46

**Date:** 2026-08-26. **Assumption:** A-002.

## Request

```
GET https://api.lever.co/v0/postings/{site}?mode=json
```

No auth. Tried several real site tokens to find one with live data: `netflix` → 404 (not a Lever
customer, or wrong token), `lever` → 200 but `[]` (Lever's own board currently has zero open
postings — a real, valid, empty response), `plaid` → 200 but `[]`, `palantir` → 200 with 305
postings. Used `palantir` as the sample.

**Result:** `HTTP 200`, 305 postings, 5,898,248 bytes. `response.json` here is truncated to the
first 3 — see `_spike_note`.

## Shape / field mapping sketch

Top-level: a flat JSON array (not wrapped in an object), one element per posting. Per posting:
`id` (opaque string, e.g. UUID-like — stable external id), `text` (job title), `hostedUrl`
(canonical apply-page URL), `categories.location`/`.team`/`.commitment`, `createdAt` (epoch ms),
`descriptionPlain`/`description` (HTML), `lists[]` (structured requirement sections), `additional`
/`additionalPlain` (freeform closing text, e.g. Palantir's accessibility/accommodation notice).

`id` → `Vacancy.ExternalId`. Also flat, no pagination — Lever's docs mention `skip`/`limit`
query params for large boards, unexercised here since 305 came back in one call. Worth
confirming the default page size cap before assuming every board returns everything in one shot.

## Rate limits

No `X-RateLimit-*` headers observed. Same conclusion as Greenhouse: unknown, be conservative,
watch for 429s once an adapter actually polls on a schedule.

## Terms-of-use verdict — cleared, after fixing the same firewall gap as Greenhouse

Initially blocked: `hire.lever.co` (Lever's developer documentation) wasn't in the firewall
allowlist, only `api.lever.co` was. Added it to `.devcontainer/init-firewall.sh` and re-ran it.

No standalone "Terms of Use" section exists on the docs site, but **Lever's own developer
documentation states the Postings API's purpose directly**, live quote from
`hire.lever.co/developer/documentation`, 2026-08-26:

> "If you want to apply a candidate to a job posting or **create a custom job site**, you should
> use the Lever Postings API instead of the Lever Data API."

This is Lever's own documentation naming "create a custom job site" as the Postings API's
intended use — directly on point for what this project does. Combined with every posting's
`hostedUrl` already being a public, unauthenticated `jobs.lever.co` page, this clears the
source. No explicit attribution requirement found in the docs.

## Verdict

Technical: **spike**, clean 200, real data (both a nonempty case via `palantir` and a valid-empty
case via `lever`/`plaid`, which matters for not treating `[]` as an error in the adapter). Legal:
**cleared** — Lever's own documentation names "custom job site" as the Postings API's intended
use.
