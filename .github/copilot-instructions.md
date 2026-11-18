# MEMS Assignment #2 — GitHub Copilot Instructions

These instructions guide GitHub Copilot when suggesting code in this repository.
They describe the **target** architecture for Assignment #2.

## What this app is

A multi-tenant, cloud-native re-architecture of the Assignment #1 MVC election app.
**Each municipality is a tenant**, resolved from the URL (`/burnaby`, `/surrey`, `/richmond`).

- **Admin site** (`MEMS.Admin`, Blazor Server, login required): dashboard + CRUD for municipalities
  (logo upload), positions, candidates (photo upload), and tenant management.
- **Public site** (`MEMS.Public`, Blazor WebAssembly, anonymous): per-tenant browse, search/filter,
  candidate details, and a Session-Storage voting guide (print supported).
- **WebAPI** (`MEMS.Api`): tenant-scoped REST endpoints consumed by the public site.

## Stack

- .NET 10 (`net10.0`), orchestrated by **.NET Aspire** (13.6)
- EF Core 10, **SQL Server** (SQLite is NOT permitted), Code First with migrations
- ASP.NET Core Identity (`IdentityUser`/`IdentityRole`): cookie login for Admin, JWT for the API
- Bootstrap 5

## Solution layout

| Project | Contents |
|---|---|
| `MEMS` | Core domain models + business logic |
| `MEMS.Data` | `AppDbContext`, migrations, seed, repositories/services, SQL Server config |
| `MEMS.Shared` | DTOs exchanged between the API and the WASM client |
| `MEMS.Api` | `[ApiController]` controllers, tenant-scoped routes, Swagger |
| `MEMS.Admin` | Blazor Server components (InteractiveServer), Identity, QuickGrid lists |
| `MEMS.Public` | Blazor WASM pages, `HttpClient` API calls, Session Storage |
| `MEMS.AppHost` | Aspire orchestration (SQL Server + Api + Admin + Public) |
| `MEMS.ServiceDefaults` | Shared telemetry, health checks, service discovery |

## Conventions (match the course examples — Medhat Elmasry)

- File-scoped namespaces; PascalCase types/members; `private readonly _camelCase` DI fields.
- Nullable reference types enabled; don't silence warnings with `!` unless the value is guaranteed.
- Async all the way: `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`.
- EF Code First: edit the model, then `dotnet ef migrations add <Name> -o Data/Migrations`.
  Migrations are applied on startup (`context.Database.Migrate()` in a service scope). Never hand-edit a pushed migration.
- Central Package Management: NuGet versions live in `Directory.Packages.props`; `.csproj` files
  reference packages **without** a `Version` attribute.
- Aspire: apps talk to each other by **service-discovery alias** (e.g. `http://apiservice/`),
  never a hardcoded host/port. Connection strings flow by resource name, not `appsettings.json`.

## Multi-tenancy rules (critical)

- Tenant = `Municipality`, identified by a unique `Slug` (`burnaby`). Entities carrying a tenant key:
  `Municipality`, `Position`, `Candidate`, and the admin user.
- The active tenant is resolved from the URL. **Every** public query and API response is filtered to
  the active tenant. A user must never see another tenant's data by changing the URL or an API request.
- Enforce isolation in **three** layers: the Blazor UI, the WebAPI (validate the tenant in the route,
  not just the UI), and EF Core (global query filter on the tenant key).
- API routes are tenant-scoped: `GET /api/{tenant}/candidates` returns only that tenant's candidates.

## Authorization rules (critical)

- Roles: **Super Admin** (manages all tenants) and **Municipality Admin** (assigned to exactly one tenant).
- A Municipality Admin can only view/create/modify records within their own tenant; never another's.
- Every admin query and POST re-checks the tenant boundary on the server; never trust IDs from hidden
  fields or dropdowns. On failure return `Forbid()`.
- Creating/deleting tenants (municipalities) is Super Admin only.
- The API uses the JWT scheme explicitly for protected endpoints; the Admin site uses the cookie default.

## Election privacy (required, graded)

- Ballot/voting selections are stored **only** in the browser's **Session Storage**.
- They must NOT be written to SQL Server, transmitted to the API, or visible to admins.
- The database stores municipalities, positions, candidates, tenants, config, and admin accounts only.

## Validation (three layers)

- Data annotations on the models drive Blazor (`DataAnnotationsValidator`) and API (`[ApiController]` 400s).
- Database-level constraints via EF configuration (required, max length, unique).
- Image uploads: `.jpg/.jpeg/.png/.webp`, max 2 MB, checked client- and server-side.

## UI

- Bootstrap 5 only; responsive (desktop + mobile); consistent navigation; meaningful error messages.
- Admin list views use **QuickGrid** (paging/sorting). Forms use `EditForm` + validation components.

## Logging & errors

- Structured logging and global exception handling; never expose sensitive info to end users.

## Don't

- Don't use SQLite. Don't let the WASM client touch the database directly (API only).
- Don't persist ballot selections anywhere server-side.
- Don't add packages without a clear need; prefer what ASP.NET Core / Aspire already provide.
- Don't catch and swallow exceptions.
