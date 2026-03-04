using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConsoleApp1.Tests;

public class AuditEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SecurityAuditLogs_ReturnsPagedItems_WhenAuthorized()
    {
        await TestAuthHelper.AuthorizeAsync(_client, "hotel.full");

        var response = await _client.GetAsync("/api/v1/admin/security-audit-logs?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }
}
