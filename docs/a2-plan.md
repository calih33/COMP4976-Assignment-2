# Assignment 2 Plan — MEMS Modernization

**Due:** Fri Nov 20, 2026, 11:59 pm · **Team:** Isaac Kehler, Cali Heung
**Repo:** this one (`calih33/COMP4976-Assignment-2`). A1 code is imported from
`calih33/Municipal-Elections-Management-System` (commit `f8b65fa`), which stays untouched as the "before" for the deficiency report.

We are migrating the A1 MVC app into the 8-project Aspire solution the spec requires. We keep A1's working
logic (access checks, image validation, seed data, search/filter) and change what A2 forbids or adds
(SQLite, cookie guide, `SavedCandidates`, explicit tenants, Blazor, full CRUD API).

---

## Architecture decisions

| # | Decision | Why |
|---|---|---|
| D1 | **Tenant = `Municipality`** with a unique lowercase `Slug` (`burnaby`). Slug lookup is case-insensitive (`/Richmond` works). | Spec §2 Tenant Resolution |
| D2 | **Two roles only:** `Super Admin`, `Municipality Admin`. Tenant assignment is `ApplicationUser.MunicipalityId` (null for Super Admin). Replaces A1's one-role-per-municipality (`AdminRoleName`). | Spec §5: admin assigned to exactly one tenant; "Administrator User" needs a tenant relationship |
| D3 | `Candidate` gets its own `MunicipalityId` (must equal its `Position.MunicipalityId`, checked in the service). | Spec §4 Tenant Data Model lists Candidate |
| D4 | **Isolation in three layers:** (a) EF global query filter on `MunicipalityId` driven by an `ITenantContext`; (b) API validates route slug vs. JWT tenant claim; (c) Blazor pages only load the current admin's tenant. | Spec §5 "enforced in Blazor, WebAPI, EF queries" |
| D5 | **Service layer in `MEMS.Data`** (`IMunicipalityService`, `IPositionService`, `ICandidateService`, `ITenantContext`). Admin and API both use it. Admin talks to the DB through it directly (Blazor Server runs on the server). | Spec §4 "repository or service layer"; saves building an HTTP client for Admin |
| D6 | **API auth:** GETs are public and tenant-scoped. POST/PUT/DELETE require a JWT (`/api/auth/login`, reuse A1 `JwtTokenService`) carrying `role` + `tenant_id`. Super Admin may use any tenant; others get 403 on a mismatched slug. | Spec §3: "Burnaby admin must not retrieve Surrey data… validation in the API layer" |
| D7 | **Images stored in SQL Server** (`StoredImage` table: Id, ContentType, Data, MunicipalityId), max 2 MB, jpg/png/webp. Served by `GET /api/{tenant}/images/{id}` and an Admin endpoint. | Admin and API are separate processes; avoids a shared disk folder |
| D8 | **Voting guide = native `sessionStorage` via `IJSRuntime`**, key `mems-guide:{slug}`, stores candidate IDs only. The guide page fetches *all* tenant candidates and filters **in the browser**, so selected IDs are never sent to the API. | Spec "Election Privacy" (5 marks) |
| D9 | **Delete `SavedCandidate`** table, API and mobile JWT register flow. | DB must not store ballot selections |
| D10 | **API applies migrations + seed on startup**; Admin and Public `WaitFor(api)`. | Only one process migrates; no extra project needed |
| D11 | **Routing / service discovery — spike first (≤1 h):** add a YARP gateway in AppHost (`Aspire.Hosting.Yarp`) on port 5001: `/api/**` → api, `/admin/**` → admin (Admin uses `UsePathBase("/admin")`), `/**` → public. Gives the spec's exact URLs (`localhost:5001/burnaby`, `/admin`) and real service discovery. **Fallback** if it fights us: Public reads the API URL from `wwwroot/appsettings.json`, API enables CORS for the Public origin, Admin pages live under `@page "/admin/..."`. | Spec §6 service discovery + URL examples |

---

## Data model (target)

