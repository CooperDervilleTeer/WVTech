using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Stable identity for a recipe candidate, used to de-duplicate recipes across
/// streams and across the slots of a day plan. Local recipes key on their
/// database id; transient external recipes (id 0) key on their source URI.
/// </summary>
public static class RecipeKey
{
    public static string For(Recipe recipe) =>
        recipe.Id != 0 ? $"id:{recipe.Id}" : $"uri:{recipe.ExternalUri ?? string.Empty}";
}
