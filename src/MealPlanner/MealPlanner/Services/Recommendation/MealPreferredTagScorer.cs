using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Soft preference scorer based on the explicit tag preferences for this meal
/// slot. Returns the rarity-weighted fraction of
/// <see cref="MealRecommendationContext.PreferredTagIds"/> that this recipe
/// matches, in [0, 1]: a match on a rarer tag (higher
/// <see cref="UserRecommendationContext.TagRarityWeights"/>) counts more
/// than a match on a common tag. Tags missing from the weight map default
/// to weight 1, so an empty map degenerates to the plain matches/total
/// fraction. Returns 0 when the slot has no preferred tags. Works alongside
/// <see cref="PreferredTagFilter"/>, which removes recipes that match no
/// preferred tags at all.
/// </summary>
public sealed class MealPreferredTagScorer : IRecipeScorer
{
    public float Score(Recipe recipe, RecommendationContext ctx)
    {
        var preferred = ctx.Meal.PreferredTagIds;
        if (preferred.Count == 0) return 0f;

        var recipeTagIds = recipe.Tags.Select(t => t.Id).ToHashSet();
        var weights = ctx.User.TagRarityWeights;
        float matchedWeight = 0f, totalWeight = 0f;
        foreach (var tagId in preferred)
        {
            float w = weights.GetValueOrDefault(tagId, 1f);
            totalWeight += w;
            if (recipeTagIds.Contains(tagId)) matchedWeight += w;
        }
        return totalWeight > 0 ? matchedWeight / totalWeight : 0f;
    }
}
