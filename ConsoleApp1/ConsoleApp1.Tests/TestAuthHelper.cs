using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConsoleApp1.Tests;

internal static class TestAuthHelper
{
    public static async Task<string> GetAccessTokenAsync(HttpClient client, string scope = "hotel.full")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", new
        {
            clientId = "hotel-ops",
            clientSecret = "hotel-ops-secret",
            scope
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    public static async Task AuthorizeAsync(HttpClient client, string scope = "hotel.full")
    {
        var token = await GetAccessTokenAsync(client, scope);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
