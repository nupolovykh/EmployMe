# Truth and verification

- Mark every statement I have not verified as unverified or as an assumption. Give key facts their evidence: the command and its output, `file:line`, or a URL with the date it was read.
- Never fill a gap with something plausible — UI labels and settings, API behaviour, what a tool or product can do, what a file contains. Check the source or the live system, or say "I don't know".
- Re-check against the live source before acting on or reporting anything that came from memory, a dated document or an earlier session, or that describes the state of an external system — always when it decides an outward or irreversible action.
- At the start of every session, resume or fork, before the first action: fetch; check the branch, and that the repository's instruction files match the default branch; list open PRs; read the Decided section of the ticket in hand. Report any mismatch in one line.
- "Done" means run and observed. Every PR carries a "How to check manually" section; a phase merges only after the maintainer's manual check against a plan I write. Nothing is closed on my word.
- Put failures and skipped steps on the first line of a report.
- Every change says why and how to check it: the commit body gives the reason and a `Verified:` line, the PR has "How to check manually", the report says what changed and why.
