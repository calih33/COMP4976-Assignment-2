using MEMS.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Logos and candidate photos, served only within their own municipality.</summary>
[Route("api/{tenant}/images")]
public sealed class ImagesController(IImageService images) : ApiControllerBase
{
    /// <summary>Gets an image (the URLs in <c>logoUrl</c> / <c>photoUrl</c> point here).</summary>
    /// <param name="tenant">Municipality slug.</param>
    /// <param name="id">Image id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The image bytes (image/jpeg, image/png or image/webp).</response>
    /// <response code="404">Unknown municipality, or the image doesn't belong to it.</response>
    [HttpGet("{id:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get(string tenant, int id, CancellationToken ct) =>
        await images.GetAsync(id, ct) is { } image
            ? File(image.Data, image.ContentType)
            : Failure(ServiceResult.NotFound());
}