```
Municipality (tenant)        Position                    Candidate
  MunicipalityId PK            PositionId PK               CandidateId PK
  Name        req, 100, uniq   Name  req, 50               Name     req, 100
  Slug        req, 50, uniq    SeatsAvailable 1–50         Profile  req, 2000
              ^[a-z0-9-]+$     MunicipalityId FK ──┐       Website  url, 200
  Province    req, 50          uniq(MunicipalityId,Name)   Email    email, 100
  ElectionDate req                                 │       DateCreated
  Description 1000                                 │       PhotoImageId FK? → StoredImage
  ThemeColor  #rrggbb (per-tenant UI)              │       PositionId FK
  LogoImageId FK? → StoredImage                    │       MunicipalityId FK ─┐
                                                   │                          │
ApplicationUser : IdentityUser                     │                          │
  MunicipalityId FK? (null = Super Admin) ─────────┴──────────────────────────┘
```

Domain classes in `MEMS` have no ASP.NET dependency (no `IFormFile`). DTOs with validation attributes go in
`MEMS.Shared` (used by both API and WASM).

---

## Phases

### Phase 0 — Repo setup (Isaac, ~1 h) · Wed
- [x] Work on feature branches in this repo; merge to `main` via PR (Phase 0 is on `phase-0-setup`)
- [x] Add `global.json` (10.0.100, `latestFeature`), `Directory.Build.props`, `Directory.Packages.props` (central versions), `.editorconfig`
- [x] Import A1 code (`git archive f8b65fa`) into `docs/a1-reference/` — not compiled, so the build stays green;
      Phase 1 ports models/data/services into the projects from there. Delete the folder before submission.
- [x] Copy A1 `.github/prompts`, `skills`, `instructions` in, rewritten for A2 (Blazor, Session Storage guide,
      images in SQL Server); `mobile-api` skill replaced by `tenant-api`; new `review-privacy` prompt; `CLAUDE.md` added
- [x] Bump Aspire to 13.6.5 (fixes CVE-2026-120006 in < 13.6.5)
- [x] Remove template Weather/Counter pages, `WeatherForecastController`, empty `Class1.cs` files

### Phase 1 — Data layer (Isaac) · Wed
- [ ] Models per diagram above; remove `SavedCandidate`, `AdminRoleName`
- [ ] `AppDbContext : IdentityDbContext<ApplicationUser>` with SQL Server, constraints, unique indexes, global tenant filter
- [ ] Fresh `InitialCreate` migration for SQL Server (drop the SQLite migrations); drop `ToLower()` search hacks
- [ ] Seed: **Burnaby, Surrey, Richmond, Pitt Meadows** (Pitt Meadows keeps `pm` working), Mayor/Councillor/School Trustee each,
      **≥ 20 candidates** (reuse A1 names), users `super`, `pm`, `burnabyadmin`, `surreyadmin`, `richmondadmin` — password `P@$$w0rd`
- [ ] Services + `ITenantContext` (port `MunicipalityAccessService` logic); `ImageService` (port `CheckImage`)

### Phase 2 — Aspire wiring (Isaac) · Wed
- [ ] AppHost: `AddSqlServer("sql").WithDataVolume().AddDatabase("memsdb")`; api/admin/public `WithReference` + `WaitFor`
- [ ] Api and Admin call `AddServiceDefaults()` / `MapDefaultEndpoints()`; `AddSqlServerDbContext<AppDbContext>("memsdb")`; DB health check
- [ ] D11 spike: gateway or fallback — decide by end of day
- [ ] ✅ Checkpoint: `dotnet run --project MEMS.AppHost` shows all resources healthy and a seeded database

### Phase 3a — WebAPI (Isaac) · Thu
- [ ] `api/{tenant}/municipality` (GET/PUT; POST/DELETE at `api/municipalities` Super Admin only)
- [ ] `api/{tenant}/positions` and `api/{tenant}/candidates` — GET, GET by id, POST, PUT, DELETE; candidates support `?search=&positionId=`
- [ ] `api/{tenant}/images/{id}`, `api/auth/login`
- [ ] Tenant resolution filter: unknown slug → 404 ProblemDetails; mismatch with token → 403
- [ ] Swagger UI with XML comments + `[ProducesResponseType]` for request/response/error examples; update `.http` file
- [ ] `AddProblemDetails()` + `UseExceptionHandler()` — no stack traces to clients

