using MEMS.Models;
using MEMS.Shared.Dtos;

namespace MEMS.Api.Infrastructure;

/// <summary>Entity → DTO. The API never returns EF entities.</summary>
internal static class DtoMapper
{
    public static string ImageUrl(string slug, int imageId) => $"/api/{slug}/images/{imageId}";

    public static MunicipalityDto ToDto(this Municipality m) => new(
        m.MunicipalityId, m.Name, m.Slug, m.Province, m.ElectionDate, m.Description, m.ThemeColor,
        m.LogoImageId is int logoId ? ImageUrl(m.Slug, logoId) : null);

    public static PositionDto ToDto(this Position p) => new(
        p.PositionId, p.Name, p.SeatsAvailable, p.MunicipalityId, p.Candidates.Count);

    public static CandidateDto ToDto(this Candidate c, string slug) => new(
        // Stored as UTC; SQL Server's datetime2 loses the "kind", so mark it before serializing (adds the "Z").
        c.CandidateId, c.Name, c.Profile, c.Website, c.Email, DateTime.SpecifyKind(c.DateCreated, DateTimeKind.Utc), c.PositionId,
        c.Position?.Name ?? string.Empty, c.MunicipalityId,
        c.PhotoImageId is int photoId ? ImageUrl(slug, photoId) : null);

    public static Municipality ToEntity(this MunicipalityRequest r) => new()
    {
        Name = r.Name, Slug = r.Slug, Province = r.Province, ElectionDate = r.ElectionDate,
        Description = r.Description, ThemeColor = r.ThemeColor,
    };

    public static Position ToEntity(this PositionRequest r) => new() { Name = r.Name, SeatsAvailable = r.SeatsAvailable };

    public static Candidate ToEntity(this CandidateRequest r) => new()
    {
        Name = r.Name, Profile = r.Profile, Website = r.Website, Email = r.Email, PositionId = r.PositionId,
    };
}
