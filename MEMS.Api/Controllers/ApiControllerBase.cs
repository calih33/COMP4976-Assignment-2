using MEMS.Api.Tenancy;
using MEMS.Data.Services;
using MEMS.Images;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MEMS.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Canonical slug of the tenant resolved by <see cref="TenantContextFilter"/>.</summary>
    protected string TenantSlug => HttpContext.Items[TenantContextFilter.SlugItemKey] as string ?? string.Empty;

    /// <summary>Maps a failed service result to 404 / 403 / 400 ProblemDetails.</summary>
    protected ActionResult Failure(ServiceResult result)
    {
        switch (result.Status)
        {
            case ServiceStatus.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found.",
                    detail: "The record doesn't exist in this municipality.");
            case ServiceStatus.Forbidden:
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not allowed.",
                    detail: "Your account can't make this change.");
            case ServiceStatus.Invalid:
                var modelState = new ModelStateDictionary();
                foreach (var (field, messages) in result.Errors)
                {
                    foreach (var message in messages)
                    {
                        modelState.AddModelError(field, message);
                    }
                }

                return ValidationProblem(modelState);
            default:
                throw new InvalidOperationException($"Unexpected service status {result.Status}.");
        }
    }

    /// <summary>Reads an uploaded image into memory, rejecting anything over the size limit before buffering it.</summary>
    protected static async Task<(ImageUpload? Upload, string? Error)> ReadImageAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            return (null, "Choose an image file.");
        }

        var error = ImageRules.Validate(file.FileName, file.ContentType, file.Length);
        if (error is not null)
        {
            return (null, error);
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        return (new ImageUpload(file.FileName, file.ContentType, memory.ToArray()), null);
    }

    protected ActionResult ImageError(string message) => Failure(ServiceResult.Invalid("file", message));
}
