# EmployMe — Web

React + TypeScript + Vite frontend for the vacancies list. See the repo root [`README.md`](../../README.md) and [`PLAN.md`](../../PLAN.md) for project context.

## Development

```bash
npm install
npm run dev
```

The dev server proxies `/api/*` to the ASP.NET Core API at `http://localhost:5000` (see `vite.config.ts`) — run `dotnet run --project src/Api` alongside it so vacancy data loads.

## Commands

```bash
npm run dev      # dev server with HMR
npm run build    # type-check (tsc -b) + production build
npm run lint      # oxlint
npm run preview  # preview the production build locally
```
