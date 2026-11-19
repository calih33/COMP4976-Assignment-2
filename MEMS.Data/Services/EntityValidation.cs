using System.ComponentModel.DataAnnotations;
using MEMS.Images;
using MEMS.Models;

namespace MEMS.Data.Services;

/// <summary>
/// Server-side re-validation of data annotations, so the rules hold even if a caller skips UI/API validation.
/// </summary>
internal static class EntityValidation
{
    public static Dictionary<string, string[]> Validate(object entity)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(entity, new ValidationContext(entity), results, validateAllProperties: true);

        return results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [string.Empty]).Select(m => (Member: m, r.ErrorMessage)))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray());
    }

    public static StoredImage ToStoredImage(ImageUpload upload) => new()
    {
        // Trust the file's bytes, not the content type the browser sent.
        ContentType = ImageRules.DetectContentType(upload.Data)!,
        Data = upload.Data,
    };

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
