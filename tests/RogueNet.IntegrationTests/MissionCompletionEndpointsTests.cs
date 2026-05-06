using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapper;

using FluentAssertions;

using Microsoft.Data.SqlClient;

using RogueNet.Api.Contracts;
using RogueNet.IntegrationTests.Fixtures;

namespace RogueNet.IntegrationTests;

public class MissionCompletionEndpointsTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public MissionCompletionEndpointsTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteMission_FreshRequest_ReturnsCreatedWithGrantedReward()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);
        var request = SampleRequest();

        var response = await client.PostAsJsonAsync(
            $"/players/{playerId}/mission-completions",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/players/{playerId}/mission-completions/");

        var body = await response.Content.ReadFromJsonAsync<MissionCompletionResponse>();
        body.Should().NotBeNull();
        body!.PlayerId.Should().Be(playerId);
        body.CompletionId.Should().Be(request.CompletionId);
        body.MissionId.Should().Be(request.MissionId);
        // Hard, score 5000, 600s → (100 + 100 score bonus) * 2.0 = 400 XP
        body.ExperienceGranted.Should().Be(400);
        // (500 + 500 score) * 2.0 = 2000 cash
        body.CashGranted.Should().Be(2000m);
        body.ItemsGranted.Should().ContainKey("mission_token").WhoseValue.Should().Be(1);
        body.ItemsGranted.Should().ContainKey("rare_component").WhoseValue.Should().Be(1);
        body.NewExperiencePoints.Should().Be(400);
        body.NewCashBalance.Should().Be(3000m); // 1000 starting + 2000 granted
        body.NewProfileVersion.Should().Be(2);  // started at 1, incremented
    }

    [Fact]
    public async Task CompleteMission_DuplicateSameBody_ReturnsOkWithIdenticalResponse()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);
        var request = SampleRequest();

        var first = await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<MissionCompletionResponse>();

        var second = await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<MissionCompletionResponse>();

        secondBody.Should().BeEquivalentTo(firstBody);
    }

    [Fact]
    public async Task CompleteMission_DuplicateDifferentBody_ReturnsConflict()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);
        var completionId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync(
            $"/players/{playerId}/mission-completions",
            SampleRequest(completionId: completionId, score: 5000));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            $"/players/{playerId}/mission-completions",
            SampleRequest(completionId: completionId, score: 9999));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompleteMission_NonExistentPlayer_ReturnsNotFound()
    {
        var client = _fixture.Factory.CreateClient();
        var nonExistentPlayerId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/players/{nonExistentPlayerId}/mission-completions",
            SampleRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteMission_DuplicateRequest_DoesNotCreateSecondLedgerRow()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);
        var request = SampleRequest(); // Hard difficulty → mission_token + rare_component (2 items)

        await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);
        var afterFirst = await CountInventoryTransactionsAsync(playerId);

        await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);
        var afterReplay = await CountInventoryTransactionsAsync(playerId);

        afterFirst.Should().Be(2);
        afterReplay.Should().Be(2); // replay must not write a second pair of ledger rows
    }

    [Fact]
    public async Task CompleteMission_GrantsAtomically_UpdatesProfileXpCashAndVersion()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);

        await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", SampleRequest());

        var profileResponse = await client.GetAsync($"/players/{playerId}/profile");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<PlayerProfileResponse>();

        profile!.ExperiencePoints.Should().Be(400);
        profile.CashBalance.Should().Be(3000m);
        profile.Version.Should().Be(2);
    }

    [Fact]
    public async Task CompleteMission_EmitsOutboxEventInSameTransaction()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);
        var request = SampleRequest();

        await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);

        var outboxRows = await ReadOutboxRowsForPlayerAsync(playerId);
        outboxRows.Should().HaveCount(1);

        var row = outboxRows[0];
        row.Status.Should().Be("Pending");
        row.Topic.Should().Be("mission-completion");
        row.RetryCount.Should().Be(0);

        using var doc = JsonDocument.Parse(row.Payload);
        doc.RootElement.GetProperty("playerId").GetGuid().Should().Be(playerId);
        doc.RootElement.GetProperty("missionId").GetString().Should().Be(request.MissionId);
        doc.RootElement.GetProperty("experienceGranted").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task CompleteMission_ReplayDoesNotEmitDuplicateOutboxEvent()
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);
        var request = SampleRequest();

        await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);
        await client.PostAsJsonAsync($"/players/{playerId}/mission-completions", request);

        var outboxRows = await ReadOutboxRowsForPlayerAsync(playerId);
        outboxRows.Should().HaveCount(1);
    }

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task CompleteMission_InvalidBody_ReturnsBadRequest(MissionCompletionRequest invalidRequest)
    {
        var client = _fixture.Factory.CreateClient();
        var playerId = await CreatePlayerAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/players/{playerId}/mission-completions",
            invalidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public static IEnumerable<object[]> InvalidRequestCases()
    {
        yield return new object[]
        {
            new MissionCompletionRequest(Guid.Empty, "mission_escape_001", 5000, 600, "Hard"),
        };
        yield return new object[]
        {
            new MissionCompletionRequest(Guid.NewGuid(), string.Empty, 5000, 600, "Hard"),
        };
        yield return new object[]
        {
            new MissionCompletionRequest(Guid.NewGuid(), "mission_escape_001", 5000, 600, string.Empty),
        };
        yield return new object[]
        {
            new MissionCompletionRequest(Guid.NewGuid(), "mission_escape_001", -1, 600, "Hard"),
        };
        yield return new object[]
        {
            new MissionCompletionRequest(Guid.NewGuid(), "mission_escape_001", 5000, -1, "Hard"),
        };
    }

    private static MissionCompletionRequest SampleRequest(
        Guid? completionId = null,
        string missionId = "mission_escape_001",
        int score = 5000,
        int durationSeconds = 600,
        string difficulty = "Hard")
    {
        return new MissionCompletionRequest(
            completionId ?? Guid.NewGuid(),
            missionId,
            score,
            durationSeconds,
            difficulty);
    }

    private static async Task<Guid> CreatePlayerAsync(HttpClient client)
    {
        var username = $"MissionTester_{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/players", new CreatePlayerRequest(username));
        response.EnsureSuccessStatusCode();
        var player = await response.Content.ReadFromJsonAsync<PlayerResponse>();
        return player!.Id;
    }

    private async Task<int> CountInventoryTransactionsAsync(Guid playerId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM InventoryTransactions WHERE PlayerId = @PlayerId",
            new { PlayerId = playerId });
    }

    private async Task<List<OutboxRow>> ReadOutboxRowsForPlayerAsync(Guid playerId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        var rows = await connection.QueryAsync<OutboxRow>(
            """
            SELECT Topic, Status, RetryCount, Payload
            FROM OutboxEvents
            WHERE Topic = 'mission-completion'
              AND JSON_VALUE(Payload, '$.playerId') = @PlayerId
            """,
            new { PlayerId = playerId.ToString() });
        return rows.ToList();
    }

    private sealed record OutboxRow(string Topic, string Status, int RetryCount, string Payload);
}
