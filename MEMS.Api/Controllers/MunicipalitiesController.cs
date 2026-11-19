using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using MEMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Municipalities (tenants). Reading is public; creating and deleting tenants is Super Admin only.</summary>
[Route("api/municipalities")]
public sealed class MunicipalitiesController(IMunicipalityService municipalities, ITenantContext tenant) : ApiControllerBase
{
    /// <summary>Lists every municipality (used by the public site's municipality picker).</summary>
    /// <response code="200">All municipalities, ordered by name.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<MunicipalityDto>>(StatusCodes.Status200OK)]
    public async Task<IEnumerable<MunicipalityDto>> GetAll(CancellationToken ct) =>
        (await municipalities.ListAllTenantsAsync(ct)).Select(m => m.ToDto());

    /// <summary>Gets one municipality by id.</summary>
    /// <response code="200">The municipality.</response>
    /// <response code="404">No municipality with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> GetById(int id, CancellationToken ct) =>
        await municipalities.FindByIdAsync(id, ct) is { } m ? m.ToDto() : Failure(ServiceResult.NotFound());

    /// <summary>Gets one municipality by its URL slug (case-insensitive), e.g. <c>burnaby</c>.</summary>
    /// <response code="200">The municipality.</response>
    /// <response code="404">No municipality uses this slug.</response>
    [HttpGet("{slug}")]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> GetBySlug(string slug, CancellationToken ct) =>
        await municipalities.FindBySlugAsync(slug, ct) is { } m ? m.ToDto() : Failure(ServiceResult.NotFound());

    /// <summary>Creates a municipality (tenant). Super Admin only.</summary>
    /// <remarks>Example: <c>{ "name": "Coquitlam", "slug": "coquitlam", "province": "British Columbia", "electionDate": "2026-10-17", "description": "...", "themeColor": "#336699" }</c></remarks>
    /// <response code="201">Created; <c>Location</c> points at the new municipality.</response>
    /// <response code="400">Validation failed (e.g. slug format, name or slug already used).</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Signed in, but not a Super Admin.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MunicipalityDto>> Create(MunicipalityRequest request, CancellationToken ct)
    {
        var result = await municipalities.CreateAsync(request.ToEntity(), logo: null, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.MunicipalityId }, result.Value.ToDto())
            : Failure(result);
    }

    /// <summary>Updates a municipality. Municipality Admins can update only their own, and can't change its slug.</summary>
    /// <response code="200">The updated municipality.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Another tenant's municipality.</response>
    /// <response code="404">No municipality with this id.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> Update(int id, MunicipalityRequest request, CancellationToken ct)
    {
        if (!OwnTenantOrSuperAdmin(id))
        {
            return Failure(ServiceResult.Forbidden());
        }

        var result = await municipalities.UpdateAsync(id, request.ToEntity(), logo: null, removeLogo: false, ct);
        return result.Succeeded ? result.Value!.ToDto() : Failure(result);
    }

    /// <summary>Deletes a municipality with all its positions, candidates, images and admin accounts. Super Admin only.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Not a Super Admin.</response>
    /// <response code="404">No municipality with this id.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await municipalities.DeleteAsync(id, ct);
        return result.Succeeded ? NoContent() : Failure(result);
    }

    /// <summary>Uploads or replaces the logo (multipart field <c>file</c>; .jpg/.jpeg/.png/.webp, max 2 MB).</summary>
    /// <response code="200">The municipality with its new <c>logoUrl</c>.</response>
    /// <response code="400">Missing file, wrong type, too large, or not a real image.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Another tenant's municipality.</response>
    /// <response code="404">No municipality with this id.</response>
    [HttpPut("{id:int}/logo")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> UploadLogo(int id, IFormFile? file, CancellationToken ct)
    {
        if (!OwnTenantOrSuperAdmin(id))
        {
            return Failure(ServiceResult.Forbidden());
        }

        var (upload, error) = await ReadImageAsync(file, ct);
        if (error is not null)
        {
            return ImageError(error);
        }

        return await ChangeLogoAsync(id, upload, remove: false, ct);
    }

    /// <summary>Removes the logo.</summary>
    /// <response code="200">The municipality without a logo.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Another tenant's municipality.</response>
    /// <response code="404">No municipality with this id.</response>
    [HttpDelete("{id:int}/logo")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<MunicipalityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MunicipalityDto>> DeleteLogo(int id, CancellationToken ct) =>
        OwnTenantOrSuperAdmin(id) ? await ChangeLogoAsync(id, null, remove: true, ct) : Failure(ServiceResult.Forbidden());

    private bool OwnTenantOrSuperAdmin(int municipalityId) =>
        tenant.IsSuperAdmin || tenant.MunicipalityId == municipalityId;

    private async Task<ActionResult<MunicipalityDto>> ChangeLogoAsync(int id, MEMS.Images.ImageUpload? logo, bool remove, CancellationToken ct)
    {
        var existing = await municipalities.GetAsync(id, ct);
        if (existing is null)
        {
            return Failure(ServiceResult.NotFound());
        }

        var result = await municipalities.UpdateAsync(id, existing, logo, remove, ct);
        return result.Succeeded ? result.Value!.ToDto() : Failure(result);
    }
}
