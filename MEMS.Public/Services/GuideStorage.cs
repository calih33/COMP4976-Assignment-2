using System.Text.Json;
using Microsoft.JSInterop;

namespace MEMS.Public.Services;

public sealed class GuideStorage(IJSRuntime js)
{
    private static string Key(string slug) => $"mems-guide:{slug.ToLowerInvariant()}";

    public async Task<IReadOnlyList<int>> GetIdsAsync(string slug)
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", Key(slug));
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task AddAsync(string slug, int candidateId)
    {
        var ids = (await GetIdsAsync(slug)).ToList();
        if (!ids.Contains(candidateId))
        {
            ids.Add(candidateId);
            await SaveAsync(slug, ids);
        }
    }

    public async Task RemoveAsync(string slug, int candidateId)
    {
        var ids = (await GetIdsAsync(slug)).ToList();
        if (ids.Remove(candidateId))
        {
            await SaveAsync(slug, ids);
        }
    }

    public async Task ClearAsync(string slug) =>
        await js.InvokeVoidAsync("sessionStorage.removeItem", Key(slug));

    private async Task SaveAsync(string slug, List<int> ids) =>
        await js.InvokeVoidAsync("sessionStorage.setItem", Key(slug), JsonSerializer.Serialize(ids));
}
