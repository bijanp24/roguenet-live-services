using FluentAssertions;

using RogueNet.Application.MissionCompletions;

namespace RogueNet.UnitTests.Application.MissionCompletions;

public class MissionCompletionServiceTests
{
    private static CompleteMissionCommand SampleCommand(
        Guid? playerId = null,
        Guid? completionId = null,
        string missionId = "mission_escape_001",
        int score = 5000,
        int durationSeconds = 600,
        string difficulty = "Hard")
    {
        return new CompleteMissionCommand(
            playerId ?? Guid.NewGuid(),
            completionId ?? Guid.NewGuid(),
            missionId,
            score,
            durationSeconds,
            difficulty);
    }

    [Fact]
    public async Task ExecuteAsync_Persisted_MapsToSuccessWithWasReplayFalse()
    {
        var command = SampleCommand();
        var stubResult = BuildResult(command);
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.Persisted(stubResult));
        var service = new MissionCompletionService(repo);

        var outcome = await service.ExecuteAsync(command, CancellationToken.None);

        outcome.Should().BeOfType<CompleteMissionOutcome.Success>()
            .Which.WasReplay.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_Replayed_MapsToSuccessWithWasReplayTrue()
    {
        var command = SampleCommand();
        var stubResult = BuildResult(command);
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.Replayed(stubResult));
        var service = new MissionCompletionService(repo);

        var outcome = await service.ExecuteAsync(command, CancellationToken.None);

        outcome.Should().BeOfType<CompleteMissionOutcome.Success>()
            .Which.WasReplay.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_IdempotencyConflict_MapsToConflictOutcome()
    {
        var command = SampleCommand();
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.IdempotencyConflict());
        var service = new MissionCompletionService(repo);

        var outcome = await service.ExecuteAsync(command, CancellationToken.None);

        outcome.Should().BeOfType<CompleteMissionOutcome.IdempotencyConflict>()
            .Which.CompletionId.Should().Be(command.CompletionId);
    }

    [Fact]
    public async Task ExecuteAsync_PlayerNotFound_MapsToPlayerNotFoundOutcome()
    {
        var command = SampleCommand();
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.PlayerNotFound());
        var service = new MissionCompletionService(repo);

        var outcome = await service.ExecuteAsync(command, CancellationToken.None);

        outcome.Should().BeOfType<CompleteMissionOutcome.PlayerNotFound>()
            .Which.PlayerId.Should().Be(command.PlayerId);
    }

    [Fact]
    public async Task ExecuteAsync_HandsCalculatedRewardToRepository()
    {
        var command = SampleCommand(score: 5000, durationSeconds: 900, difficulty: "Hard");
        var repo = new FakeRepository(w => new MissionCompletionPersistResult.Persisted(BuildResult(command)));
        var service = new MissionCompletionService(repo);

        await service.ExecuteAsync(command, CancellationToken.None);

        // Score 5000 + duration 900 + Hard = (100 + 100) * 2.0 = 400 XP, (500 + 500) * 2.0 = 2000 cash
        repo.LastWrite!.ExperienceGranted.Should().Be(400);
        repo.LastWrite.CashGranted.Should().Be(2000m);
        repo.LastWrite.ItemsGranted.Should().ContainKey("mission_token");
        repo.LastWrite.ItemsGranted.Should().ContainKey("rare_component");
    }

    [Fact]
    public async Task ExecuteAsync_SameBody_ProducesIdenticalRequestHash()
    {
        var first = SampleCommand();
        var second = first with { CompletionId = Guid.NewGuid(), PlayerId = Guid.NewGuid() };
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.Persisted(BuildResult(first)));
        var service = new MissionCompletionService(repo);

        await service.ExecuteAsync(first, CancellationToken.None);
        var firstHash = repo.LastWrite!.RequestHash;
        await service.ExecuteAsync(second, CancellationToken.None);
        var secondHash = repo.LastWrite!.RequestHash;

        secondHash.Should().Be(firstHash);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentScore_ProducesDifferentRequestHash()
    {
        var first = SampleCommand(score: 5000);
        var second = first with { Score = 5001 };
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.Persisted(BuildResult(first)));
        var service = new MissionCompletionService(repo);

        await service.ExecuteAsync(first, CancellationToken.None);
        var firstHash = repo.LastWrite!.RequestHash;
        await service.ExecuteAsync(second, CancellationToken.None);
        var secondHash = repo.LastWrite!.RequestHash;

        secondHash.Should().NotBe(firstHash);
    }

    [Theory]
    [InlineData("Hard", "hard")]
    [InlineData("HARD", "Hard")]
    [InlineData("expert", "Expert")]
    public async Task ExecuteAsync_DifficultyCaseDifference_ProducesIdenticalRequestHash(
        string firstDifficulty,
        string secondDifficulty)
    {
        var first = SampleCommand(difficulty: firstDifficulty);
        var second = first with { Difficulty = secondDifficulty };
        var repo = new FakeRepository(_ => new MissionCompletionPersistResult.Persisted(BuildResult(first)));
        var service = new MissionCompletionService(repo);

        await service.ExecuteAsync(first, CancellationToken.None);
        var firstHash = repo.LastWrite!.RequestHash;
        await service.ExecuteAsync(second, CancellationToken.None);
        var secondHash = repo.LastWrite!.RequestHash;

        secondHash.Should().Be(firstHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_BlankMissionId_ThrowsArgumentException(string missionId)
    {
        var service = new MissionCompletionService(new FakeRepository(_ => throw new InvalidOperationException("repo should not be called")));

        var act = () => service.ExecuteAsync(SampleCommand(missionId: missionId), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_BlankDifficulty_ThrowsArgumentException(string difficulty)
    {
        var service = new MissionCompletionService(new FakeRepository(_ => throw new InvalidOperationException("repo should not be called")));

        var act = () => service.ExecuteAsync(SampleCommand(difficulty: difficulty), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_NegativeScore_ThrowsArgumentException()
    {
        var service = new MissionCompletionService(new FakeRepository(_ => throw new InvalidOperationException("repo should not be called")));

        var act = () => service.ExecuteAsync(SampleCommand(score: -1), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_NegativeDuration_ThrowsArgumentException()
    {
        var service = new MissionCompletionService(new FakeRepository(_ => throw new InvalidOperationException("repo should not be called")));

        var act = () => service.ExecuteAsync(SampleCommand(durationSeconds: -1), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static MissionCompletionResult BuildResult(CompleteMissionCommand command)
    {
        return new MissionCompletionResult(
            CompletionId: command.CompletionId,
            PlayerId: command.PlayerId,
            MissionId: command.MissionId,
            ExperienceGranted: 400,
            CashGranted: 2000m,
            ItemsGranted: new Dictionary<string, int> { ["mission_token"] = 1 },
            NewExperiencePoints: 400,
            NewLevel: 1,
            NewCashBalance: 3000m,
            NewProfileVersion: 2,
            CompletedAt: DateTime.UtcNow);
    }

    private sealed class FakeRepository : IMissionCompletionRepository
    {
        private readonly Func<MissionCompletionWrite, MissionCompletionPersistResult> _respond;

        public FakeRepository(Func<MissionCompletionWrite, MissionCompletionPersistResult> respond)
        {
            _respond = respond;
        }

        public MissionCompletionWrite? LastWrite { get; private set; }

        public Task<MissionCompletionPersistResult> CompleteAsync(
            MissionCompletionWrite write,
            CancellationToken cancellationToken)
        {
            LastWrite = write;
            return Task.FromResult(_respond(write));
        }
    }
}
