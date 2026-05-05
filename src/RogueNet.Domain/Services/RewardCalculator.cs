using RogueNet.Domain.ValueObjects;

namespace RogueNet.Domain.Services;

public static class RewardCalculator
{
    private const decimal BaseCashReward = 500m;
    private const int BaseExperienceReward = 100;

    public static MissionReward CalculateReward(
        string missionId,
        int score,
        int durationSeconds,
        string difficulty)
    {
        var difficultyMultiplier = GetDifficultyMultiplier(difficulty);
        var speedBonus = CalculateSpeedBonus(durationSeconds);
        var scoreBonus = CalculateScoreBonus(score);

        var experience = (int)((BaseExperienceReward + scoreBonus) * difficultyMultiplier * speedBonus);
        var cash = (BaseCashReward + (score * 0.1m)) * difficultyMultiplier * speedBonus;

        var items = GetMissionItems(missionId, difficulty);

        return new MissionReward(experience, cash, items);
    }

    private static decimal GetDifficultyMultiplier(string difficulty)
    {
        return difficulty.ToLowerInvariant() switch
        {
            "easy" => 1.0m,
            "normal" => 1.5m,
            "hard" => 2.0m,
            "expert" => 3.0m,
            _ => 1.0m
        };
    }

    private static decimal CalculateSpeedBonus(int durationSeconds)
    {
        // Speed bonus: 1.5x if under 5 minutes, 1.2x if under 10 minutes
        if (durationSeconds < 300)
        {
            return 1.5m;
        }

        if (durationSeconds < 600)
        {
            return 1.2m;
        }

        return 1.0m;
    }

    private static int CalculateScoreBonus(int score)
    {
        // Bonus XP based on score thresholds
        return score switch
        {
            >= 10000 => 200,
            >= 5000 => 100,
            >= 1000 => 50,
            _ => 0
        };
    }

    private static Dictionary<string, int> GetMissionItems(string missionId, string difficulty)
    {
        // Simplified item rewards - in a real system this would come from mission config
        var items = new Dictionary<string, int>();

        // All missions grant at least one item
        items["mission_token"] = 1;

        // Hard and Expert grant additional items
        if (difficulty.Equals("Hard", StringComparison.OrdinalIgnoreCase) ||
            difficulty.Equals("Expert", StringComparison.OrdinalIgnoreCase))
        {
            items["rare_component"] = 1;
        }

        if (difficulty.Equals("Expert", StringComparison.OrdinalIgnoreCase))
        {
            items["epic_cache"] = 1;
        }

        return items;
    }
}
