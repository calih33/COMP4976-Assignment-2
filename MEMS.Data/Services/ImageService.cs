using MEMS.Models;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

public interface IImageService
{
    /// <summary>
    /// The image if it is the logo or a candidate photo of a tenant the caller can see; otherwise null.
    /// </summary>
    Task<StoredImage?> GetAsync(int id, CancellationToken ct = default);
}

public sealed class ImageService(AppDbContextFactory factory) : IImageService
{
    public async Task<StoredImage?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Create();

        // Both checks go through the tenant query filters.
        var visible = await db.Municipalities.AnyAsync(m => m.LogoImageId == id, ct)
            || await db.Candidates.AnyAsync(c => c.PhotoImageId == id, ct);

        return visible
            ? await db.StoredImages.AsNoTracking().FirstOrDefaultAsync(i => i.StoredImageId == id, ct)
            : null;
    }
}
