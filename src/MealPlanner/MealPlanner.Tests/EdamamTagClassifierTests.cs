using MealPlanner.Models;
using MealPlanner.Services;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class EdamamTagClassifierTests
{
    [Test]
    public void Classify_CuisineTag_RoutesToCuisineTypes()
    {
        var result = EdamamTagClassifier.Classify(["Italian"]);
        Assert.That(result.CuisineTypes, Does.Contain("Italian"));
    }

    [Test]
    public void Classify_MealTypeTag_RoutesToMealTypes()
    {
        var result = EdamamTagClassifier.Classify(["Breakfast"]);
        Assert.That(result.MealTypes, Does.Contain("Breakfast"));
    }

    [Test]
    public void Classify_DishTypeTag_RoutesToDishTypes()
    {
        var result = EdamamTagClassifier.Classify(["Salad"]);
        Assert.That(result.DishTypes, Does.Contain("Salad"));
    }

    [Test]
    public void Classify_DietTag_RoutesToDiets()
    {
        var result = EdamamTagClassifier.Classify(["high-protein"]);
        Assert.That(result.Diets, Does.Contain("high-protein"));
    }

    [Test]
    public void Classify_HealthTag_RoutesToHealthLabels()
    {
        var result = EdamamTagClassifier.Classify(["Vegan"]);
        Assert.That(result.HealthLabels, Does.Contain("vegan"));
    }

    [Test]
    public void Classify_UnknownTag_RoutesToFreeText()
    {
        var result = EdamamTagClassifier.Classify(["Comfort Food"]);
        Assert.That(result.FreeTextTerms, Does.Contain("Comfort Food"));
    }

    [Test]
    public void Classify_MatchingIsCaseInsensitive()
    {
        var result = EdamamTagClassifier.Classify(["italian"]);
        Assert.That(result.CuisineTypes, Does.Contain("Italian"));
    }

    [Test]
    public void Classify_MatchingIsSeparatorInsensitive()
    {
        // Our tags use spaces; Edamam's diet values are hyphenated.
        var result = EdamamTagClassifier.Classify(["Low Carb", "Middle Eastern"]);
        Assert.That(result.Diets, Does.Contain("low-carb"));
        Assert.That(result.CuisineTypes, Does.Contain("Middle Eastern"));
    }

    [Test]
    public void Classify_AppliesDishTypeAlias()
    {
        // The likely tag "Dessert" maps to Edamam's "Desserts".
        var result = EdamamTagClassifier.Classify(["Dessert"]);
        Assert.That(result.DishTypes, Does.Contain("Desserts"));
    }

    [Test]
    public void Classify_MixedTags_RoutesEachToItsOwnFacet()
    {
        var result = EdamamTagClassifier.Classify(["Italian", "Breakfast", "Comfort Food"]);
        Assert.That(result.CuisineTypes, Does.Contain("Italian"));
        Assert.That(result.MealTypes, Does.Contain("Breakfast"));
        Assert.That(result.FreeTextTerms, Does.Contain("Comfort Food"));
    }

    [Test]
    public void Classify_DeduplicatesRepeatedFacetValues()
    {
        var result = EdamamTagClassifier.Classify(["Italian", "italian"]);
        Assert.That(result.CuisineTypes.Count, Is.EqualTo(1));
    }

    [Test]
    public void Classify_EmptyInput_ReturnsEmptySelection()
    {
        var result = EdamamTagClassifier.Classify([]);
        Assert.That(result.CuisineTypes, Is.Empty);
        Assert.That(result.FreeTextTerms, Is.Empty);
    }

    // --- ResolveLocalTags (inverse direction) ---

    [Test]
    public void ResolveLocalTags_EdamamCuisineString_ResolvesToLocalCuisineTag()
    {
        var italian = new Tag { Id = 1, Name = "Italian" };
        var breakfast = new Tag { Id = 2, Name = "Breakfast" };

        var result = EdamamTagClassifier.ResolveLocalTags(["Italian"], [italian, breakfast]);

        Assert.That(result, Does.Contain(italian));
        Assert.That(result, Does.Not.Contain(breakfast));
    }

    [Test]
    public void ResolveLocalTags_EdamamHealthString_ResolvesToLocalHealthTag()
    {
        var vegan = new Tag { Id = 1, Name = "Vegan" };

        var result = EdamamTagClassifier.ResolveLocalTags(["vegan"], [vegan]);

        Assert.That(result, Does.Contain(vegan));
    }

    [Test]
    public void ResolveLocalTags_EdamamDishStringMatchesAliasedLocalTag()
    {
        // Edamam returns "Desserts"; the local Tag uses the alias "Dessert"
        // (singular). The reverse map must find it via the same alias table the
        // forward classifier uses.
        var dessert = new Tag { Id = 1, Name = "Dessert" };

        var result = EdamamTagClassifier.ResolveLocalTags(["Desserts"], [dessert]);

        Assert.That(result, Does.Contain(dessert));
    }

    [Test]
    public void ResolveLocalTags_MatchingIsCaseAndSeparatorInsensitive()
    {
        var lowCarb = new Tag { Id = 1, Name = "Low Carb" };

        var result = EdamamTagClassifier.ResolveLocalTags(["low-carb"], [lowCarb]);

        Assert.That(result, Does.Contain(lowCarb));
    }

    [Test]
    public void ResolveLocalTags_MultipleCategories_ResolveEachToItsOwnTag()
    {
        var italian = new Tag { Id = 1, Name = "Italian" };
        var breakfast = new Tag { Id = 2, Name = "Breakfast" };
        var dessert = new Tag { Id = 3, Name = "Dessert" };
        var unrelated = new Tag { Id = 4, Name = "Comfort Food" };

        var result = EdamamTagClassifier.ResolveLocalTags(
            ["Italian", "Breakfast", "Desserts"],
            [italian, breakfast, dessert, unrelated]);

        Assert.That(result, Is.EquivalentTo(new[] { italian, breakfast, dessert }));
    }

    [Test]
    public void ResolveLocalTags_DeduplicatesAcrossMatches()
    {
        // If two Edamam strings both map to the same local Tag (e.g. via an
        // alias collision), the local Tag appears in the result only once.
        var dessert = new Tag { Id = 1, Name = "Dessert" };

        var result = EdamamTagClassifier.ResolveLocalTags(["Desserts", "Desserts"], [dessert]);

        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public void ResolveLocalTags_BothAliasAndCanonicalLocalTags_ReturnsAll()
    {
        // A user with both a "Dessert" tag and a "Desserts" tag should see both
        // attached when Edamam returns "Desserts".
        var dessertAlias = new Tag { Id = 1, Name = "Dessert" };
        var dessertCanonical = new Tag { Id = 2, Name = "Desserts" };

        var result = EdamamTagClassifier.ResolveLocalTags(["Desserts"], [dessertAlias, dessertCanonical]);

        Assert.That(result, Is.EquivalentTo(new[] { dessertAlias, dessertCanonical }));
    }

    [Test]
    public void ResolveLocalTags_NoMatchingLocalTags_ReturnsEmpty()
    {
        var italian = new Tag { Id = 1, Name = "Italian" };

        var result = EdamamTagClassifier.ResolveLocalTags(["Mexican"], [italian]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolveLocalTags_EmptyCategories_ReturnsEmpty()
    {
        var italian = new Tag { Id = 1, Name = "Italian" };

        var result = EdamamTagClassifier.ResolveLocalTags([], [italian]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolveLocalTags_UnclassifiableLocalTag_NeverMatches()
    {
        // A local tag whose name maps to no Edamam facet (e.g. "Comfort Food"
        // falls through to free text in the forward direction) cannot be
        // resolved from any Edamam category and must not appear in the result.
        var comfortFood = new Tag { Id = 1, Name = "Comfort Food" };

        var result = EdamamTagClassifier.ResolveLocalTags(
            ["Italian", "Breakfast", "Comfort Food"],
            [comfortFood]);

        Assert.That(result, Is.Empty);
    }
}
