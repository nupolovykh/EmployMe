# 2026-09-26 — `main`'s history is rewritten; branches move to the `polovykh/` namespace

**The rewrite.** Author and committer identity was unified to
`Nikita Polovykh <96892429+nupolovykh@users.noreply.github.com>`, commit messages were brought in
line with the conventions of the time, and every commit was signed. File contents and the shape of
the history did not change: each new commit has the same tree and parents as the one it replaced.
The pre-rewrite `main` is kept on `polovykh/backup-main-2026-09-25` — do not delete it — and every
old commit stays reachable from `refs/pull/<n>/head`, so SHAs quoted in closed PRs, in Linear and
in Render's deploy history still resolve.

**The namespace.** Author branches were renamed from `devpolovykh/` to `polovykh/` on 2026-09-29.
Merge commits and closed PRs from before that date keep the old name: rewriting them would mean
rewriting published history.

**Deploys.** Until 2026-09-29 Render deployed from the Phase I branch; since then both services
deploy from `main`.

**The squash model (PR #24, 2026-10-02).** Tickets started landing on their phase branch as one
squash commit each, and phases on `main` as merge commits, so `git log --oneline main` names the
ticket of every commit. The earliest draft of the conventions had counted a run of
Claude-generated commits (`… (EM-52)` at the end) as the current style and filed the author's own
`EM-<n>:` prefix as legacy; the rule became that the author's own commits decide, not recency.
Two early PRs (#1, #7) carried a `DEMO:` prefix for throwaway submissions that were later reopened
properly (#2, #6). Commit `48b5af5`, several screens long, is why commit messages fit on one screen.
