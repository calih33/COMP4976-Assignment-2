using MEMS.Api.Infrastructure;
using MEMS.Data.Services;
using MEMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Elected positions of one municipality. Only that tenant's positions are ever returned.</summary>
[Route("api/{tenant}/positions")]
public sealed class PositionsController(IPositionService positions) : ApiControllerBase
{
    /// <summary>Lists the municipality's positions.</summary>
    /// <param name="tenant">Municipality slug, e.g. <c>burnaby</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Positions ordered by name.</response>
    /// <response code="403">Signed in as another municipality's admin.</response>
    /// <response code="404">Unknown municipality.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<PositionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IEnumerable<PositionDto>> GetAll(string tenant, CancellationToken ct) =>
        (await positions.ListAsync(ct)).Select(p => p.ToDto());

    /// <summary>Gets one position.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Position id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The position.</response>
    /// <response code="404">Unknown municipality, or the position isn't in this municipality.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> GetById(string tenant, int id, CancellationToken ct) =>
        await positions.GetAsync(id, ct) is { } p ? p.ToDto() : Failure(ServiceResult.NotFound());

    /// <summary>Creates a position in this municipality.</summary>
    /// <remarks>Example: <c>{ "name": "Councillor", "seatsAvailable": 8 }</c></remarks>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="request">Position name and seats.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Created.</response>
    /// <response code="400">Validation failed (e.g. name already used in this municipality, seats outside 1–50).</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<PositionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PositionDto>> Create(string tenant, PositionRequest request, CancellationToken ct)
    {
        var result = await positions.CreateAsync(request.ToEntity(), ct);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        var created = await positions.GetAsync(result.Value!.PositionId, ct);
        return CreatedAtAction(nameof(GetById), new { tenant = TenantSlug, id = created!.PositionId }, created.ToDto());
    }

    /// <summary>Updates a position's name and seats.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Position id.</param>
    /// <param name="request">New name and seats.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The updated position.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    /// <response code="404">The position isn't in this municipality.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType<PositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> Update(string tenant, int id, PositionRequest request, CancellationToken ct)
    {
        var result = await positions.UpdateAsync(id, request.ToEntity(), ct);
        return result.Succeeded ? (await positions.GetAsync(id, ct))!.ToDto() : Failure(result);
    }

    /// <summary>Deletes a position and its candidates.</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Position id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Token is for another municipality.</response>
    /// <response code="404">The position isn't in this municipality.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AnyAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(string tenant, int id, CancellationToken ct)
    {
        var result = await positions.DeleteAsync(id, ct);
        return result.Succeeded ? NoContent() : Failure(result);
    }
}
