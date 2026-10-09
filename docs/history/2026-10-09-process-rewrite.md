# 2026-10-09 — The process rules and Claude's rules are rewritten (EM-66)

**What went wrong.** The rules lived in six places — `CLAUDE.md`, `CONVENTIONS.md`, `PLAN.md` §01,
`REPOSITORY.md`, the PR template and `.claude/settings.json` — and partly contradicted each other.
Nothing enforced them:

- `CLAUDE.md` told Claude to "open the PR, wait for build, report, and stop". The line came from a
  note Claude kept in its memory on 2026-09-29, which turned a rule about *merging* into a licence
  to *open* PRs, and it landed in PR #24. On 2026-10-06 Claude read "take Phase II to final review"
  as leave to open #51 and "make a reminder" as leave to create a Linear ticket.
- `.claude/settings.json` denied `gh pr create*` and asked before `git push*`, but only for those
  exact prefixes. `gh api` and `git -c … push` went straight past them.
- Status lived in four places. Phase II showed 0 of 8 in `PLAN.md` — §01 rule 3 had been read as
  "not until the phase merges into `main`" — while Linear had five tickets In Review and three In
  Progress. The three were dragged back by the Linear–GitHub integration, which links every ticket
  id in a PR title and read `EM-20–25` as EM-20 alone (EM-65).
- `Fix:`, `Chore:` and `PostReview:` were defined by the state of a branch rather than by the kind
  of work. Review answers went in as `Fix:`, and `Chore:` PRs went straight into `main` mid-phase,
  so the documentation on `main` described code `main` did not have.

**What replaced it.** Decided in an interview with the maintainer, recorded in EM-66:

- a ticket's box is ticked when its PR merges into the phase branch, and Linear moves to Done at
  the same moment;
- PR titles are `EM-n:`, `Fix: EM-n -`, `Hotfix: EM-n -` or `Phase N:`, checked by CI; `Chore:`
  and `PostReview:` are retired, the kind of work is a label, and every change has a ticket;
- Claude's behaviour rules live in `~/.claude/rules/` (mirrored in `.claude/rules/`); what Claude
  may do freely and what needs the maintainer's yes is a permission matrix in settings, with a
  hook that catches the command forms the rules cannot see;
- the documentation separates rules (`PROCESS.md`, `CONVENTIONS.md`), reference and this history.
