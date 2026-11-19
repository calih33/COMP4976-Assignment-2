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
