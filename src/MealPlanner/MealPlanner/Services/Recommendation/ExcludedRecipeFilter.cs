using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Rejects recipes the caller has already committed — placed in an earlier
/// slot of the same day plan, or excluded by the regenerate flow — so a day
/// plan never repeats the same recipe.
/// </summary>
public sealed class ExcludedRecipeFilter : IRecipeFilter
{
    public bool Allow(Recipe recipe, RecommendationContext ctx) =>
        !ctx.Meal.ExcludedRecipeKeys.Contains(RecipeKey.For(recipe));
}
