using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using RogueNet.Api.Contracts;
using RogueNet.IntegrationTests.Fixtures;

namespace RogueNet.IntegrationTests;

public class PlayerEndpointsTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PlayerEndpointsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePlayer_WithValidUsername_ReturnsCreatedPlayer()
    {
        var client = _fixture.Factory.CreateClient();
        var username = $"TestPlayer_{Guid.NewGuid():N}";
        var request = new CreatePlayerRequest(username);

        var response = await client.PostAsJsonAsync("/players", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var player = await response.Content.ReadFromJsonAsync<PlayerResponse>();
        player.Should().NotBeNull();
        player!.Username.Should().Be(username);
        player.Id.Should().NotBe(Guid.Empty);
        player.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/players/{player.Id}/profile");
    }

    [Fact]
    public async Task CreatePlayer_WithDuplicateUsername_ReturnsConflict()
    {
        var client = _fixture.Factory.CreateClient();
        var username = $"DuplicateUser_{Guid.NewGuid():N}";
        var request = new CreatePlayerRequest(username);

        await client.PostAsJsonAsync("/players", request);
        var response = await client.PostAsJsonAsync("/players", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatePlayer_WithEmptyUsername_ReturnsBadRequest()
    {
        var client = _fixture.Factory.CreateClient();
        var request = new CreatePlayerRequest(string.Empty);

        var response = await client.PostAsJsonAsync("/players", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPlayerProfile_WithExistingPlayer_ReturnsProfile()
    {
        var client = _fixture.Factory.CreateClient();
        var username = $"ProfileTestUser_{Guid.NewGuid():N}";
        var createResponse = await client.PostAsJsonAsync("/players", new CreatePlayerRequest(username));
        var createdPlayer = await createResponse.Content.ReadFromJsonAsync<PlayerResponse>();

        var response = await client.GetAsync($"/players/{createdPlayer!.Id}/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Username.Should().Be(username);
        profile.PlayerId.Should().Be(createdPlayer.Id);
        profile.ExperiencePoints.Should().Be(0);
        profile.Level.Should().Be(1);
        profile.CashBalance.Should().Be(1000m);
        profile.Reputation.Should().Be(0);
        profile.Version.Should().Be(1);
    }

    [Fact]
    public async Task GetPlayerProfile_WithNonExistentPlayer_ReturnsNotFound()
    {
        var client = _fixture.Factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        var response = await client.GetAsync($"/players/{nonExistentId}/profile");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePlayer_ThenGetProfile_ReturnsConsistentData()
    {
        var client = _fixture.Factory.CreateClient();
        var username = $"EndToEndTestUser_{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("/players", new CreatePlayerRequest(username));
        var createdPlayer = await createResponse.Content.ReadFromJsonAsync<PlayerResponse>();

        var profileResponse = await client.GetAsync($"/players/{createdPlayer!.Id}/profile");
        var profile = await profileResponse.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        profile!.PlayerId.Should().Be(createdPlayer.Id);
        profile.Username.Should().Be(username);
    }
}
