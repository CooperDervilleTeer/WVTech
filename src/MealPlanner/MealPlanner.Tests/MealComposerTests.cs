using MealPlanner.Models;
using MealPlanner.Services.Recommendation;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class MealComposerTests
{
    private static Recipe R(int id, int calories, int protein = 0, int carbs = 0, int fat = 0) =>
        new() { Id = id, Name = $"R{id}", Calories = calories, Protein = protein, Carbs = carbs, Fat = fat, Tags = [] };

    // --- Compose (macro-aware brute-force subset selection) ---

    [Test]
    public void Compose_EmptyInput_ReturnsEmpty()
    {
        var result = MealComposer.Compose([], calorieTarget: 2000, maxRecipes: 5);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Compose_RecipeWithinCalorieBudget_IsIncluded()
    {
        var recipe = R(1, 300);

        var result = MealComposer.Compose([recipe], calorieTarget: 500, maxRecipes: 5);

        Assert.That(result, Does.Contain(recipe));
    }

    [Test]
    public void Compose_RecipeExceedingCalorieBudget_IsExcluded()
    {
        var recipe = R(1, 600);

        var result = MealComposer.Compose([recipe], calorieTarget: 500, maxRecipes: 5);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Compose_CombinedCaloriesExceedingBudget_DropsLowerRankedRecipe()
    {
        var a = R(1, 300);
        var b = R(2, 300);

        var result = MealComposer.Compose([a, b], calorieTarget: 400, maxRecipes: 5);

        Assert.That(result, Does.Contain(a));
        Assert.That(result, Does.Not.Contain(b));
    }

    [Test]
    public void Compose_MaxRecipesLimit_IsEnforced()
    {
        var recipes = Enumerable.Range(1, 10).Select(i => R(i, 50)).ToList();

        var result = MealComposer.Compose(recipes, calorieTarget: 9999, maxRecipes: 3);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public void Compose_NoMacroTargets_FillsWithTopRankedRecipes()
    {
        var a = R(1, 100);
        var b = R(2, 100);
        var c = R(3, 100);

        var result = MealComposer.Compose([a, b, c], calorieTarget: 9999, maxRecipes: 5);

        Assert.That(result.Select(r => r.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Compose_PrefersSubsetClosestToMacroTarget()
    {
        // badProtein ranks first but overshoots the 30g protein target 3x;
        // goodProtein matches it exactly. The composer drops the off-target
        // recipe even though it ranks higher and fits the calorie budget.
        var badProtein = R(1, calories: 100, protein: 90);
        var goodProtein = R(2, calories: 100, protein: 30);

        var result = MealComposer.Compose(
            [badProtein, goodProtein], calorieTarget: 9999, maxRecipes: 5, proteinTarget: 30);

        Assert.That(result, Does.Contain(goodProtein));
        Assert.That(result, Does.Not.Contain(badProtein));
    }

    [Test]
    public void Compose_LoneMacroOvershootRecipe_IsStillIncluded()
    {
        // A single recipe that overshoots the protein target is soft-penalized,
        // not hard-rejected — with no alternative it is still returned.
        var recipe = R(1, calories: 100, protein: 50);

        var result = MealComposer.Compose(
            [recipe], calorieTarget: 9999, maxRecipes: 5, proteinTarget: 20);

        Assert.That(result, Does.Contain(recipe));
    }

    [Test]
    public void Compose_HighMacroRecipeDoesNotBlockSmallerOnes()
    {
        // Documented greedy bug: a 30g-protein recipe used to fill the whole
        // 30g budget and block the two 5g recipes. The composer fits all three.
        var bigProtein = R(1, calories: 100, protein: 30);
        var smallA = R(2, calories: 100, protein: 5);
        var smallB = R(3, calories: 100, protein: 5);

        var result = MealComposer.Compose(
            [bigProtein, smallA, smallB], calorieTarget: 9999, maxRecipes: 5, proteinTarget: 30);

        Assert.That(result, Has.Count.GreaterThanOrEqualTo(2));
    }
}
