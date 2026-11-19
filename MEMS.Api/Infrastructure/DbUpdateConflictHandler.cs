using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Api.Infrastructure;

/// <summary>
/// Turns database constraint violations that slip past validation (e.g. two requests racing to create the same slug)
/// into a 409 ProblemDetails instead of a 500. Nothing from the SQL error is returned to the client.
/// </summary>
public sealed class DbUpdateConflictHandler(IProblemDetailsService problemDetails, ILogger<DbUpdateConflictHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not DbUpdateException)
        {
            return false;
        }

        logger.LogWarning(exception, "Database update rejected for {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The change conflicts with existing data.",
                Detail = "Another record already uses one of these values, or the record is still in use. Reload and try again.",
            },
        });
    }
}
