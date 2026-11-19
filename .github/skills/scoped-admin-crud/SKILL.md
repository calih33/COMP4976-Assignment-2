---
name: scoped-admin-crud
description: How to build Blazor Server admin CRUD pages so a Municipality Admin can only see and change their own tenant's data while a Super Admin manages everything. Use when adding or changing anything under MEMS.Admin/Components/Pages or the services in MEMS.Data/Services.
---

# Scoped admin CRUD (Blazor Server)

The marking scheme checks that a Municipality Admin (e.g. `burnabyadmin`, tenant Burnaby) cannot see or change
another tenant's data, and that isolation holds in the Blazor app, the API and EF Core. Ported from A1's
`MunicipalityAccessService` rules.

## Who is the current tenant?

`ITenantContext` (in `MEMS.Data`) answers this for the current request/circuit:

| Member | Meaning |
|---|---|
| `IsSuperAdmin` | User is in the `Super Admin` role → sees all tenants |
| `MunicipalityId` | The admin's tenant (`ApplicationUser.MunicipalityId`); `null` for Super Admin |

In Admin it is built from the signed-in user's claims (`tenant_id` claim added at login). In the API it comes
from the JWT plus the `{tenant}` route slug.

## Layers

1. **EF Core:** `AppDbContext` has a global query filter on `MunicipalityId` for tenant-owned entities, so a
   Municipality Admin's queries only return their own rows even if a page forgets to filter.
2. **Services** (`IMunicipalityService`, `IPositionService`, `ICandidateService`): every create/update/delete
   re-checks the tenant on the **stored** record and on any posted `PositionId`/`MunicipalityId`.
   For Municipality Admins, `MunicipalityId` is set from `ITenantContext`, never from the form.
3. **Pages:** `[Authorize]` on every page; dropdowns only list the admin's tenant; a missing or foreign record
   shows a "not found" message.

## Page pattern

```razor
@page "/positions/edit/{Id:int}"
@attribute [Authorize]
@rendermode InteractiveServer
@inject IPositionService Positions
@inject NavigationManager Nav

@if (_model is null) { <p>Position not found.</p> }
else
{
    <EditForm Model="_model" OnValidSubmit="SaveAsync">
        <DataAnnotationsValidator />
        <ValidationSummary />
        ...
    </EditForm>
}

@code {
    [Parameter] public int Id { get; set; }
    private PositionFormModel? _model;

    protected override async Task OnParametersSetAsync() =>
        _model = await Positions.GetForEditAsync(Id); // null when missing or another tenant's

    private async Task SaveAsync()
    {
        var result = await Positions.UpdateAsync(Id, _model!);
        if (result.Succeeded) Nav.NavigateTo("/positions");
        else _error = result.Error; // shown in an alert
    }
}
```

## Rules per operation

| Operation | Rule |
|---|---|
| List / Dashboard | Rely on the query filter; Super Admin sees totals across all tenants. |
| Details / Edit / Delete (load) | Service returns `null` for missing **or** foreign records → "not found". |
| Create | Municipality Admin: `MunicipalityId` from `ITenantContext`. Candidate: the chosen position must belong to the same tenant; set `Candidate.MunicipalityId = Position.MunicipalityId`. |
| Update | Load stored entity (filtered), check, copy allowed fields onto it. Never attach a posted object. |
| Delete | Load, check, remove; images are deleted with the row (`StoredImage` cascade). |
| Tenants (Municipality create/delete, slug) | `Super Admin` only. Municipality Admins may edit their own tenant's details and logo, not its slug. |

## Verify

Log in as `burnabyadmin` / `P@$$w0rd`: only Burnaby rows appear; opening a Surrey position's edit URL shows
not-found. Log in as `super`: everything is visible. Run the `/review-authorization` prompt.
