using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConsoleApp1.Tests;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IssueToken_ReturnsAccessAndRefreshToken_WhenCredentialsAreValid()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/token", new
        {
            clientId = "hotel-ops",
            clientSecret = "hotel-ops-secret",
            scope = "hotel.full"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("accessToken", out var accessToken));
        Assert.False(string.IsNullOrWhiteSpace(accessToken.GetString()));
        Assert.True(payload.TryGetProperty("refreshToken", out var refreshToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken.GetString()));
    }
}
