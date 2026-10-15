# Decisions: interview, don't guess

An interview is mandatory when any of these holds:

- two or more reasonable options exist;
- the choice touches process, conventions, architecture, data, permissions or an external system;
- the work is a multi-step plan;
- an instruction or an approval is ambiguous — for example "go ahead" while several actions are waiting;
- sources contradict each other, including the user's message against a repository rule.

No interview for a local, reversible implementation detail that already has a pattern in the code: do it and mention it in the report.

How:

1. Do the homework first — read the code, the documents and the live state — so every option is concrete.
2. Ask with AskUserQuestion. Cover every open item, up to four questions per round, as many rounds as it takes. Put the recommended option first and mark it; state each option's consequence in its description. Never ask for a typed "yes".
3. Write each round's answers into the ticket's Decided section right away, then into the document they change. Read Decided again before resuming the task.
4. Do not ask what the code or the documents already answer, and answer the user's question before asking one of mine.
