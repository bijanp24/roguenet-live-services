using FluentAssertions;
using RogueNet.Domain.Services;

namespace RogueNet.UnitTests.Domain;

public class RewardCalculatorTests
{
    [Fact]
    public void CalculateReward_EasyDifficulty_ReturnsBaseReward()
    {
        // Arrange
        var missionId = "mission_test_001";
        var score = 1000;
        var duration = 900; // 15 minutes, no speed bonus
        var difficulty = "Easy";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert
        reward.ExperiencePoints.Should().Be(150); // Base 100 + 50 score bonus * 1.0 difficulty
        reward.Cash.Should().Be(600m); // Base 500 + 100 from score * 1.0
        reward.Items.Should().ContainKey("mission_token");
        reward.Items["mission_token"].Should().Be(1);
    }

    [Fact]
    public void CalculateReward_HardDifficulty_AppliesMultiplier()
    {
        // Arrange
        var missionId = "mission_test_002";
        var score = 5000;
        var duration = 900;
        var difficulty = "Hard";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert
        reward.ExperiencePoints.Should().Be(400); // (Base 100 + 100 score bonus) * 2.0 difficulty
        reward.Cash.Should().Be(2000m); // (500 + 500 from score) * 2.0
        reward.Items.Should().ContainKey("mission_token");
        reward.Items.Should().ContainKey("rare_component");
    }

    [Fact]
    public void CalculateReward_ExpertDifficulty_GrantsAllItems()
    {
        // Arrange
        var missionId = "mission_test_003";
        var score = 10000;
        var duration = 900;
        var difficulty = "Expert";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert
        reward.ExperiencePoints.Should().Be(900); // (100 + 200 score bonus) * 3.0 difficulty
        reward.Items.Should().ContainKey("mission_token");
        reward.Items.Should().ContainKey("rare_component");
        reward.Items.Should().ContainKey("epic_cache");
    }

    [Fact]
    public void CalculateReward_FastCompletion_AppliesSpeedBonus()
    {
        // Arrange
        var missionId = "mission_speed_test";
        var score = 1000;
        var duration = 250; // Under 5 minutes
        var difficulty = "Normal";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert - (100 + 50) * 1.5 difficulty * 1.5 speed
        reward.ExperiencePoints.Should().Be(337); // Rounded from 337.5
        reward.Cash.Should().BeGreaterThan(1000m); // Speed bonus applied
    }

    [Fact]
    public void CalculateReward_ModerateSpeed_AppliesReducedBonus()
    {
        // Arrange
        var missionId = "mission_moderate_speed";
        var score = 1000;
        var duration = 500; // Under 10 minutes
        var difficulty = "Normal";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert - (100 + 50) * 1.5 difficulty * 1.2 speed
        reward.ExperiencePoints.Should().Be(270);
    }

    [Fact]
    public void CalculateReward_HighScore_GrantsBonusExperience()
    {
        // Arrange
        var missionId = "mission_high_score";
        var score = 15000;
        var duration = 900;
        var difficulty = "Normal";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert
        reward.ExperiencePoints.Should().Be(450); // (100 + 200 bonus) * 1.5 difficulty
        reward.Cash.Should().BeGreaterThan(2000m); // High score contributes to cash
    }

    [Fact]
    public void CalculateReward_LowScore_GrantsMinimalBonus()
    {
        // Arrange
        var missionId = "mission_low_score";
        var score = 500;
        var duration = 900;
        var difficulty = "Normal";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert
        reward.ExperiencePoints.Should().Be(150); // (100 + 0 bonus) * 1.5
    }

    [Fact]
    public void CalculateReward_InvalidDifficulty_UsesDefaultMultiplier()
    {
        // Arrange
        var missionId = "mission_invalid";
        var score = 1000;
        var duration = 900;
        var difficulty = "InvalidLevel";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert
        reward.ExperiencePoints.Should().Be(150); // (100 + 50) * 1.0 (default)
    }

    [Fact]
    public void CalculateReward_CombinesAllBonuses_ExpertFastHighScore()
    {
        // Arrange
        var missionId = "mission_perfect_run";
        var score = 12000;
        var duration = 200; // Very fast
        var difficulty = "Expert";

        // Act
        var reward = RewardCalculator.CalculateReward(missionId, score, duration, difficulty);

        // Assert - All multipliers stack
        reward.ExperiencePoints.Should().BeGreaterThan(1000); // (100 + 200) * 3.0 * 1.5
        reward.Cash.Should().BeGreaterThan(5000m);
        reward.Items.Count.Should().Be(3); // All items granted
    }

    [Theory]
    [InlineData("easy", 1.0)]
    [InlineData("Easy", 1.0)]
    [InlineData("EASY", 1.0)]
    [InlineData("normal", 1.5)]
    [InlineData("Normal", 1.5)]
    [InlineData("hard", 2.0)]
    [InlineData("Hard", 2.0)]
    [InlineData("expert", 3.0)]
    [InlineData("Expert", 3.0)]
    public void CalculateReward_DifficultyIsCaseInsensitive(string difficulty, decimal expectedMultiplier)
    {
        // Arrange
        var score = 1000;
        var duration = 900;

        // Act
        var reward = RewardCalculator.CalculateReward("test", score, duration, difficulty);

        // Assert - Verify multiplier was applied correctly
        reward.ExperiencePoints.Should().Be((int)(150 * expectedMultiplier));
    }
}
