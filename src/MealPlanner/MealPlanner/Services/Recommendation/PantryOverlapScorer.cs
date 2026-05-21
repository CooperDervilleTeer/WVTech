using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Scores a recipe by the fraction of its distinct ingredients the user
/// already has in their pantry. Ingredient names are normalized the same way
/// on both sides so the match works for local and external recipes alike.
/// Returns 0 when the pantry is empty or the recipe carries no ingredients.
/// </summary>
public sealed class PantryOverlapScorer : IRecipeScorer
{
    public float Score(Recipe recipe, RecommendationContext ctx)
    {
        var pantry = ctx.User.PantryIngredientNames;
        if (pantry.Count == 0) return 0f;

        var recipeIngredients = recipe.Ingredients
            .Select(i => IngredientNameNormalizer.NormalizeKey(i.IngredientBase.Name))
            .Distinct()
            .ToList();
        if (recipeIngredients.Count == 0) return 0f;

        int matches = recipeIngredients.Count(pantry.Contains);
        return (float)matches / recipeIngredients.Count;
    }
}
