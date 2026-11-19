---
description: Audit Admin pages, API controllers and EF queries for cross-tenant access leaks
agent: ask
---

Review tenant isolation in all three layers.

**EF Core (`MEMS.Data`)**

1. Every tenant-owned entity (`Position`, `Candidate`, `StoredImage`) has a global query filter on `MunicipalityId`
   driven by `ITenantContext`.
2. No service uses `IgnoreQueryFilters()` except where a Super Admin explicitly needs all tenants.
3. Create/update methods set `MunicipalityId` from `ITenantContext`, not from the incoming model, for Municipality Admins.

**Admin (`MEMS.Admin`)**

4. Every page has `[Authorize]`; tenant create/delete pages require `Super Admin`.
5. Detail/edit/delete pages for another tenant's record show not-found/forbidden.
6. Dropdowns only list the admin's own tenant's positions.

**API (`MEMS.Api`)**

7. Write endpoints require the JWT scheme; the `{tenant}` route slug is compared to the token's `tenant_id`
   claim (Super Admin exempt) before any work is done.
8. GET by id returns 404 when the record belongs to a different tenant than the route slug.

Walk through these attacks and say whether each is blocked and where:

- `burnabyadmin` token → `GET /api/surrey/candidates` and `PUT /api/surrey/candidates/{id}`
- `burnabyadmin` token → `PUT /api/burnaby/candidates/{surreyCandidateId}`
- `burnabyadmin` creates a candidate with a Surrey `PositionId` from the Admin form (edited in dev tools)
- Anonymous `GET /api/burnaby/candidates/{surreyCandidateId}`

List problems as `file:line – problem – fix`. Don't change code unless asked.
