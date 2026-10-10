# 2026-09-18 — The scheduler reaches three assumption expiries

Three entries in `ASSUMPTIONS.md` had EM-18 (scheduled ingest) as their expiry. All three were
reached when it was built, and each was resolved differently.

- **A-011, Render Free sleeps.** The expiry said "does not sleep" would become a requirement. It
  did not: the sleep was routed around. A GitHub Actions cron calls the ingest endpoint hourly,
  which wakes the instance and runs ingest under the same interval rules (A-014). Render Free stays.
- **A-012, the advisory lock.** A scheduler firing during a manual run makes concurrent runs
  routine, which was the point at which "mutual exclusion except across a reconnect" would stop
  being acceptable. The lock was kept: the scheduler runs with `force=false`, so an overlapping
  tick is refused by the lock, and one landing during a lapsed lock is still refused by
  `min_poll_interval` — unless the manual run was forced, had not stamped `LastSuccessAt`, and its
  connection reconnected mid-run. The integration tests (EM-23) now pin the skipped/due/forced
  behaviour.
- **A-013, the held connection.** Still `assumed`, and the window got longer: EM-20's retry
  pipeline can hold a fetch for up to 120 s inside the held connection. Nothing was measured.

Each entry carries a new expiry in `ASSUMPTIONS.md`.
