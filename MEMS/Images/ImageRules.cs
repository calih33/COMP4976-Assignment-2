namespace MEMS.Images;

/// <summary>
/// Upload restrictions for logos and candidate photos (ported from A1's ImageStorageService.CheckImage).
/// <see cref="Validate(string,string,long)"/> runs before upload (Blazor) and again on the server;
/// <see cref="Validate(ImageUpload)"/> also checks the file's actual bytes.
/// </summary>
public static class ImageRules
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public const string Accept = ".jpg,.jpeg,.png,.webp";

    /// <summary>Returns an error message, or null when the file metadata is acceptable.</summary>
    public static string? Validate(string fileName, string contentType, long length)
    {
        if (length == 0)
        {
            return "The image file is empty.";
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "Only .jpg, .jpeg, .png or .webp images are allowed.";
        }

        if (length > MaxBytes)
        {
            return "The image must be 2 MB or smaller.";
        }

        return null;
    }

    /// <summary>Server-side check: metadata plus the file signature, so a renamed file can't pass as an image.</summary>
    public static string? Validate(ImageUpload upload)
    {
        var error = Validate(upload.FileName, upload.ContentType, upload.Data.LongLength);
        if (error is not null)
        {
            return error;
        }

        return DetectContentType(upload.Data) is null
            ? "The file is not a valid JPEG, PNG or WebP image."
            : null;
    }

    /// <summary>Content type from the file's magic bytes, or null if it isn't JPEG/PNG/WebP.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
