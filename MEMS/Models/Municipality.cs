using System.ComponentModel.DataAnnotations;

namespace MEMS.Models;

/// <summary>
/// A municipality is a tenant. Its <see cref="Slug"/> identifies it in URLs (<c>/burnaby</c>, <c>/api/burnaby/...</c>).
/// </summary>
public class Municipality
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public int MunicipalityId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(SlugPattern, ErrorMessage = "Use lowercase letters, numbers and single hyphens only (e.g. \"pitt-meadows\").")]
    [Display(Name = "URL Slug")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Province { get; set; } = "British Columbia";

    [Required]
    [Display(Name = "Election Date")]
    public DateOnly ElectionDate { get; set; } = new(2026, 10, 17);

    [StringLength(1000)]
    public string? Description { get; set; }

    // Per-tenant accent colour for the public site.
    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Use a hex colour such as #1f6f8b.")]
    [Display(Name = "Theme Colour")]
    public string ThemeColor { get; set; } = "#1f6f8b";

    [Display(Name = "Logo")]
    public int? LogoImageId { get; set; }

    public StoredImage? LogoImage { get; set; }

    public ICollection<Position> Positions { get; set; } = new List<Position>();

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}
