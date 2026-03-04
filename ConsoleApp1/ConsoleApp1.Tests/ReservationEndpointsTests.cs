using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConsoleApp1.Tests;

public class ReservationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ReservationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateReservation_ReturnsCreated_WhenAvailabilityExists()
    {
        await TestAuthHelper.AuthorizeAsync(_client, "hotel.full");

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var availabilityResponse = await _client.GetAsync($"/api/v1/reservations/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}");
        availabilityResponse.EnsureSuccessStatusCode();

        var availability = await availabilityResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(availability.ValueKind == JsonValueKind.Array);
        Assert.True(availability.GetArrayLength() > 0);

        var roomId = availability[0].GetProperty("roomId").GetGuid();

        var request = new
        {
            roomId,
            guestFullName = "Teste Integração",
            checkIn,
            checkOut,
            earlyCheckInRequested = false,
            sourceChannel = "direct"
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservations")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var createResponse = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    }
}
