# MEMS — Municipal Elections Management System (COMP4976 Assignment #2)

Cloud-native, multi-tenant re-architecture of the Assignment #1 MVC app, built with
**.NET Aspire**, **Blazor**, **ASP.NET WebAPI**, **SQL Server**, and **EF Core**.

> Course: COMP4976 — Web Application Development with Microsoft Technologies
> Due: Nov 20, 2026

## Architecture

Each **municipality is a tenant**, resolved from the URL (`/burnaby`, `/surrey`, `/richmond`).
Tenant data isolation is enforced in the Blazor apps, the WebAPI, **and** the EF Core queries.

| Project | Type | Responsibility |
|---|---|---|
| `MEMS` | class library | Core domain models + business logic |
| `MEMS.Data` | class library | EF Core `DbContext`, migrations, seed, SQL Server |
| `MEMS.Shared` | class library | DTOs shared between the API and Blazor WASM |
| `MEMS.Api` | ASP.NET WebAPI | Tenant-scoped REST API (`/api/{tenant}/...`) + Swagger |
| `MEMS.Admin` | Blazor **Server** | Admin site — Identity login, CRUD, dashboard, tenant management |
| `MEMS.Public` | Blazor **WebAssembly** | Public per-tenant site — API-only, Session-Storage voting guide |
| `MEMS.AppHost` | .NET Aspire | Orchestrates SQL Server + API + Admin + Public |
| `MEMS.ServiceDefaults` | .NET Aspire | Shared telemetry, health checks, service discovery |

## Tech stack

- .NET 10 (`net10.0`)
- .NET Aspire 13.6 orchestration
- Entity Framework Core 10 — **SQL Server**, Code First, migrations
- ASP.NET Core Identity (Super Admin + per-tenant Municipality Admins)
- Bootstrap 5

## Prerequisites

- .NET 10 SDK (10.0.401+)
- **Docker Desktop running** (Aspire starts SQL Server in a container — `mcr.microsoft.com/azure-sql-edge`)
- `dotnet-ef` tool: `dotnet tool install -g dotnet-ef`
- (optional) Aspire CLI: https://aspire.dev/get-started/install-cli

## Run

```bash
# from the solution root (Docker must be running)
aspire run            # or: dotnet run --project MEMS.AppHost
```

The Aspire dashboard opens automatically and launches every service.

## Repository conventions

- NuGet versions are set per project in each `.csproj`.
- Build artifacts (`bin/`, `obj/`) are git-ignored; `wwwroot/lib/` (Bootstrap) is committed.
- `.github/copilot-instructions.md` — GitHub Copilot guidance for this repo.
