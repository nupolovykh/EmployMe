# Frontend

`src/Web` — React 19, TypeScript 7, Vite 8, `oxlint`. One page: the vacancy list with filters and
pagination. No router, no state library, no component library.

---

## Reading order

Dependencies point upward, so read bottom-up:

| # | File | What it holds |
|---|---|---|
| 1 | `src/types.ts` | The contract with the API: `Vacancy` (mirrors `VacancyDto`), `VacancyFilters`, `Paged<T>` (mirrors `PagedResult`), `Seniority` |
| 2 | `src/api.ts` | The only network call, `fetchVacancies(filters)`: builds the query string, prefixes `VITE_API_URL` (empty locally), throws on a non-2xx |
| 3 | `src/App.tsx` | Everything else, in one component (≈200 lines) — below |
| 4 | `src/main.tsx` | Mounts `<App/>` into `#root` |
| 5 | `src/App.css`, `src/index.css` | Styles. `index.css` is still the Vite starter's theme tokens |
| 6 | `index.html` | Vite's entry; loads `/src/main.tsx` as an ES module |

---

## `App.tsx`

**`formatSalary(v)`** — `null` when neither bound exists (the block is not rendered),
`"120,000–180,000 USD"` when both do, `"from …"` / `"up to …"` when one does.

**State** — six `useState` hooks. The one that matters is the split between:

- `pendingFilters` — what the form inputs currently hold (controlled inputs);
- `filters` — what was last submitted and is actually being queried.

Typing changes only `pendingFilters`, so there is no request per keystroke. `handleSubmit` copies
`pendingFilters` into `filters` and resets `page` to 1 — page 3 of the old result almost never
exists in the new one. `page`, `result`, `loading`, `error` are the usual async-load set.

**Loading** — one `useEffect` on `[filters, page]`. It sets a local `cancelled` flag in its cleanup,
and every `.then/.catch/.finally` checks it, so a slow response for an old page cannot overwrite a
newer one. The request itself is not aborted, only its result ignored.

**Derived values** — `filtered` (any filter non-empty) separates "the database is empty" (suggest
running an ingest) from "nothing matched" — they used to show the same hint. `lastPage` guards
against division by zero.

**Form** — keyword, location, level, published after, published before. "Level not stated"
(`Unknown`) is offered explicitly rather than hidden under "any level": Greenhouse and Lever state no
level, so hiding it would hide most of the database. The "published before" input was missing once
although the API and types supported it.

**Card** — title linking to the original posting; company, location, work format, level (unless
`Unknown`) and salary; and the attribution line. **The attribution line is a compliance
requirement, not decoration** (EM-54): when `attributionRequired && sourceUrl`, the source name must
link to the source — Tier B terms make the credit a condition of display.

**Pagination** — shown only when `lastPage > 1`; Previous/Next disabled at the ends.

---

## What each command uses

```
package.json
 ├─ npm run dev      → vite.config.ts
 ├─ npm run build    → tsconfig.json → { tsconfig.app.json, tsconfig.node.json } → vite.config.ts
 ├─ npm run lint     → .oxlintrc.json
 └─ npm run preview  → dist/

Dockerfile → npm ci (package-lock.json) → npm run build → serve -s dist
.dockerignore → filters the build context before COPY . .
.gitignore    → filters what reaches git
```

### `npm run dev`

Vite dev server on `http://localhost:5173`, with `/api` proxied to `http://localhost:5000` — the
browser sees one origin, so CORS never comes into play locally. Types are **not** checked here.

### `npm run build` = `tsc -b && vite build`

`tsc -b` only type-checks (both referenced projects are `noEmit`): `tsconfig.app.json` covers
`src/**` as browser code with `noUnusedLocals` / `noUnusedParameters`, `tsconfig.node.json` covers
`vite.config.ts`. `vite build` runs only if that passed, inlines `VITE_API_URL` into the bundle and
writes `dist/`. This is the command that catches a type error; `dev` does not.

### `npm run lint`

`oxlint` with the `react`, `typescript` and `oxc` plugins; `react/rules-of-hooks` is an error,
`react/only-export-components` a warning (it keeps Fast Refresh working). Not a type-checker.

### Docker

Two `node:24-alpine` stages: **build** (`npm ci` before `COPY . .`, so the dependency layer stays
cached; `VITE_API_URL` as a build argument) and **runtime** (`serve -s dist` on `${PORT:-8080}`,
only `dist/` copied). Changing the API URL means rebuilding the image — see
[`CONFIGURATION.md`](./CONFIGURATION.md#frontend-settings).

---

## Known limitations

Facts about the current code, not plans — what to change is tracked in Linear.

- One component; no routing, so there is no vacancy page — `GET /api/vacancies/{id}` exists but is
  never called, and the list DTO carries no description.
- Filters live in component state, not in the URL: a search cannot be linked or bookmarked, and
  the browser's Back button discards it.
- The publication date is in the data (`publishedAt`, `fetchedAt`) but not shown on cards.
- No source filter and no sort control; the API supports neither yet.
- No frontend tests.
- The visual theme is the Vite starter's (`--accent: #aa3bff` and unused `--social-bg`,
  `--code-bg`, `--shadow` in `index.css`).
