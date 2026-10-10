# 2026-08-28 — Railway is dropped; Render and Neon go live

EM-17 was written for Railway. Its trial expired and left no free path, and the deployment moved
to Render (API as a Docker web service, frontend as a static site) and Neon (Postgres 18 with
pgvector). Both went live on 2026-08-28.

The failure was in the assumption register's coverage, not in any one claim: it had ten entries,
every one about a job source or the semantic layer, and nothing recorded what the project runs on.
"We can host this for nothing" sat unexamined until it failed by surprise. `ASSUMPTIONS.md` A-011
was opened to close that gap and verified live the same day: Neon's non-superuser owner can still
`CREATE EXTENSION vector`, Render's free instance builds a Dockerfile, the ingest guards hold in a
deployed environment, and CORS names the frontend's origin only.

Railway Hobby stays the fallback in A-011 and A-014: the Dockerfiles read `PORT` at start, so a
move is a re-point, not a rewrite.
