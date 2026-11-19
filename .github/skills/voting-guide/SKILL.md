---
name: voting-guide
description: How the public voting guide stores selected candidates in browser Session Storage (never on the server) and how the printable guide is built. Use when working on the guide pages, "Add to guide" buttons, the guide count, or the print page in MEMS.Public.
---

# Voting guide (election privacy — graded)

Selections must stay in the voter's browser session. They are **never** written to SQL Server, **never** sent to
the API, removed automatically when the browser session ends, and invisible to admins. (A1 used a 60-day cookie;
A2 replaces it.)

## GuideStorage (MEMS.Public/Services)

Wraps native `sessionStorage` through `IJSRuntime` (no extra package):

| Member | What it does |
|---|---|
| key | `mems-guide:{slug}` — one guide per tenant |
| `GetIdsAsync(slug)` | Reads and parses the JSON int array; ignores bad data |
| `AddAsync(slug, id)` / `RemoveAsync(slug, id)` | Updates and re-saves |
| `ClearAsync(slug)` | Removes the key |
| `Changed` event | Lets the nav badge update its count |

```csharp
await _js.InvokeVoidAsync("sessionStorage.setItem", Key(slug), JsonSerializer.Serialize(ids));
var json = await _js.InvokeAsync<string?>("sessionStorage.getItem", Key(slug));
```

## Pages

| Route | Notes |
|---|---|
| `/{tenant}/guide` | Loads **all** candidates for the tenant from the API, then filters to the stored IDs in the browser. Remove / clear buttons. |
| `/{tenant}/guide/print` | Printer-friendly: municipality name, election date, each candidate's name and position, print date. Print button calls `window.print()` inside `d-print-none`. |

Never call an API with the selected IDs (no `?ids=1,2,3`) — that would transmit ballot selections.

Print date: format the browser's local date (WASM runs in the voter's time zone), e.g. `DateTime.Now.ToString("MMMM d, yyyy")`.

## Verify

Add candidates, refresh (still there), close the tab and reopen (gone). In DevTools → Network, no request contains
selected IDs. In DevTools → Application → Session Storage, the `mems-guide:{slug}` key holds them. Run the
`/review-privacy` prompt.
