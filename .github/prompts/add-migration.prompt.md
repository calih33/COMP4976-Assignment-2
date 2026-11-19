---
description: Make a schema change the Code First way and generate a SQL Server migration
agent: agent
---

Apply this schema change: ${input:change:Describe the change, e.g. add a Twitter URL to Candidate}

Steps:

1. Edit the entity in `MEMS/Models/` (data annotations for validation; no ASP.NET types in this project).
2. Update the entity configuration / `OnModelCreating` in `MEMS.Data/AppDbContext.cs` for keys, indexes,
   max lengths and the tenant query filter if the entity is tenant-owned.
3. Update `MEMS.Data/Seed/SeedData.cs` if seeded rows need the new data. Keep it idempotent.
4. Update the DTOs in `MEMS.Shared` and the mapping in the API if the field is exposed.
5. Run:
   `dotnet ef migrations add ${input:name:PascalCase migration name} --project MEMS.Data --startup-project MEMS.Api -o Migrations`
6. Read the generated migration and confirm it only contains the intended change (no dropped tables or data loss).
7. Run the AppHost once so the API applies the migration at startup, and confirm it starts without errors.

Never edit migrations that are already pushed; add a new one instead. SQLite is not allowed.
