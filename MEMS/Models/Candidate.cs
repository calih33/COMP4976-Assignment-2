using System.ComponentModel.DataAnnotations;

namespace MEMS.Models;

public class Candidate
{
    public const int ProfileMaxLength = 2000;

    public int CandidateId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Candidate Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(ProfileMaxLength)]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Biography")]
    public string Profile { get; set; } = string.Empty;

    [StringLength(255)]
    [Display(Name = "Photo")]
    public string? PhotoFileName { get; set; }

    [Url]
    [StringLength(200)]
    [Display(Name = "Website URL")]
    public string? Website { get; set; }

    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "Email Address")]
    public string? Email { get; set; }

    [Display(Name = "Date Created")]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "The Municipality field is required.")]
    [Display(Name = "Municipality")]
    public int? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }

    [Required(ErrorMessage = "The Position field is required.")]
    [Display(Name = "Position")]
    public int? PositionId { get; set; }
    public Position? Position { get; set; }
}
