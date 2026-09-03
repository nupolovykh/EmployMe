# Ashby posting API — spike (EM-57)

**Captured 2026-08-30.** `GET https://api.ashbyhq.com/posting-api/job-board/{name}?includeCompensation=true`
— no key, no header, no auth of any kind. Three boards, all `200`, 78 postings.

| Board token | HTTP | Postings |
|---|---|---|
| `Ashby` | 200 | 67 |
| `Nango` | 200 | 7 |
| `zencastr` | 200 | 4 |

## Terms of use — verdict: **not cleared, pending a decision**

This is deliberately not marked `cleared`, and the reason is a difference in wording rather than
a technical failure. Himalayas (A-003) passed every technical check and still failed here.

Ashby's [customer Terms of Service](https://www.ashbyhq.com/terms) do not mention the posting API
at all — read live 2026-08-30. The only statement of purpose is in the
[public job posting API documentation](https://developers.ashbyhq.com/docs/public-job-posting-api):

> "This API allows you to get data for all currently published Job Postings **for your
> organization**. **If you host your own careers page**, you can use this data to populate it."

Nothing prohibits third-party use, and there is no attribution requirement, no rate limit and no
restriction on who may call it stated anywhere. But note who that sentence addresses. Greenhouse
was cleared (A-001) on wording about what *callers* may build — "build careers pages with a unique
look and feel" — which reads as a grant to whoever is calling. Ashby's is scoped to *your*
organization and *your own* careers page: it speaks to the Ashby customer publishing their own
jobs, not to a third party aggregating other companies' boards.

That is an absence of permission, not a denial of it. Under §01 rule 1 the honest level is
"documented but not granted", so the verdict is left open rather than assumed favourable.

## Shape

Root is `{ "jobs": [...], "apiVersion": "..." }`.

| Field | Notes for the adapter (EM-19) |
|---|---|
| `id` | UUID string, stable — the external id |
| `title` | |
| `descriptionPlain` | **already plain text.** No HTML stripping, no double-unescaping like Greenhouse |
| `descriptionHtml` | markup version, not needed |
| `jobUrl` / `applyUrl` | canonical posting URL |
| `location` | free text, e.g. `"USA"`, `"Remote - European"`, `"San Francisco Office"` |
| `secondaryLocations[].location` | additional regions — where hiring geography actually lives |
| `isRemote`, `workplaceType` | `workplaceType` is `"Remote"`/`"Onsite"`/`"Hybrid"` — cleaner than Arbeitnow's ambiguous `remote: false` |
| `publishedAt` | ISO 8601 with offset |
| `isListed` | whether the posting is publicly listed |
| `compensation` | see below |
| `department`, `team`, `employmentType` | |

The board name is **not** in the payload, same as Lever — `Company` must come from the registry row.

**Board tokens are case-sensitive.** `Ashby` → `200`; `ashbyhq` → `404`. Greenhouse and Lever
tokens are lowercase and forgiving; Ashby's are not normalised, so the registry must store the
exact casing from the board URL.

## Compensation — the reason this spike exists

A-009 measured salary coverage across 1,021 live postings at **0% on Greenhouse and 0% on Lever**.
Jobicy (Tier B) was the only source supplying it at all.

Measured here, across all 78 postings:

- **74 (94%)** carry a non-empty `compensation.compensationTierSummary`
- **73 (93%)** carry a structured annual salary with `minValue`, `maxValue` and `currencyCode`

Structure:

```
compensation.compensationTiers[].components[]
  compensationType: "Salary" | "EquityPercentage" | ...
  interval:         "1 YEAR" | "1 HOUR" | "NONE"
  currencyCode:     "USD" | "EUR" | "CAD" | "GBP" | ...
  minValue, maxValue
```

Two consequences for EM-19:

1. Filter to `compensationType == "Salary"` **and** `interval == "1 YEAR"`. One `1 HOUR` component
   was observed, and the vacancy model stores a single range with no period — folding an hourly
   figure into it makes the column meaningless. This is exactly the guard JobicyJobSource applies
   to `salaryPeriod`.
2. Ten currencies appear (USD, EUR, CAD, GBP, AUD, SGD, JPY, KRW, PHP, NZD). `Currency` is already
   a per-vacancy column, so nothing needs converting — but nothing may assume USD either.

## Hiring geography — the open problem

None of the three boards sampled passes the target-company registry's hard filter (EM-50: the
author is in Tbilisi, so a company qualifies only on global remote with no country restriction, a
remote region that explicitly includes Georgia, or relocation with a visa).

| Board | Geography as stated | Against EM-50 |
|---|---|---|
| `Ashby` | every role region-locked: `Remote - European` (Spain, Italy, Germany, Switzerland, Denmark, Norway), `Remote - US`, `United Kingdom`, `Remote - Canada` | rejected — EU-only is a rejection by rule |
| `Nango` | "Remote — North America, LATAM or Europe"; `secondaryLocations` lists `Europe` explicitly | ambiguous — whether "Europe" includes Georgia is undecided; also every engineering role is Staff level, against the junior criterion |
| `zencastr` | roles anchored to San Francisco / New York offices, one "EU & US" | rejected — same rule as Ashby's EU roles |

The API is healthy and the compensation data is the best of any source in scope. What has not been
shown is that any company reachable through it is a company this project would apply to. That
question belongs to the registry (EM-50), not to the source, but it decides whether EM-19 is worth
building.
