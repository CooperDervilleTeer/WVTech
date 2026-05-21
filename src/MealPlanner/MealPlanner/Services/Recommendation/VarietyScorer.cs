using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Scores a recipe by how "fresh" it is for the planned date — 1 when the user
/// has not eaten it within the lookback window, decaying toward 0 the more
/// recently and more often it appears on nearby days, past or future. Keeps a
/// day plan from echoing the rest of the week.
/// </summary>
public sealed class VarietyScorer : IRecipeScorer
{
    /// <summary>
    /// How many days either side of the planned date count toward recency.
    /// Tunable.
    /// </summary>
    public const int VarietyWindowDays = 14;

    public float Score(Recipe recipe, RecommendationContext ctx)
    {
        if (!ctx.User.RecentRecipeDayOffsets.TryGetValue(recipe.Id, out var offsets))
            return 1f;

        // Each nearby occurrence adds staleness: a day-1 occurrence costs the
        // full 1.0, a window-edge occurrence costs only 1/window. Repeated
        // occurrences accumulate, so frequent recipes fall fastest.
        float staleness = 0f;
        foreach (int d in offsets)
        {
            if (d < 1 || d > VarietyWindowDays) continue;
            staleness += (float)(VarietyWindowDays - d + 1) / VarietyWindowDays;
        }
        return Math.Max(0f, 1f - staleness);
    }
}
