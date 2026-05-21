using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Scores a recipe by its community upvote percentage, normalized to [0, 1].
/// </summary>
public sealed class VotePercentageScorer : IRecipeScorer
{
    public float Score(Recipe recipe, RecommendationContext ctx) =>
        ctx.User.VotePercentages.GetValueOrDefault(recipe.Id, 0.5f);
}
