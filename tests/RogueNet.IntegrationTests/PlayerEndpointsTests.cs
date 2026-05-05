using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RogueNet.Api.Contracts;
using RogueNet.Infrastructure.Data;

namespace RogueNet.IntegrationTests;

public class PlayerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlayerEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

                // Remove DbContextOptions
                var dbContextOptionsDescriptor = services.Where(
                    d => d.ServiceType.IsGenericType &&
                         d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
                    .ToList();
                foreach (var descriptor in dbContextOptionsDescriptor)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        });
    }

    [Fact]
    public async Task CreatePlayer_WithValidUsername_ReturnsCreatedPlayer()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreatePlayerRequest("TestPlayer123");

        // Act
        var response = await client.PostAsJsonAsync("/players", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var player = await response.Content.ReadFromJsonAsync<PlayerResponse>();
        player.Should().NotBeNull();
        player!.Username.Should().Be("TestPlayer123");
        player.Id.Should().NotBe(Guid.Empty);
        player.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/players/{player.Id}/profile");
    }

    [Fact]
    public async Task CreatePlayer_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreatePlayerRequest("DuplicateUser");

        // Act - Create first player
        await client.PostAsJsonAsync("/players", request);

        // Act - Try to create duplicate
        var response = await client.PostAsJsonAsync("/players", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatePlayer_WithEmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreatePlayerRequest("");

        // Act
        var response = await client.PostAsJsonAsync("/players", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPlayerProfile_WithExistingPlayer_ReturnsProfile()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new CreatePlayerRequest("ProfileTestUser");

        // Create player first
        var createResponse = await client.PostAsJsonAsync("/players", createRequest);
        var createdPlayer = await createResponse.Content.ReadFromJsonAsync<PlayerResponse>();

        // Act
        var response = await client.GetAsync($"/players/{createdPlayer!.Id}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Username.Should().Be("ProfileTestUser");
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
        // Arrange
        var client = _factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/players/{nonExistentId}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePlayer_ThenGetProfile_ReturnsConsistentData()
    {
        // Arrange
        var client = _factory.CreateClient();
        var username = "EndToEndTestUser";

        // Act - Create player
        var createResponse = await client.PostAsJsonAsync("/players", new CreatePlayerRequest(username));
        var createdPlayer = await createResponse.Content.ReadFromJsonAsync<PlayerResponse>();

        // Act - Retrieve profile
        var profileResponse = await client.GetAsync($"/players/{createdPlayer!.Id}/profile");
        var profile = await profileResponse.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        // Assert - Data is consistent
        profile!.PlayerId.Should().Be(createdPlayer.Id);
        profile.Username.Should().Be(username);
    }
}
