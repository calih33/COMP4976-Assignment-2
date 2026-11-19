# Assignment 1 → Assignment 2: What Changed and Why

Each entry: **A1** (how it worked before), **A2** (what the code does now), **Benefit**.
A1 code is in [`docs/a1-reference/`](a1-reference/) (commit `f8b65fa`). Feeds the Deficiency Resolution Report.

---

## Phase 0 — Solution and tooling

### One MVC project → eight-project Aspire solution
- **A1:** A single ASP.NET Core MVC project held models, data, admin, public site and API.
- **A2:** `MEMS` (domain), `MEMS.Data` (EF Core + services), `MEMS.Shared` (DTOs), `MEMS.Api`, `MEMS.Admin`
  (Blazor Server), `MEMS.Public` (Blazor WASM), `MEMS.AppHost` + `MEMS.ServiceDefaults` (Aspire).
- **Benefit:** Each part has one job and can only reach what it references (e.g. the public WASM app can't touch the
  database at all). Required by the A2 spec.

### Central build settings and package versions
- **A1:** Target framework and every package version were in the one `.csproj`.
- **A2:** `Directory.Build.props` (shared settings), `Directory.Packages.props` (all NuGet versions),
  `global.json` (pins the .NET 10 SDK), `.editorconfig` (naming/brace rules), local `dotnet-ef` 10 tool manifest.
- **Benefit:** Eight projects can't drift onto different versions; everyone on the team builds with the same SDK and tools.