### Phase 3b — Admin (Blazor Server) (Cali) · Thu
- [ ] Identity login/logout pages (username login, no registration), `[Authorize]` on all pages
- [ ] Dashboard: totals of municipalities / positions / candidates (scoped to tenant for Municipality Admin)
- [ ] Tenant management (Super Admin): create / edit / delete, set slug, logo upload, theme color
- [ ] Positions and Candidates CRUD with QuickGrid lists, `EditForm` + `DataAnnotationsValidator`, `InputFile` with
      client-side type/size check, photo upload
- [ ] Municipality Admin: tenant fixed to their own; every save re-checks on the server (port A1's `CanManage` checks)
- [ ] Bootstrap layout from A1 (colours, navbar, dark mode)

### Phase 3c — Public (Blazor WASM) (Cali) · Thu
- [ ] `/` — municipality picker (browse municipalities)
- [ ] `/{tenant}` — home with logo, description, election date, theme color
- [ ] `/{tenant}/positions`, `/{tenant}/candidates` (search by name, filter by position), `/{tenant}/candidates/{id}` with photo
- [ ] Unknown slug → friendly "municipality not found" page
- [ ] Typed `ApiClient` in WASM; all data via API only

### Phase 4 — Privacy + quality (both) · Fri morning
- [ ] `GuideStorage` service over `sessionStorage` (add / remove / clear / list); "Add to guide" buttons; `/{tenant}/guide`
- [ ] `/{tenant}/guide/print`: municipality, election date, candidate names, positions, print date; print CSS
- [ ] Verify: no ballot data in DB tables, no guide IDs in API requests (browser Network tab), guide gone after closing the tab
- [ ] Validation in all three layers; structured logging (`ILogger` with message templates) in services/controllers
- [ ] Cross-tenant tests: `burnabyadmin` token → `GET/PUT /api/surrey/...` = 403; Admin pages with another tenant's id = forbidden
- [ ] Responsive check on mobile widths

### Phase 5 — Docs + submission (both) · Fri afternoon
- [ ] Architecture diagram (Admin, gateway/routing, 3 tenant WASM experiences, API, SQL Server, Aspire; tenant isolation for Burnaby/Surrey/Richmond)
- [ ] Updated ERD
- [ ] **Deficiency Resolution Report** (Issue / Original Problem / Resolution) — needs the instructor's A1 feedback
- [ ] Copilot usage doc: how used, example prompts, productivity, problems
- [ ] API docs (Swagger export or `docs/api-endpoints.md` updated)
- [ ] Screenshots: Aspire dashboard (resources, logs, traces, health), Admin, Public, API testing, SQL Server tables
- [ ] Add `docs/readme.txt` (team, contributions, not completed, challenges, testing steps) and update `README.md`;
      everything merged to `main`; confirm instructor has access to this repo

---

## Suggested split

Based on A1 (Isaac did data/identity/access, Cali did public site and admin views):

- **Isaac:** Phases 0, 1, 2, 3a, cross-tenant tests, architecture diagram + ERD
- **Cali:** Phases 3b, 3c, guide/print, UI polish, screenshots
- **Both:** deficiency report, Copilot doc, final review

Phase 3a/3b/3c can run in parallel once the Phase 2 checkpoint passes — that's the critical path, so it must land Wednesday.

## Open items

1. **Instructor's A1 feedback** — needed for the deficiency report (10 marks). Who has it?
2. **D11 gateway vs. fallback** — decide after the spike.
3. Spec contradiction: p. 6 says guide uses *local* storage; the privacy section requires *Session* Storage. We use Session Storage and note it in the docs.
4. "Filter by municipality" inside a single-tenant site: the tenant *is* the filter; the `/` picker covers browsing across municipalities. Note this in the docs.
