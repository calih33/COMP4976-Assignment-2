---
name: tenant-api
description: How MEMS.Api controllers are built - tenant slug in every route, public GETs, JWT-protected writes checked against the token's tenant, DTOs from MEMS.Shared, ProblemDetails errors and Swagger docs. Use when adding or changing anything in MEMS.Api.
---

# Tenant-scoped WebAPI

Replaces A1's `mobile-api` skill. The API serves the Blazor WASM public site and future clients.

## Routes

`[Route("api/{tenant}/candidates")]` — every tenant-owned resource includes the slug. A resource filter resolves
the slug (case-insensitive) to a `MunicipalityId` and sets `ITenantContext`; an unknown slug returns 404
ProblemDetails before the action runs.

| Resource | Endpoints |
|---|---|
| Municipality | `GET api/municipalities` (public list for the picker), `POST api/municipalities` / `DELETE api/municipalities/{id}` (Super Admin), `GET/PUT api/{tenant}/municipality` |
| Positions | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` under `api/{tenant}/positions` |
| Candidates | same, under `api/{tenant}/candidates`; `GET` supports `?search=&positionId=` |
| Images | `GET api/{tenant}/images/{id}` |
| Auth | `POST api/auth/login` → JWT |

## Authentication / authorization

- GETs are anonymous.
- Writes: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
- The JWT (`JwtTokenService`, ported from A1) carries `sub`, `name`, `role`, and `tenant_id` (absent for Super Admin).
- Before any write: if the user is not Super Admin and `tenant_id` ≠ the route tenant → `403` ProblemDetails.
- `MapInboundClaims = false`; `Jwt:Key` ≥ 32 chars, supplied via Aspire parameter / user secrets, never committed.

## Controller rules

- `[ApiController]`, return `ActionResult<TDto>`; DTOs live in `MEMS.Shared` with validation attributes.
  Never return EF entities.
- Call services from `MEMS.Data`; no `AppDbContext` in controllers.
- Errors: `Problem(title: "...", statusCode: 404)`; field errors via `ValidationProblem(ModelState)`.
- GET by id for another tenant's record → 404 (same as missing).
- Document every action with XML comments and `[ProducesResponseType]` (200/201/204/400/401/403/404) so Swagger
  shows request, response and error examples. Keep `MEMS.Api.http` in sync.
- Global exception handling: `AddProblemDetails()` + `UseExceptionHandler()`; no stack traces in responses.

## Verify

With a `burnabyadmin` token: `GET /api/burnaby/candidates` 200, `PUT /api/surrey/candidates/{id}` 403,
`PUT /api/burnaby/candidates/{surreyId}` 404. Run the `/review-authorization` prompt.
