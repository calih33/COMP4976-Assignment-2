---
description: Generate tenant-scoped Blazor Server admin CRUD pages for an entity
agent: agent
---

Create full admin CRUD for the `${input:entity:Entity name, e.g. Position}` entity in `MEMS.Admin`.

Follow the `scoped-admin-crud` skill in `.github/skills/scoped-admin-crud/SKILL.md`, the `razor-components`
instructions, and the repository instructions.

Deliverables:

1. `MEMS.Admin/Components/Pages/${input:entity}s/` with `Index.razor` (QuickGrid), `Details.razor`, `Create.razor`,
   `Edit.razor` and `Delete.razor`.
2. All data access through the matching service in `MEMS.Data/Services` (never `AppDbContext` directly in a component).
3. Dropdowns built only from the current admin's tenant (all tenants for Super Admin).
4. Image upload with `InputFile` if the entity has an image (see the `image-upload` skill).
5. A link in `MEMS.Admin/Components/Layout/NavMenu.razor`.

Before finishing, check:

- Every service method filters by `ITenantContext` and re-checks the tenant on create/update/delete.
- A Municipality Admin opening `/admin/${input:entity}s/edit/{id}` for another tenant's record gets "not found / forbidden".
- `dotnet build` succeeds with no new warnings.
