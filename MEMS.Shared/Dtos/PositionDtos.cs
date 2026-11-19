using System.ComponentModel.DataAnnotations;

namespace MEMS.Shared.Dtos;

/// <summary>An elected position within one municipality.</summary>
/// <param name="Id">Position id.</param>
/// <param name="Name">e.g. "Mayor", "Councillor", "School Trustee".</param>
/// <param name="SeatsAvailable">Number of seats to be elected (1–50).</param>
/// <param name="MunicipalityId">Owning municipality (tenant).</param>
/// <param name="CandidateCount">Number of candidates running for this position.</param>
public sealed record PositionDto(
    int Id,
    string Name,
    int SeatsAvailable,
    int MunicipalityId,
    int CandidateCount);

/// <summary>Body for creating or updating a position. The municipality comes from the URL.</summary>
public sealed class PositionRequest
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 50)]
    public int SeatsAvailable { get; set; } = 1;
}
