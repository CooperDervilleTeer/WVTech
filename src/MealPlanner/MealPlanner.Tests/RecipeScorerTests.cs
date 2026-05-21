using MealPlanner.Models;
using MealPlanner.Services.Recommendation;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class RecipeScorerTests
{
    private static RecommendationContext EmptyContext(
        HashSet<string>? restrictions = null,
        Dictionary<int, UserVoteType>? votes = null,
        Dictionary<int, float>? percentages = null,
        List<Recipe>? upvoted = null,
        HashSet<int>? userPreferredTagIds = null,
        HashSet<string>? pantryIngredientNames = null,
        Dictionary<int, List<int>>? recentRecipeDayOffsets = null,
        int? calorieTarget = null,
        int? proteinTarget = null,
        int? carbTarget = null,
        int? fatTarget = null,
        HashSet<int>? mealPreferredTagIds = null,
        HashSet<string>? excludedRecipeKeys = null,
        Dictionary<int, float>? tagRarityWeights = null) =>
        new(
            new UserRecommendationContext(
                restrictions ?? [],
                votes ?? [],
                percentages ?? [],
                upvoted ?? [],
                userPreferredTagIds ?? [],
                pantryIngredientNames ?? [],
                recentRecipeDayOffsets ?? [],
                tagRarityWeights ?? []),
            new MealRecommendationContext(
                calorieTarget,
                proteinTarget,
                carbTarget,
                fatTarget,
                mealPreferredTagIds ?? [],
                excludedRecipeKeys ?? []));

    // --- UpvotePriorityScorer ---

    [Test]
    public void UpvotePriorityScorer_UpvotedRecipe_ReturnsPositiveScore()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(upvoted: [recipe]);
        var scorer = new UpvotePriorityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.GreaterThan(0f));
    }

    [Test]
    public void UpvotePriorityScorer_NonUpvotedRecipe_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(upvoted: []);
        var scorer = new UpvotePriorityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void UpvotePriorityScorer_UpvotedScoreExceedsMaxVotePercentage()
    {
        // Upvote score must dominate vote%, so it must be > 1.0 (the max normalized vote%).
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(upvoted: [recipe]);
        var scorer = new UpvotePriorityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.GreaterThan(1f));
    }

    // --- VotePercentageScorer ---

    [Test]
    public void VotePercentageScorer_ReturnsNormalizedVotePercentage()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(percentages: new Dictionary<int, float> { [1] = 0.75f });
        var scorer = new VotePercentageScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.75f).Within(0.001f));
    }

    [Test]
    public void VotePercentageScorer_RecipeNotInDictionary_ReturnsHalf()
    {
        var recipe = new Recipe { Id = 99, Tags = [] };
        var ctx = EmptyContext(percentages: []);
        var scorer = new VotePercentageScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.5f).Within(0.001f));
    }

    // --- DownVoteFilter ---

    [Test]
    public void DownVoteFilter_DownvotedRecipe_ReturnsFalse()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(votes: new Dictionary<int, UserVoteType> { [1] = UserVoteType.DownVote });
        var filter = new DownVoteFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    [Test]
    public void DownVoteFilter_NoVoteRecipe_ReturnsTrue()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(votes: new Dictionary<int, UserVoteType> { [1] = UserVoteType.NoVote });
        var filter = new DownVoteFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void DownVoteFilter_UpvotedRecipe_ReturnsTrue()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(votes: new Dictionary<int, UserVoteType> { [1] = UserVoteType.UpVote });
        var filter = new DownVoteFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void DownVoteFilter_RecipeNotInDictionary_ReturnsTrue()
    {
        var recipe = new Recipe { Id = 99, Tags = [] };
        var ctx = EmptyContext(votes: []);
        var filter = new DownVoteFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    // --- MealPreferredTagScorer ---

    [Test]
    public void MealPreferredTagScorer_NoPreferredTags_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(mealPreferredTagIds: []);
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void MealPreferredTagScorer_AllTagsMatch_ReturnsOne()
    {
        var tag = new Tag { Id = 1, Name = "Italian" };
        var recipe = new Recipe { Id = 1, Tags = [tag] };
        var ctx = EmptyContext(mealPreferredTagIds: [1]);
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void MealPreferredTagScorer_HalfTagsMatch_ReturnsHalf()
    {
        var italian = new Tag { Id = 1, Name = "Italian" };
        var recipe = new Recipe { Id = 1, Tags = [italian] }; // only 1 of 2 preferred tags
        var ctx = EmptyContext(mealPreferredTagIds: [1, 2]);
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void MealPreferredTagScorer_NoTagsMatch_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 3, Name = "Mexican" }] };
        var ctx = EmptyContext(mealPreferredTagIds: [1, 2]);
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void MealPreferredTagScorer_RecipeWithNoTags_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(mealPreferredTagIds: [1, 2]);
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void MealPreferredTagScorer_IgnoresUserLevelPreferredTags()
    {
        // Recipe matches a user-level pref, but the meal slot has no preference for it.
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(userPreferredTagIds: [1], mealPreferredTagIds: []);
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void MealPreferredTagScorer_RareTagMatchOutweighsCommonTagMatch()
    {
        // Two recipes each match exactly one of the slot's two preferred tags.
        // The one matching the rarer tag (higher rarity weight) must score
        // strictly more — otherwise the scorer treats "Italian" the same as
        // "Breakfast" even though one is much more discriminating.
        var common = new Tag { Id = 1, Name = "Common" };
        var rare = new Tag { Id = 2, Name = "Rare" };
        var commonRecipe = new Recipe { Id = 1, Tags = [common] };
        var rareRecipe = new Recipe { Id = 2, Tags = [rare] };

        var ctx = EmptyContext(
            mealPreferredTagIds: [1, 2],
            tagRarityWeights: new Dictionary<int, float> { [1] = 0.5f, [2] = 2.0f });
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(rareRecipe, ctx), Is.GreaterThan(scorer.Score(commonRecipe, ctx)),
            "matching a rare-weighted tag must score above matching a common-weighted one");
    }

    [Test]
    public void MealPreferredTagScorer_FullMatchScoresOne_RegardlessOfRarityWeights()
    {
        // A recipe matching every preferred tag still scores 1.0 — the
        // denominator normalises by the sum of weights.
        var a = new Tag { Id = 1, Name = "A" };
        var b = new Tag { Id = 2, Name = "B" };
        var recipe = new Recipe { Id = 1, Tags = [a, b] };
        var ctx = EmptyContext(
            mealPreferredTagIds: [1, 2],
            tagRarityWeights: new Dictionary<int, float> { [1] = 0.1f, [2] = 5.0f });
        var scorer = new MealPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    // --- UserPreferredTagScorer ---

    [Test]
    public void UserPreferredTagScorer_NoPreferredTags_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(userPreferredTagIds: []);
        var scorer = new UserPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void UserPreferredTagScorer_AllTagsMatch_ReturnsOne()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(userPreferredTagIds: [1]);
        var scorer = new UserPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void UserPreferredTagScorer_HalfTagsMatch_ReturnsHalf()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(userPreferredTagIds: [1, 2]);
        var scorer = new UserPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void UserPreferredTagScorer_IgnoresMealLevelPreferredTags()
    {
        // Recipe matches a slot-level pref, but the user has no standing prefs.
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(userPreferredTagIds: [], mealPreferredTagIds: [1]);
        var scorer = new UserPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void UserPreferredTagScorer_RareTagMatchOutweighsCommonTagMatch()
    {
        var common = new Tag { Id = 1, Name = "Common" };
        var rare = new Tag { Id = 2, Name = "Rare" };
        var commonRecipe = new Recipe { Id = 1, Tags = [common] };
        var rareRecipe = new Recipe { Id = 2, Tags = [rare] };

        var ctx = EmptyContext(
            userPreferredTagIds: [1, 2],
            tagRarityWeights: new Dictionary<int, float> { [1] = 0.5f, [2] = 2.0f });
        var scorer = new UserPreferredTagScorer();

        Assert.That(scorer.Score(rareRecipe, ctx), Is.GreaterThan(scorer.Score(commonRecipe, ctx)));
    }

    [Test]
    public void UserPreferredTagScorer_FullMatchScoresOne_RegardlessOfRarityWeights()
    {
        var a = new Tag { Id = 1, Name = "A" };
        var b = new Tag { Id = 2, Name = "B" };
        var recipe = new Recipe { Id = 1, Tags = [a, b] };
        var ctx = EmptyContext(
            userPreferredTagIds: [1, 2],
            tagRarityWeights: new Dictionary<int, float> { [1] = 0.1f, [2] = 5.0f });
        var scorer = new UserPreferredTagScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    // --- PreferredTagFilter ---

    [Test]
    public void PreferredTagFilter_NoMealPreferredTags_AllowsAnyRecipe()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(mealPreferredTagIds: []);
        var filter = new PreferredTagFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void PreferredTagFilter_RecipeMatchesAtLeastOneTag_Allows()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(mealPreferredTagIds: [1, 2]);
        var filter = new PreferredTagFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void PreferredTagFilter_RecipeMatchesNoTags_Rejects()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 3, Name = "Mexican" }] };
        var ctx = EmptyContext(mealPreferredTagIds: [1, 2]);
        var filter = new PreferredTagFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    [Test]
    public void PreferredTagFilter_RecipeWithNoTagsAndSlotHasPreference_Rejects()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(mealPreferredTagIds: [1]);
        var filter = new PreferredTagFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    [Test]
    public void PreferredTagFilter_IgnoresUserLevelPreferredTags()
    {
        // Recipe doesn't match the slot pref but does match a user pref: still rejected.
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(userPreferredTagIds: [1], mealPreferredTagIds: [2]);
        var filter = new PreferredTagFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    // --- DietaryRestrictionFilter ---

    [Test]
    public void DietaryRestrictionFilter_NoRestrictions_AllowsAnyRecipe()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(restrictions: []);
        var filter = new DietaryRestrictionFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void DietaryRestrictionFilter_RecipeHasMatchingTag_ReturnsTrue()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Vegan" }] };
        var ctx = EmptyContext(restrictions: ["Vegan"]);
        var filter = new DietaryRestrictionFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void DietaryRestrictionFilter_RecipeMissingRequiredTag_ReturnsFalse()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(restrictions: ["Vegan"]);
        var filter = new DietaryRestrictionFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    [Test]
    public void DietaryRestrictionFilter_MultipleRestrictions_RequiresAllTagsPresent()
    {
        var veganOnly = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Vegan" }] };
        var ctx = EmptyContext(restrictions: ["Vegan", "Gluten-Free"]);
        var filter = new DietaryRestrictionFilter();

        Assert.That(filter.Allow(veganOnly, ctx), Is.False);
    }

    [Test]
    public void DietaryRestrictionFilter_AllRestrictionsPresent_ReturnsTrue()
    {
        var recipe = new Recipe
        {
            Id = 1,
            Tags = [new Tag { Id = 1, Name = "Vegan" }, new Tag { Id = 2, Name = "Gluten-Free" }]
        };
        var ctx = EmptyContext(restrictions: ["Vegan", "Gluten-Free"]);
        var filter = new DietaryRestrictionFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    // --- TagSimilarityScorer ---

    [Test]
    public void TagSimilarityScorer_NoUpvotedRecipes_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(upvoted: []);
        var scorer = new TagSimilarityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void TagSimilarityScorer_UpvotedRecipesHaveNoTags_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var ctx = EmptyContext(upvoted: [new Recipe { Id = 2, Tags = [] }]);
        var scorer = new TagSimilarityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void TagSimilarityScorer_RecipeMatchesEntireProfile_ReturnsOne()
    {
        var ctx = EmptyContext(upvoted: [new Recipe { Id = 2, Tags = [new Tag { Id = 1, Name = "Italian" }] }]);
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Italian" }] };
        var scorer = new TagSimilarityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void TagSimilarityScorer_RecipeMatchesNoProfileTags_ReturnsZero()
    {
        var ctx = EmptyContext(upvoted: [new Recipe { Id = 2, Tags = [new Tag { Id = 1, Name = "Italian" }] }]);
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 99, Name = "Mexican" }] };
        var scorer = new TagSimilarityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void TagSimilarityScorer_RecipeWithNoTags_ReturnsZero()
    {
        var ctx = EmptyContext(upvoted: [new Recipe { Id = 2, Tags = [new Tag { Id = 1, Name = "Italian" }] }]);
        var recipe = new Recipe { Id = 1, Tags = [] };
        var scorer = new TagSimilarityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void TagSimilarityScorer_PartialProfileOverlap_ReturnsFraction()
    {
        // Profile built from one upvoted recipe carrying two equally-weighted tags.
        var ctx = EmptyContext(upvoted:
        [
            new Recipe { Id = 2, Tags = [new Tag { Id = 1 }, new Tag { Id = 2 }] }
        ]);
        var recipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1 }] };
        var scorer = new TagSimilarityScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void TagSimilarityScorer_FrequentTagOutweighsRareTag()
    {
        // Tag 1 appears in all three upvoted recipes; tag 2 appears in only one.
        var ctx = EmptyContext(upvoted:
        [
            new Recipe { Id = 2, Tags = [new Tag { Id = 1 }] },
            new Recipe { Id = 3, Tags = [new Tag { Id = 1 }] },
            new Recipe { Id = 4, Tags = [new Tag { Id = 1 }, new Tag { Id = 2 }] }
        ]);
        var scorer = new TagSimilarityScorer();
        var frequentTagRecipe = new Recipe { Id = 1, Tags = [new Tag { Id = 1 }] };
        var rareTagRecipe = new Recipe { Id = 5, Tags = [new Tag { Id = 2 }] };

        Assert.That(
            scorer.Score(frequentTagRecipe, ctx),
            Is.GreaterThan(scorer.Score(rareTagRecipe, ctx)));
    }

    // --- NutrientFitScorer ---

    [Test]
    public void NutrientFitScorer_NoMacroTargets_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [], Protein = 30, Carbs = 40, Fat = 10 };
        var ctx = EmptyContext();
        var scorer = new NutrientFitScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void NutrientFitScorer_PartialMacroTargets_ReturnsZero()
    {
        // Only protein target set — composition needs all three macros.
        var recipe = new Recipe { Id = 1, Tags = [], Protein = 30, Carbs = 40, Fat = 10 };
        var ctx = EmptyContext(proteinTarget: 50);
        var scorer = new NutrientFitScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void NutrientFitScorer_RecipeWithNoMacros_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [], Protein = 0, Carbs = 0, Fat = 0 };
        var ctx = EmptyContext(proteinTarget: 30, carbTarget: 40, fatTarget: 10);
        var scorer = new NutrientFitScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void NutrientFitScorer_RecipeMatchesTargetComposition_ReturnsOne()
    {
        var recipe = new Recipe { Id = 1, Tags = [], Protein = 30, Carbs = 40, Fat = 10 };
        var ctx = EmptyContext(proteinTarget: 30, carbTarget: 40, fatTarget: 10);
        var scorer = new NutrientFitScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void NutrientFitScorer_SameCompositionDifferentScale_ReturnsOne()
    {
        // Recipe carries double the target's macros but the same balance.
        var recipe = new Recipe { Id = 1, Tags = [], Protein = 60, Carbs = 80, Fat = 20 };
        var ctx = EmptyContext(proteinTarget: 30, carbTarget: 40, fatTarget: 10);
        var scorer = new NutrientFitScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void NutrientFitScorer_OppositeComposition_ScoresLow()
    {
        // Target is almost all protein; recipe is almost all fat.
        var recipe = new Recipe { Id = 1, Tags = [], Protein = 5, Carbs = 5, Fat = 90 };
        var ctx = EmptyContext(proteinTarget: 90, carbTarget: 5, fatTarget: 5);
        var scorer = new NutrientFitScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.LessThan(0.3f));
    }

    [Test]
    public void NutrientFitScorer_CloserCompositionScoresHigher()
    {
        var ctx = EmptyContext(proteinTarget: 30, carbTarget: 40, fatTarget: 10);
        var scorer = new NutrientFitScorer();
        var near = new Recipe { Id = 1, Tags = [], Protein = 28, Carbs = 42, Fat = 10 };
        var far = new Recipe { Id = 2, Tags = [], Protein = 5, Carbs = 5, Fat = 90 };

        Assert.That(scorer.Score(near, ctx), Is.GreaterThan(scorer.Score(far, ctx)));
    }

    // --- PantryOverlapScorer ---

    private static Recipe RecipeWithIngredients(int id, params string[] ingredientNames) =>
        new()
        {
            Id = id,
            Tags = [],
            Ingredients = ingredientNames
                .Select(n => new Ingredient
                {
                    DisplayName = n,
                    IngredientBase = new IngredientBase { Name = n },
                    Measurement = new Measurement { Name = "unit" }
                })
                .ToList()
        };

    [Test]
    public void PantryOverlapScorer_EmptyPantry_ReturnsZero()
    {
        var recipe = RecipeWithIngredients(1, "egg", "flour");
        var ctx = EmptyContext(pantryIngredientNames: []);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void PantryOverlapScorer_RecipeWithNoIngredients_ReturnsZero()
    {
        var recipe = new Recipe { Id = 1, Tags = [], Ingredients = [] };
        var ctx = EmptyContext(pantryIngredientNames: ["egg"]);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void PantryOverlapScorer_AllIngredientsInPantry_ReturnsOne()
    {
        var recipe = RecipeWithIngredients(1, "egg", "flour");
        var ctx = EmptyContext(pantryIngredientNames: ["egg", "flour"]);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void PantryOverlapScorer_NoIngredientsInPantry_ReturnsZero()
    {
        var recipe = RecipeWithIngredients(1, "egg", "flour");
        var ctx = EmptyContext(pantryIngredientNames: ["beef", "rice"]);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void PantryOverlapScorer_HalfIngredientsInPantry_ReturnsHalf()
    {
        var recipe = RecipeWithIngredients(1, "egg", "flour");
        var ctx = EmptyContext(pantryIngredientNames: ["egg"]);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void PantryOverlapScorer_NormalizesIngredientNamesBeforeMatching()
    {
        // Recipe lists plural "Eggs"/"Tomatoes"; the pantry holds the
        // singular, lowercased keys the normalizer produces.
        var recipe = RecipeWithIngredients(1, "Eggs", "Tomatoes");
        var ctx = EmptyContext(pantryIngredientNames: ["egg", "tomato"]);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void PantryOverlapScorer_CountsDistinctIngredientsOnce()
    {
        // "egg" is listed twice but counts as a single ingredient, so one of
        // two distinct ingredients matching the pantry scores 0.5.
        var recipe = RecipeWithIngredients(1, "egg", "egg", "flour");
        var ctx = EmptyContext(pantryIngredientNames: ["egg"]);
        var scorer = new PantryOverlapScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0.5f).Within(0.001f));
    }

    // --- ExcludedRecipeFilter ---

    [Test]
    public void ExcludedRecipeFilter_NoExcludedKeys_AllowsAnyRecipe()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(excludedRecipeKeys: []);
        var filter = new ExcludedRecipeFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void ExcludedRecipeFilter_RecipeKeyExcluded_ReturnsFalse()
    {
        var recipe = new Recipe { Id = 7, Tags = [] };
        var ctx = EmptyContext(excludedRecipeKeys: ["id:7"]);
        var filter = new ExcludedRecipeFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    [Test]
    public void ExcludedRecipeFilter_RecipeKeyNotExcluded_ReturnsTrue()
    {
        var recipe = new Recipe { Id = 7, Tags = [] };
        var ctx = EmptyContext(excludedRecipeKeys: ["id:99"]);
        var filter = new ExcludedRecipeFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.True);
    }

    [Test]
    public void ExcludedRecipeFilter_ExternalRecipeExcludedByUri_ReturnsFalse()
    {
        // External recipes have no database id, so they are keyed by their URI.
        var recipe = new Recipe { Id = 0, ExternalUri = "http://edamam/abc", Tags = [] };
        var ctx = EmptyContext(excludedRecipeKeys: ["uri:http://edamam/abc"]);
        var filter = new ExcludedRecipeFilter();

        Assert.That(filter.Allow(recipe, ctx), Is.False);
    }

    // --- VarietyScorer ---

    [Test]
    public void VarietyScorer_RecipeNotEatenRecently_ReturnsOne()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext();
        var scorer = new VarietyScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f));
    }

    [Test]
    public void VarietyScorer_RecipeNotInHistory_ReturnsOne()
    {
        // A different recipe was planned nearby, but not this one.
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(recentRecipeDayOffsets: new() { [99] = [1] });
        var scorer = new VarietyScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(1f));
    }

    [Test]
    public void VarietyScorer_RecipeEatenOnAdjacentDay_ReturnsZero()
    {
        // An occurrence one day away carries the full staleness weight.
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(recentRecipeDayOffsets: new() { [1] = [1] });
        var scorer = new VarietyScorer();

        Assert.That(scorer.Score(recipe, ctx), Is.EqualTo(0f));
    }

    [Test]
    public void VarietyScorer_RecipeEatenAtWindowEdge_StaysAlmostFresh()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(recentRecipeDayOffsets:
            new() { [1] = [VarietyScorer.VarietyWindowDays] });
        var scorer = new VarietyScorer();

        var score = scorer.Score(recipe, ctx);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThan(1f));
    }

    [Test]
    public void VarietyScorer_MoreRecentOccurrenceScoresLower()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var scorer = new VarietyScorer();
        var recent = EmptyContext(recentRecipeDayOffsets: new() { [1] = [2] });
        var older  = EmptyContext(recentRecipeDayOffsets: new() { [1] = [10] });

        Assert.That(scorer.Score(recipe, recent), Is.LessThan(scorer.Score(recipe, older)));
    }

    [Test]
    public void VarietyScorer_MoreOccurrencesScoreLower()
    {
        var recipe = new Recipe { Id = 1, Tags = [] };
        var scorer = new VarietyScorer();
        var once  = EmptyContext(recentRecipeDayOffsets: new() { [1] = [7] });
        var twice = EmptyContext(recentRecipeDayOffsets: new() { [1] = [7, 7] });

        Assert.That(scorer.Score(recipe, twice), Is.LessThan(scorer.Score(recipe, once)));
    }
}
