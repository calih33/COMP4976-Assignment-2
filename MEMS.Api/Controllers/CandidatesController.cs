using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using MEMS.Images;
using MEMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Candidates of one municipality. Only that tenant's candidates are ever returned.</summary>
[Route("api/{tenant}/candidates")]
public sealed class CandidatesController(ICandidateService candidates) : ApiControllerBase
{
    /// <summary>Lists the municipality's candidates, optionally searched by name and filtered by position.</summary>
    /// <param name="tenant">Municipality slug, e.g. <c>burnaby</c>.</param>
    /// <param name="search">Part of a name, case-insensitive (e.g. <c>chen</c>).</param>
    /// <param name="positionId">Only candidates for this position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Candidates ordered by position, then name.</response>
    /// <response code="403">Signed in as another municipality's admin.</response>
    /// <response code="404">Unknown municipality.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<CandidateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IEnumerable<CandidateDto>> GetAll(string tenant, string? search, int? positionId, CancellationToken ct) =>
        (await candidates.ListAsync(search, positionId, ct)).Select(c => c.ToDto(TenantSlug));

    /// <summary>Gets one candidate.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Candidate id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The candidate.</response>
    /// <response code="404">Unknown municipality, or the candidate isn't in this municipality.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> GetById(string tenant, int id, CancellationToken ct) =>
        await candidates.GetAsync(id, ct) is { } c ? c.ToDto(TenantSlug) : Failure(ServiceResult.NotFound());

    /// <summary>Registers a candidate for a position in this municipality.</summary>
    /// <remarks>Example: <c>{ "name": "Alex Rivera", "profile": "Long-time resident...", "website": "https://example.com/alex", "email": "alex@example.com", "positionId": 2 }</c></remarks>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="request">Candidate details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Created.</response>
    /// <response code="400">Validation failed, or the position isn't in this municipality.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CandidateDto>> Create(string tenant, CandidateRequest request, CancellationToken ct)
    {
        var result = await candidates.CreateAsync(request.ToEntity(), photo: null, ct);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        var created = await candidates.GetAsync(result.Value!.CandidateId, ct);
        return CreatedAtAction(nameof(GetById), new { tenant = TenantSlug, id = created!.CandidateId }, created.ToDto(TenantSlug));
    }

    /// <summary>Updates a candidate. The photo is kept; use the photo endpoints to change it.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Candidate id.</param>
    /// <param name="request">New candidate details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The updated candidate.</response>
    /// <response code="400">Validation failed, or the position isn't in this municipality.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    /// <response code="404">The candidate isn't in this municipality.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> Update(string tenant, int id, CandidateRequest request, CancellationToken ct)
    {
        var result = await candidates.UpdateAsync(id, request.ToEntity(), photo: null, removePhoto: false, ct);
        return result.Succeeded ? (await candidates.GetAsync(id, ct))!.ToDto(TenantSlug) : Failure(result);
    }

    /// <summary>Deletes a candidate and their photo.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Candidate id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    /// <response code="404">The candidate isn't in this municipality.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(string tenant, int id, CancellationToken ct)
    {
        var result = await candidates.DeleteAsync(id, ct);
        return result.Succeeded ? NoContent() : Failure(result);
    }

    /// <summary>Uploads or replaces the candidate's photo (multipart field <c>file</c>; .jpg/.jpeg/.png/.webp, max 2 MB).</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Candidate id.</param>
    /// <param name="file">The image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The candidate with its new <c>photoUrl</c>.</response>
    /// <response code="400">Missing file, wrong type, too large, or not a real image.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    /// <response code="404">The candidate isn't in this municipality.</response>
    [HttpPut("{id:int}/photo")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDto>> UploadPhoto(string tenant, int id, IFormFile? file, CancellationToken ct)
    {
        var (upload, error) = await ReadImageAsync(file, ct);
        return error is not null ? ImageError(error) : await ChangePhotoAsync(id, upload, remove: false, ct);
    }

    /// <summary>Removes the candidate's photo.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Candidate id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The candidate without a photo.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    /// <response code="404">The candidate isn't in this municipality.</response>
    [HttpDelete("{id:int}/photo")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<CandidateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<CandidateDto>> DeletePhoto(string tenant, int id, CancellationToken ct) =>
        ChangePhotoAsync(id, null, remove: true, ct);

    private async Task<ActionResult<CandidateDto>> ChangePhotoAsync(int id, ImageUpload? photo, bool remove, CancellationToken ct)
    {
        var existing = await candidates.GetAsync(id, ct);
        if (existing is null)
        {
            return Failure(ServiceResult.NotFound());
        }

        var result = await candidates.UpdateAsync(id, existing, photo, remove, ct);
        return result.Succeeded ? (await candidates.GetAsync(id, ct))!.ToDto(TenantSlug) : Failure(result);
    }
}
