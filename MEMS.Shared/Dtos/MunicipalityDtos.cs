using System.ComponentModel.DataAnnotations;

namespace MEMS.Shared.Dtos;

/// <summary>A municipality (tenant) as returned by the API.</summary>
/// <param name="Id">Municipality id.</param>
/// <param name="Name">Display name, e.g. "Burnaby".</param>
/// <param name="Slug">URL segment that identifies the tenant, e.g. "burnaby".</param>
/// <param name="Province">Province, e.g. "British Columbia".</param>
/// <param name="ElectionDate">Election day (yyyy-MM-dd).</param>
/// <param name="Description">Short description shown on the tenant home page.</param>
/// <param name="ThemeColor">Accent colour for the tenant's public site (#rrggbb).</param>
/// <param name="LogoUrl">Relative URL of the logo, or null when there is none.</param>
public sealed record MunicipalityDto(
    int Id,
    string Name,
    string Slug,
    string Province,
    DateOnly ElectionDate,
    string? Description,
    string ThemeColor,
    string? LogoUrl);

/// <summary>Body for creating or updating a municipality.</summary>
public sealed class MunicipalityRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Lowercase letters, numbers and single hyphens. Only a Super Admin can change it.</summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers and single hyphens only (e.g. \"pitt-meadows\").")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Province { get; set; } = "British Columbia";

    [Required]
    public DateOnly ElectionDate { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Use a hex colour such as #1f6f8b.")]
    public string ThemeColor { get; set; } = "#1f6f8b";
}
