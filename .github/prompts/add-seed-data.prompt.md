---
description: Add a seeded municipality tenant with positions, fictional candidates and its admin user
agent: agent
---

Add seed data for the tenant `${input:municipality:Municipality name, e.g. Coquitlam}` in `MEMS.Data/Seed/SeedData.cs`.

- Slug: lowercase, letters/digits/hyphens only (e.g. `coquitlam`). It must be unique.
- Election date October 17, 2026, province British Columbia, a short description, and a theme colour.
- Positions: Mayor (1 seat), Councillor and School Trustee with realistic seat counts.
- 2–3 candidates per position. Each candidate's `MunicipalityId` must equal its position's `MunicipalityId`.
  **Candidates must be fictional** – never use names of real politicians or real people.
- Use `@example.com` emails and `https://example.com/...` websites only.
- Create the admin user `${input:municipality}admin` (lowercase) with password `P@$$w0rd`, role `Municipality Admin`,
  and `MunicipalityId` set to the new tenant.
- Keep seeding idempotent: look tenants up by slug and users by user name before inserting.
