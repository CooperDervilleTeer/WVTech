using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Scores a recipe by how closely its protein/carb/fat composition matches the
/// meal slot's target composition. Both the recipe's macros and the slot's
/// targets are reduced to fraction vectors on the macro simplex (each summing
/// to 1), and the score is the L1 distance between them mapped into [0, 1] —
/// identical balance scores 1, opposite balance scores 0. The score is
/// scale-invariant: only the macro <em>balance</em> matters, not portion size
/// (the composer handles total amounts). Returns 0 when the slot has fewer
/// than three macro targets, or the recipe carries no macros.
/// </summary>
public sealed class NutrientFitScorer : IRecipeScorer
{
    public float Score(Recipe recipe, RecommendationContext ctx)
    {
        var meal = ctx.Meal;
        if (!meal.ProteinTarget.HasValue || !meal.CarbTarget.HasValue || !meal.FatTarget.HasValue)
            return 0f;

        float targetTotal = meal.ProteinTarget.Value + meal.CarbTarget.Value + meal.FatTarget.Value;
        float recipeTotal = recipe.Protein + recipe.Carbs + recipe.Fat;
        if (targetTotal <= 0f || recipeTotal <= 0f) return 0f;

        // L1 distance between the two macro-fraction vectors. Both lie on the
        // simplex (components sum to 1), so the distance is at most 2.
        float distance =
            Math.Abs(recipe.Protein / recipeTotal - meal.ProteinTarget.Value / targetTotal)
            + Math.Abs(recipe.Carbs / recipeTotal - meal.CarbTarget.Value / targetTotal)
            + Math.Abs(recipe.Fat / recipeTotal - meal.FatTarget.Value / targetTotal);

        return 1f - distance / 2f;
    }
}
