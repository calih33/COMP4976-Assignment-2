# Assignment 1 reference code (temporary)

Read-only copy of the Assignment 1 MVC app, exported with `git archive` from
`calih33/Municipal-Elections-Management-System` at commit `f8b65fa` (the submitted version).

It is **not compiled** and is not part of the solution. It's here so we can port logic into the
A2 projects without switching repos:

| A1 file(s) | Ported to (A2) |
|---|---|
| `Models/*.cs` | `MEMS/Models/` (minus `SavedCandidate`, `IFormFile` props, `AdminRoleName`) |
| `Data/ApplicationDbContext.cs`, `Data/SeedData.cs`, `Data/AppRoles.cs` | `MEMS.Data/` (SQL Server, tenant filter, new seed users) |
| `Services/MunicipalityAccessService.cs` | `MEMS.Data/Services/` → `ITenantContext` + services |
| `Services/ImageStorageService.cs` (`CheckImage`) | `MEMS.Data/Services/ImageService.cs` (images stored in SQL Server) |
| `Services/JwtTokenService.cs` | `MEMS.Api/` |
| `Areas/Admin/**` | `MEMS.Admin/Components/Pages/**` (Blazor Server) |
| `Controllers/*.cs`, `Views/**` (public site) | `MEMS.Public/Pages/**` (Blazor WASM) |
| `Controllers/Api/*.cs` | `MEMS.Api/Controllers/` (tenant-scoped, full CRUD) |
| `Services/VotingGuideService.cs` (cookie) | replaced by Session Storage in `MEMS.Public` |
| `Controllers/Api/SavedCandidatesApiController.cs`, `Models/SavedCandidate.cs` | **not ported** — A2 forbids storing ballot selections |
| `wwwroot/css/site.css`, `wwwroot/js/*.js`, `wwwroot/images/*` | Admin/Public `wwwroot` (colours, dark mode, placeholders) |

Not exported: Bootstrap `wwwroot/lib` (A2 projects ship their own), SQLite migrations, project files,
and git-ignored files such as `appsettings.Development.json`.

**Delete this folder before submission** (Phase 5). The original stays in the A1 repo.
