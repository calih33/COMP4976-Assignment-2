---
applyTo: "**/*.razor"
---

# Razor component rules (Blazor Server admin + Blazor WASM public)

- Every routable page sets `<PageTitle>`.
- Admin pages: `@rendermode InteractiveServer` and `@attribute [Authorize]` (tenant management pages use
  `[Authorize(Roles = AppRoles.SuperAdmin)]`).
- Public pages take the tenant from the route: `@page "/{Tenant}/candidates"` with `[Parameter] public string Tenant { get; set; }`.
  Never take a municipality ID from the query string.
- Forms: `<EditForm Model="..." OnValidSubmit="...">` + `<DataAnnotationsValidator />` + `<ValidationSummary />`,
  and a `<ValidationMessage For="..." />` under every input.
- File inputs: `<InputFile accept=".jpg,.jpeg,.png,.webp" OnChange="...">`; check size/extension in the handler
  before reading the stream (see the `image-upload` skill).
- Lists in Admin use `QuickGrid` with paging. Public lists use Bootstrap cards.
- Images: `<img src="@url" alt="@name" class="img-fluid">`, falling back to `images/placeholder-*.svg` when there is no image.
- Destructive actions (delete, clear guide) ask for confirmation before running.
- Show user-friendly error messages; never render exception details.
- Load data in `OnInitializedAsync` / `OnParametersSetAsync` (the latter when route parameters such as `Tenant` can change).