### Aspire 13.6.5
- **A1:** No Aspire.
- **A2:** Aspire SDK 13.6.5 (the scaffold's 13.6.4 has a high-severity advisory, CVE-2026-120006).
- **Benefit:** No known vulnerable packages in the build.

### Copilot guidance rewritten for A2
- **A1:** Skills/prompts described MVC controllers, cookie voting guide, disk uploads and a mobile JWT API.
- **A2:** Same files updated for Blazor, Session Storage, images in SQL Server and the tenant API; `mobile-api` skill
  replaced by `tenant-api`; new `review-privacy` prompt.
- **Benefit:** AI assistance follows the new rules instead of reintroducing A1 patterns that A2 forbids.

---

## Phase 1 — Data layer

### SQLite → SQL Server
- **A1:** `UseSqlite`, `elections.db` file, SQLite migrations.
- **A2:** `UseSqlServer`; fresh `InitialCreate` migration for SQL Server.
- **Benefit:** Required by A2. Real server database with proper types (`date`, `varbinary(max)`, `nchar(7)`),
  check constraints and multi-column foreign keys.

### Municipality is now an explicit tenant (`Slug`)
- **A1:** Municipalities were identified only by `Name`/`MunicipalityId`.
- **A2:** `Municipality.Slug` (unique, lowercase, `^[a-z0-9]+(-[a-z0-9]+)*$`, also enforced by a SQL CHECK constraint)
  used in URLs (`/burnaby`, `/api/burnaby/...`). Lookup is case-insensitive, so `/Richmond` works.
- **Benefit:** Tenant resolution from the URL, and new municipalities work without new apps.

### Per-tenant theme colour
- **A1:** Every municipality looked the same.
- **A2:** `Municipality.ThemeColor` (hex, validated).
- **Benefit:** "Unique UI" per tenant on the public site without code changes.

### Admin groups: one role per municipality → two roles + `ApplicationUser.MunicipalityId`
- **A1:** A role per municipality (`"Pitt Meadows Admin"`), matched to `Municipality.AdminRoleName` by string.
  A user could end up in several municipality roles.
- **A2:** Roles are only `Super Admin` and `Municipality Admin`. `ApplicationUser : IdentityUser` has a nullable
  `MunicipalityId` foreign key; `AdminRoleName` is gone.
- **Benefit:** Matches the spec ("assigned to exactly one tenant"). The tenant link is a real foreign key instead of a
  name match, so renaming a municipality can't break access, and the user table now has a tenant relationship.

### Candidate gets its own `MunicipalityId`, enforced by a composite foreign key
- **A1:** A candidate's municipality was only reachable through `Position.Municipality`.
- **A2:** `Candidate.MunicipalityId` is copied from the position by the service, and the FK
  `(PositionId, MunicipalityId) → Positions(PositionId, MunicipalityId)` makes SQL Server reject a mismatch.
- **Benefit:** The spec lists Candidate as tenant-owned. Tenant filtering is a direct column check, and data integrity is
  guaranteed by the database, not just by application code.

### Tenant isolation in EF Core (global query filters)
- **A1:** Every admin controller action called `MunicipalityAccessService` and added `.Where(ids.Contains(...))` by hand;
  public queries weren't scoped.
- **A2:** `AppDbContext` has a global query filter on `Municipality`, `Position` and `Candidate` driven by a scoped
  `ITenantContext`. It starts with **no access** (fail closed); the host sets public (one tenant, read-only),
  admin (own tenant or all for Super Admin) or system (seeding).
- **Benefit:** The spec requires isolation "in Entity Framework queries". A forgotten `Where` can no longer leak another
  tenant's rows, because every query is filtered automatically.

### Controllers with inline queries → service layer
- **A1:** Each MVC controller queried `ApplicationDbContext` directly and repeated the access checks.
- **A2:** `IMunicipalityService`, `IPositionService`, `ICandidateService`, `IImageService`, `IDashboardService` in
  `MEMS.Data`. They return a `ServiceResult` (Ok / NotFound / Forbidden / Invalid + field errors), log with structured
  messages, and re-validate data annotations server-side. Each call opens a short-lived context.
- **Benefit:** Required by the spec ("repository or service layer"). Admin and API share one set of rules, so the
  tenant checks exist once. The short-lived context avoids Blazor Server's concurrent-`DbContext` errors.

### Images: files on disk → SQL Server
- **A1:** `ImageStorageService` saved files to `wwwroot/uploads` (or `/home/data/uploads` on Azure);
  `LogoFileName`/`PhotoFileName` columns; `IFormFile` properties on the entities.
- **A2:** `StoredImage` table (`ContentType`, `Data`); `LogoImageId`/`PhotoImageId` foreign keys. Entities no longer
  contain ASP.NET types. Images are visible only through a tenant the caller can see (`IImageService`).
- **Benefit:** Admin and the API are separate processes, so a local uploads folder would only exist in one of them.
  Images are now backed up with the database, deleted with their record in the same transaction, and tenant-isolated.

### Stronger image validation (file signature check)
- **A1:** Checked extension, browser-reported content type and size (2 MB).
- **A2:** Same rules in `ImageRules` (shared by Admin and server), **plus** a magic-byte check of the actual file
  (JPEG/PNG/WebP). The stored content type comes from the bytes, not the browser.
- **Benefit:** A renamed non-image file can no longer be uploaded and served as an image.

### `SavedCandidates` table removed
- **A1:** The mobile API stored each user's saved candidates in the database.
- **A2:** Table, entity and endpoints removed.
- **Benefit:** A2's election privacy rule forbids storing ballot selections in the database.

### `ElectionDate`: `DateTime` → `DateOnly`
- **A1:** `DateTime` with a `[DataType(Date)]` hint.
- **A2:** `DateOnly`, stored as SQL `date`.
- **Benefit:** No time-zone or midnight-shift bugs; the column means exactly "a date".

### Validation in three layers
- **A1:** Data annotations (client + server) and some EF indexes.
- **A2:** Data annotations on models (Blazor + API), re-validated in the service layer, plus database constraints:
  max lengths, unique indexes (name, slug, position per municipality), CHECK constraints (slug format, 1–50 seats),
  composite tenant foreign key.
- **Benefit:** The spec requires client, API and database validation; bad data is rejected even if one layer is bypassed.

### Seed data
- **A1:** Pitt Meadows, Maple Ridge, Burnaby; users `super`, `pm`; skipped seeding entirely if any municipality existed.
- **A2:** Burnaby, Surrey, Richmond (required) + Pitt Meadows (keeps `pm` working); 41 candidates; users `super`,
  `pm`, `burnabyadmin`, `surreyadmin`, `richmondadmin`. Each tenant and user is checked individually.
  "Councilor" → "Councillor" (Canadian spelling, as in the A2 spec).
- **Benefit:** Meets the A2 seed requirements, and an existing database picks up any missing tenant or user on startup.

### Case-insensitive search without `ToLower()`
- **A1:** `c.Name.ToLower().Contains(search.ToLower())` because SQLite compares case-sensitively.
- **A2:** `c.Name.Contains(term)`; SQL Server's default collation is case-insensitive.
- **Benefit:** Simpler query that can use the `(MunicipalityId, Name)` index.

---

## Phase 2 — Aspire orchestration

### `dotnet run` of one app → Aspire AppHost orchestrating five resources
- **A1:** `dotnet run` started the MVC app; the SQLite file was created next to it.
- **A2:** `MEMS.AppHost` starts SQL Server (container + data volume), the API, Admin, the public WASM site and a
  gateway, in dependency order (`WaitFor`): database → API (migrates + seeds) → Admin/Public → gateway.
- **Benefit:** One command runs the whole distributed system, and nothing starts before what it depends on is healthy.

### Connection string in `appsettings.Development.json` → managed by Aspire
- **A1:** `ConnectionStrings:DefaultConnection` in a git-ignored `appsettings.Development.json` that each developer
  had to copy from an `.example` file.
- **A2:** `WithReference(db)` injects `ConnectionStrings:memsdb` into the API and Admin at launch. The SQL Server
  password is generated by Aspire and kept in the AppHost's user secrets. No connection string in any config file.
- **Benefit:** Required by A2 ("connection strings must be managed using Aspire"). No manual setup step and no secrets
  that could be committed by mistake.

### Migrations/seeding on app startup → owned by the API only
- **A1:** The single app migrated and seeded on startup.
- **A2:** Only the API migrates and seeds; Admin and Public `WaitFor(api)`.
- **Benefit:** Two apps share one database, so exactly one of them owns the schema. No race between two migrators.

### Observability: none → OpenTelemetry + health checks via ServiceDefaults
- **A1:** Console logging only.
- **A2:** `AddServiceDefaults()` gives the API and Admin OpenTelemetry logs, traces and metrics (shown in the Aspire
  dashboard), `/health` and `/alive` endpoints, and resilient `HttpClient`s. `EnrichSqlServerDbContext` adds a
  database health check, connection retries and EF Core query tracing. The AppHost polls `/health`.
- **Benefit:** Required by A2 (logging, tracing, health checks in the dashboard). A request can be followed from the
  gateway through the API into the SQL query.

### One host and port → gateway with tenant URLs (service discovery)
- **A1:** One app on one port; public pages at `/Municipalities`, admin at `/Admin/...`.
- **A2:** A YARP gateway on `localhost:5001` routes `/api/**` → API, `/admin/**` → Admin (path base `/admin`),
  everything else → the public WASM site (`/burnaby`, `/surrey`, ...). Routes point at Aspire resources, not ports.
- **Benefit:** Matches the spec's URLs (`localhost:5001/burnaby`, `/admin`). Apps find each other through Aspire
  service discovery. The WASM site calls the API on the same origin, so no CORS and no API URL baked into the client.

---

## Phase 3a — WebAPI

### Read-only, global API → tenant-scoped API with full CRUD
- **A1:** `/api/municipalities`, `/api/positions`, `/api/candidates` were GET-only and returned every municipality's
  data, filtered by optional `?municipalityId=` query strings.
- **A2:** Tenant-owned routes include the slug (`/api/burnaby/candidates`). GET, GET by id, POST, PUT and DELETE for
  municipalities, positions and candidates, plus logo/photo upload and image serving.
- **Benefit:** Required by A2. The tenant is part of the address, so a client can't get another tenant's rows by
  changing a query parameter; and future clients can manage data, not just read it.

### Tenant validated in the API layer, not just the UI
- **A1:** The API had no tenant concept; admin restrictions existed only in the MVC admin controllers.
- **A2:** `TenantContextFilter` runs before every action: unknown slug → 404; a Municipality Admin token used on
  another tenant's route → 403 (logged as a warning). It then sets the tenant context, so the EF query filters and the
  service checks apply to everything the action does. A record id from another tenant returns 404.
- **Benefit:** The spec's examples hold even when calling the API directly: a Burnaby admin can't read or change Surrey
  data. Verified by `tools/api_smoke_test.py`.

### JWT for mobile users → JWT for admins, with role and tenant
- **A1:** JWT existed only for COMP4977 mobile users (`api/auth/register`, no roles); signing key in
  `appsettings.Development.json`.
- **A2:** `POST /api/auth/login` issues JWTs to admin accounts only, carrying `role` and `tenant_id`. Same message for
  unknown user and wrong password; lockout after 5 failures. Public registration removed. The signing key is a
  generated Aspire secret parameter (user secrets), never in a config file.
- **Benefit:** API writes are restricted to admins of the right tenant without a database lookup per request, and no
  secret can be committed.

### Entities/ad-hoc DTOs → shared DTOs in `MEMS.Shared`
- **A1:** DTOs lived in the MVC project (`Models/Api`) and used COMP4977 field names.
- **A2:** Request/response records in `MEMS.Shared` with the same validation attributes as the models; the WASM site
  will reuse them. Image URLs point to the tenant-scoped image endpoint.
- **Benefit:** One contract shared by server and client; the public site can't drift from what the API returns.

### Error handling → ProblemDetails everywhere
- **A1:** API errors used `Problem(...)` in some actions; unhandled exceptions used the MVC error page.
- **A2:** `AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages`: every error (400/401/403/404/409/500) is
  RFC 7807 JSON with a `traceId`. A dedicated handler turns database constraint races into 409. No stack traces or
  SQL messages are returned. `X-Content-Type-Options: nosniff` on every response.
- **Benefit:** Required by A2 (global exception handling, no sensitive info). Clients get one predictable error format,
  and the `traceId` links an error to its trace in the Aspire dashboard.

### Markdown endpoint list → Swagger/OpenAPI + documentation
- **A1:** `docs/api-endpoints.md` written by hand.
- **A2:** .NET 10 OpenAPI document generated from XML comments and `[ProducesResponseType]`, Swagger UI with a JWT
  "Authorize" button at `/api/swagger`, `docs/api.md` with request/response/error examples taken from real responses,
  and a runnable `.http` file.
- **Benefit:** Meets the API documentation requirement, and the docs come from the code, so they stay accurate.
