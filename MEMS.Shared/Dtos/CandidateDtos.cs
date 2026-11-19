using System.ComponentModel.DataAnnotations;

namespace MEMS.Shared.Dtos;

/// <summary>A candidate running for a position in one municipality.</summary>
/// <param name="Id">Candidate id.</param>
/// <param name="Name">Candidate's full name.</param>
/// <param name="Profile">Biography (max 2000 characters).</param>
/// <param name="Website">Campaign website, or null.</param>
/// <param name="Email">Contact email, or null.</param>
/// <param name="DateCreated">When the candidate was registered (UTC).</param>
/// <param name="PositionId">Position the candidate is running for.</param>
/// <param name="PositionName">Name of that position.</param>
/// <param name="MunicipalityId">Owning municipality (tenant).</param>
/// <param name="PhotoUrl">Relative URL of the photo, or null when there is none.</param>
public sealed record CandidateDto(
    int Id,
    string Name,
    string Profile,
    string? Website,
    string? Email,
    DateTime DateCreated,
    int PositionId,
    string PositionName,
    int MunicipalityId,
    string? PhotoUrl);

/// <summary>Body for creating or updating a candidate. The municipality comes from the position.</summary>
public sealed class CandidateRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Profile { get; set; } = string.Empty;

    [Url]
    [StringLength(200)]
    public string? Website { get; set; }

    [EmailAddress]
    [StringLength(100)]
    public string? Email { get; set; }

    /// <summary>Must be a position in the same municipality as the URL.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "The Position field is required.")]
    public int PositionId { get; set; }
}
