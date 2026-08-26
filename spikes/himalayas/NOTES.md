# Spike: Himalayas remote jobs API (Tier B) — EM-47

**Date:** 2026-08-26. **Assumption:** A-003.

## Request

```
GET https://himalayas.app/jobs/api
```

No auth. `response.json` here is truncated to the first 3 of 20 jobs in this page — see
`_spike_note`.

## Shape / field mapping sketch

Top-level: `{ jobs: [...], offset, limit, totalCount, nextCursor, updatedAt, comments }`.
`limit` defaults to 20 per page. `totalCount` was 100,592 at fetch time — this is Himalayas'
entire jobs feed, not a curated remote-only subset, so the adapter needs Himalayas' own filters
(the OpenAPI spec's `/jobs/api/search` endpoint) to avoid ingesting everything.

Per job: no numeric `id` field — **`guid` is the unique external id** (this is a mapping detail
that would have been wrong if guessed from the field name alone: `id` doesn't exist, easy to
assume it does and use `companySlug` or something unstable instead). Also: `title`, `companyName`,
`companySlug`, `description` (HTML), `employmentType`, `minSalary`/`maxSalary`/`salaryPeriod`
(period-relative as of the 2026-06-08 API change noted in the OpenAPI spec — not normalized to
annual), `locationRestrictions[]`, `timezoneRestrictions[]`, `categories[]`, `pubDate`,
`expiryDate`. No direct "canonical apply URL" field on the job itself — `applicationLink` serves
that role.

Pagination: cursor-based via `nextCursor` (opaque token), not offset/page-number.

## Rate limits

No `X-RateLimit-*` headers. `Cache-Control: public, max-age=0, s-maxage=7200,
stale-while-revalidate=86400` — the CDN caches this for 2h server-side regardless of client
polling frequency, so polling faster than 2h gets you the same cached response. The OpenAPI
spec's own description says "Data is updated every 24 hours." **Recommend `min_poll_interval` =
24h**, driven by the documented refresh cadence, not the shorter CDN cache window.

## Terms-of-use verdict — ⚠️ NOT CLEARED, this is the significant finding of this spike batch

Fetched `https://himalayas.app/terms` directly (reachable — this domain unlike greenhouse.io/
lever.co's docs sites is on the same host as the already-allowlisted API). Live quotes from that
page, 2026-08-26:

> "You may not use data mining, robots, screen scraping, or similar automated data gathering,
> extraction or publication tools on this Site (including without limitation for the purposes of
> establishing, maintaining, advancing or reproducing information contained on our Site on
> another website or in any other publication), **without Himalayas' prior written approval.**"

> "[Do not] use software, devices, scripts, robots or any other means or processes... to scrape
> the Services or otherwise copy profiles and other data from the Services... **Copy, use,
> disclose or distribute any information obtained from the Services, whether directly or through
> third parties (such as search engines), without the consent** of Himalayas..."

This is the general Site Terms of Service, and it is broad enough to plausibly cover the
`/jobs/api` endpoint too — nothing in either the terms page or the OpenAPI spec (`Himalayas
Remote Jobs API 1.0.0`, read separately) carves the public API out from this restriction or
states it may be used to redisplay data on a third-party site. The OpenAPI description promotes
Himalayas' *MCP server* for AI agents ("the recommended way for AI agents to interact with the
remote job market") but says nothing about redisplay rights for the REST API.

**This directly contradicts the current `docs/SOURCES.md` entry**, which states the display
condition as simply "visible link back to himalayas.app plus a source credit" — that claim was
never sourced to an actual terms quote (it was `docs`-level, i.e. inferred from the tier's general
pattern, not verified against Himalayas specifically). It turns out to be wrong, or at least
unconfirmed, on inspection.

**This is exactly the shape of the hh.ru mistake at smaller scale: a permissive claim that was
never checked against the actual terms text.** Recording it now, at `spike` level, before any
adapter is written, is the point of this process.

## Verdict

Technical: **spike**, clean 200, real paginated data, `guid`-not-`id` mapping gotcha documented.
Legal: **not cleared for public display.** Recommend either (a) requesting Himalayas' written
approval before enabling this source in a deployed environment, or (b) treating Himalayas as a
Tier B candidate that stays at `enabled = false` / `public_deploy_enabled = false` until approval
exists — same posture the schema already enforces for Tier D. Do not let EM-53 build an adapter
against this source without one of those two happening first.
