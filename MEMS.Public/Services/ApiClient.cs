using System.Net.Http.Json;
using MEMS.Shared.Dtos;

namespace MEMS.Public.Services;

public sealed class ApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<MunicipalityDto>> GetMunicipalitiesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<MunicipalityDto>>("api/municipalities", ct) ?? [];

    public async Task<MunicipalityDto?> GetMunicipalityAsync(string slug, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/municipalities/{Uri.EscapeDataString(slug)}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MunicipalityDto>(cancellationToken: ct)
            : null;
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string tenant, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PositionDto>>($"api/{tenant}/positions", ct) ?? [];

    public async Task<IReadOnlyList<CandidateDto>> GetCandidatesAsync(string tenant, string? search = null, int? positionId = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (positionId is not null)
        {
            query.Add($"positionId={positionId}");
        }

        var url = $"api/{tenant}/candidates" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await http.GetFromJsonAsync<List<CandidateDto>>(url, ct) ?? [];
    }

    public async Task<CandidateDto?> GetCandidateAsync(string tenant, int id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/{tenant}/candidates/{id}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CandidateDto>(cancellationToken: ct)
            : null;
    }
}
