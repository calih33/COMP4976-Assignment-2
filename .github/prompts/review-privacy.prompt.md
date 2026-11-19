---
description: Audit that voting-guide selections never leave the browser (election privacy requirement)
agent: ask
---

Check the election privacy rules from the assignment against the code:

1. The voting guide in `MEMS.Public` stores candidate IDs only in `sessionStorage` (not `localStorage`, cookies,
   or any server call).
2. No API endpoint accepts a list of selected candidate IDs, and the guide page does not send them in a query
   string, header or body. It loads all candidates for the tenant and filters in the browser.
3. No entity, table or migration in `MEMS.Data` stores selections, picks, favourites, saved candidates or vote counts.
4. Logging never writes guide contents (check `ILogger` calls in `MEMS.Public` and the API).
5. Admin pages have no screen that shows voter selections.

Report each rule as pass/fail with `file:line` evidence. Don't change code unless asked.
