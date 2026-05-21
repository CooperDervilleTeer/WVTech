using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.Services.Recommendation;
using MealPlanner.ViewModels;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class MealRecommendationServiceTests
{
    private Mock<IUserRecipeRepository> _userRecipeRepoMock;
    private Mock<IRecommendationStream> _streamMock;
    private Mock<IUserNutritionPreferenceRepository> _nutritionRepoMock;
    private Mock<IUserDietaryRestrictionRepository> _dietaryRestrictionRepoMock;
    private Mock<IUserFoodPreferenceRepository> _foodPrefRepoMock;
    private Mock<IPantryService> _pantryServiceMock;
    private Mock<IMealRepository> _mealRepoMock;
    private Mock<ITagRepository> _tagRepoMock;
    private MealRecommendationService _service;

    private readonly User _user = new() { Id = "user-1" };

    private static Tag VeganTag => new() { Id = 1, Name = "Vegan" };
    private static Tag GlutenFreeTag => new() { Id = 2, Name = "Gluten-Free" };

    // Streams return scored candidates; tests that don't exercise scoring build
    // a list of zero-scored recipes. Equal scores keep input order (the merge
    // sorts stably), so order-sensitive tests stay valid.
    private static IReadOnlyList<ScoredRecipe> Ranked(params Recipe[] recipes) =>
        recipes.Select(r => new ScoredRecipe(r, 0f)).ToList();

    [SetUp]
    public void SetUp()
    {
        _userRecipeRepoMock = new Mock<IUserRecipeRepository>();
        _streamMock = new Mock<IRecommendationStream>();
        _nutritionRepoMock = new Mock<IUserNutritionPreferenceRepository>();
        _dietaryRestrictionRepoMock = new Mock<IUserDietaryRestrictionRepository>();
        _foodPrefRepoMock = new Mock<IUserFoodPreferenceRepository>();
        _pantryServiceMock = new Mock<IPantryService>();
        _mealRepoMock = new Mock<IMealRepository>();
        _tagRepoMock = new Mock<ITagRepository>();
        _tagRepoMock.Setup(r => r.GetTagRarityWeightsAsync())
                    .ReturnsAsync(new Dictionary<int, float>());

        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync([]);

        _userRecipeRepoMock
            .Setup(r => r.GetUserRecipesByVoteType(_user.Id, UserVoteType.UpVote))
            .ReturnsAsync([]);
        _userRecipeRepoMock
            .Setup(r => r.GetUserRecipeVoteAsync(_user.Id, It.IsAny<int>()))
            .ReturnsAsync(UserVoteType.NoVote);
        _userRecipeRepoMock
            .Setup(r => r.GetRecipeVotePercentage(It.IsAny<int>()))
            .ReturnsAsync(0f);
        _userRecipeRepoMock
            .Setup(r => r.GetUserVotesByUserIdAsync(_user.Id))
            .ReturnsAsync(new Dictionary<int, UserVoteType>());
        _userRecipeRepoMock
            .Setup(r => r.GetAllVotePercentagesAsync())
            .ReturnsAsync(new Dictionary<int, float>());
        _nutritionRepoMock
            .Setup(r => r.GetUsersNutritionPreferenceAsync(_user.Id))
            .ReturnsAsync(new UserNutritionPreference { UserId = _user.Id, CalorieTarget = 9999 });
        _dietaryRestrictionRepoMock
            .Setup(r => r.GetByUserIdAsync(_user.Id))
            .ReturnsAsync([]);
        _foodPrefRepoMock
            .Setup(r => r.GetFoodPreferenceTagIdsAsync(_user.Id))
            .ReturnsAsync([]);
        _pantryServiceMock
            .Setup(p => p.GetPantryItems(It.IsAny<string>()))
            .Returns(new List<Ingredient>());
        _mealRepoMock
            .Setup(r => r.GetUserMealsByDateRangeAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _mealRepoMock
            .Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        _service = new MealRecommendationService(
            _userRecipeRepoMock.Object,
            _nutritionRepoMock.Object,
            _dietaryRestrictionRepoMock.Object,
            _foodPrefRepoMock.Object,
            _mealRepoMock.Object,
            _tagRepoMock.Object,
            [_streamMock.Object],
            _pantryServiceMock.Object);
    }

    [Test]
    public async Task GetRecommendedMealsForUser_ExcludesDownvotedRecipes()
    {
        // Downvote filtering is the stream's responsibility; mock it returning only the allowed recipe.
        var downvoted = new Recipe { Id = 1, Name = "Downvoted", Calories = 300, Tags = [] };
        var allowed = new Recipe { Id = 2, Name = "Allowed", Calories = 300, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(allowed));

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
        };

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(result[0].Recipes, Does.Not.Contain(downvoted));
        Assert.That(result[0].Recipes, Does.Contain(allowed));
    }

    [Test]
    public async Task GetRecommendedMealsForUser_WithNoDietaryRestrictions_IncludesAllCandidates()
    {
        var recipe = new Recipe { Id = 1, Name = "Any Recipe", Calories = 300, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(recipe));

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
        };

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(result[0].Recipes, Does.Contain(recipe));
    }

    [Test]
    public async Task GetRecommendedMealsForUser_WithDietaryRestriction_IncludesMatchingRecipe()
    {
        // Restriction filtering is the stream's responsibility; mock it returning only the matching recipe.
        var veganRecipe = new Recipe { Id = 1, Name = "Vegan Dish", Calories = 300, Tags = [VeganTag] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(veganRecipe));

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
        };

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(result[0].Recipes, Does.Contain(veganRecipe));
    }

    [Test]
    public async Task GetRecommendedMealsForUser_WithDietaryRestriction_ExcludesNonMatchingRecipe()
    {
        // Restriction filtering is the stream's responsibility; mock it returning an empty list.
        var nonVeganRecipe = new Recipe { Id = 2, Name = "Beef Stew", Calories = 300, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync([]);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
        };

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(result[0].Recipes, Does.Not.Contain(nonVeganRecipe));
    }

    [Test]
    public async Task GetRecommendedMealsForUser_FeedsPriorSlotRecipesToLaterSlotExcludedKeys()
    {
        // Cross-meal de-duplication is now the ExcludedRecipeFilter's job. The
        // service's part is feeding each prior slot's composed recipes into the
        // next slot's ExcludedRecipeKeys.
        var recipe1 = new Recipe { Id = 1, Name = "Recipe 1", Calories = 100, Tags = [] };
        var recipe2 = new Recipe { Id = 2, Name = "Recipe 2", Calories = 100, Tags = [] };
        var recipe3 = new Recipe { Id = 3, Name = "Recipe 3", Calories = 100, Tags = [] };
        var captured = new List<RecommendationContext>();
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured.Add(c))
                   .ReturnsAsync(Ranked(recipe1, recipe2, recipe3));

        var config = new DayPlanConfigViewModel
        {
            MealCount = 2,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences =
            [
                new MealPreferenceViewModel { Size = MealSize.Average },
                new MealPreferenceViewModel { Size = MealSize.Average }
            ]
        };

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        var firstSlotKeys = result[0].Recipes.Select(r => $"id:{r.Id}").ToList();
        Assert.That(firstSlotKeys, Is.Not.Empty, "first slot should compose at least one recipe");
        Assert.That(captured[0].Meal.ExcludedRecipeKeys, Is.Empty, "first slot starts with nothing excluded");
        Assert.That(captured[1].Meal.ExcludedRecipeKeys, Is.SupersetOf(firstSlotKeys),
            "second slot excludes every recipe placed in the first");
    }

    [Test]
    public async Task GetRecommendedMealsForUser_SeedsExistingDayMealRecipesIntoFirstSlotExcludeKeys()
    {
        // Recipes already on the day's other meals are excluded from the very
        // first slot — the service loads day meals itself rather than asking
        // the caller for the id set.
        var dayMeal = new Meal
        {
            Id = 99, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 7 }, new Recipe { Id = 8 }]
        };
        _mealRepoMock.Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
                     .ReturnsAsync([dayMeal]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured ??= c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.Meal.ExcludedRecipeKeys, Is.SupersetOf(new[] { "id:7", "id:8" }));
    }

    [Test]
    public async Task GetRecommendedMealsForUser_SubtractsExistingMealsCaloriesFromDailyBudget()
    {
        // Daily target 1800 minus an already-planned 900-cal meal leaves 900
        // for the single slot — the bug WVT-59's scenario was trying (and
        // failing) to catch.
        SetNutritionPrefs(calories: 1800);
        var planned = new Meal
        {
            Id = 1, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 50, Calories = 900 }]
        };
        _mealRepoMock.Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
                     .ReturnsAsync([planned]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured ??= c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.Meal.CalorieTarget, Is.EqualTo(900),
            "the slot should target the budget remaining after existing meals are subtracted");
    }

    [Test]
    public async Task GetRecommendedMealsForUser_SubtractsExistingMealsMacrosFromDailyBudget()
    {
        SetNutritionPrefs(calories: 1800, protein: 100, carbs: 200, fat: 60);
        var planned = new Meal
        {
            Id = 1, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 50, Calories = 900, Protein = 40, Carbs = 80, Fat = 20 }]
        };
        _mealRepoMock.Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
                     .ReturnsAsync([planned]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured ??= c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.Meal.ProteinTarget, Is.EqualTo(60), "remaining protein = 100 - 40");
        Assert.That(captured.Meal.CarbTarget,    Is.EqualTo(120), "remaining carbs   = 200 - 80");
        Assert.That(captured.Meal.FatTarget,     Is.EqualTo(40),  "remaining fat     = 60 - 20");
    }

    [Test]
    public async Task GetRecommendedMealsForUser_ClampsRemainingBudgetToZeroWhenOverConsumed()
    {
        // If existing meals already exceed the daily target, the remaining
        // budget is clamped to zero rather than going negative (which would
        // sign-flip the composer's calorie check).
        SetNutritionPrefs(calories: 1000, protein: 50);
        var planned = new Meal
        {
            Id = 1, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 50, Calories = 1500, Protein = 80 }]
        };
        _mealRepoMock.Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
                     .ReturnsAsync([planned]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured ??= c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.Meal.CalorieTarget, Is.EqualTo(0), "calorie budget clamps at zero");
        Assert.That(captured.Meal.ProteinTarget,  Is.EqualTo(0), "macro budget clamps at zero");
    }

    [Test]
    public async Task GetRecommendedMealsForUser_ExcludeMealId_KeepsThatMealsCaloriesInTheBudget()
    {
        // RegenerateMeal passes the meal being regenerated; its calories must
        // NOT be subtracted, since its recipes are about to be replaced.
        SetNutritionPrefs(calories: 1800);
        var regenTarget = new Meal
        {
            Id = 1, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 7, Calories = 500 }]
        };
        var sibling = new Meal
        {
            Id = 2, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 8, Calories = 400 }]
        };
        _mealRepoMock.Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
                     .ReturnsAsync([regenTarget, sibling]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured ??= c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig(), excludeMealId: 1);

        Assert.That(captured!.Meal.CalorieTarget, Is.EqualTo(1400),
            "1800 - 400 (sibling only); the regen target's 500 cal must NOT be subtracted");
    }

    [Test]
    public async Task GetRecommendedMealsForUser_ExcludeMealId_OmitsThatMealsRecipesFromExcludeKeys()
    {
        var regenTarget = new Meal
        {
            Id = 1, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 7 }]
        };
        var sibling = new Meal
        {
            Id = 2, UserId = _user.Id, StartTime = DateTime.Today,
            Recipes = [new Recipe { Id = 8 }]
        };
        _mealRepoMock.Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
                     .ReturnsAsync([regenTarget, sibling]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured ??= c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig(), excludeMealId: 1);

        Assert.That(captured!.Meal.ExcludedRecipeKeys, Does.Contain("id:8"),
            "sibling meal's recipes are excluded as expected");
        Assert.That(captured.Meal.ExcludedRecipeKeys, Does.Not.Contain("id:7"),
            "the regenerated meal's own recipes are NOT excluded — they're being replaced");
    }

    [Test]
    public async Task GetRecommendedMealsForUser_WithMultipleRestrictions_RequiresAllTagsToMatch()
    {
        // Restriction filtering is the stream's responsibility; mock it returning only the fully-matching recipe.
        var veganOnly = new Recipe { Id = 1, Name = "Vegan Only", Calories = 300, Tags = [VeganTag] };
        var veganGlutenFree = new Recipe { Id = 2, Name = "Vegan GF", Calories = 300, Tags = [VeganTag, GlutenFreeTag] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(veganGlutenFree));

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
        };

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(result[0].Recipes, Does.Contain(veganGlutenFree));
        Assert.That(result[0].Recipes, Does.Not.Contain(veganOnly));
    }

    // --- Macro-target tests ---

    private static DayPlanConfigViewModel SingleMealConfig() => new()
    {
        MealCount = 1,
        SelectedMonth = DateTime.Today.Month,
        SelectedDay = DateTime.Today.Day,
        MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
    };

    private void SetNutritionPrefs(int calories = 9999, int? protein = null, int? carbs = null, int? fat = null) =>
        _nutritionRepoMock
            .Setup(r => r.GetUsersNutritionPreferenceAsync(_user.Id))
            .ReturnsAsync(new UserNutritionPreference
            {
                UserId = _user.Id,
                CalorieTarget = calories,
                ProteinTarget = protein,
                CarbTarget = carbs,
                FatTarget = fat
            });

    [Test]
    public async Task ProteinTarget_PrefersRecipeCloserToTarget()
    {
        // Soft macros: the recipe matching the protein target is chosen over
        // one that overshoots it, even though the overshooting recipe ranks
        // first. The old greedy algorithm hard-excluded any overshoot.
        SetNutritionPrefs(protein: 30);
        var overshoots = new Recipe { Id = 1, Name = "High Protein", Calories = 100, Protein = 90, Tags = [] };
        var onTarget   = new Recipe { Id = 2, Name = "Right Protein", Calories = 100, Protein = 30, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(overshoots, onTarget));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(onTarget));
        Assert.That(result[0].Recipes, Does.Not.Contain(overshoots));
    }

    [Test]
    public async Task CarbTarget_PrefersRecipeCloserToTarget()
    {
        SetNutritionPrefs(carbs: 30);
        var overshoots = new Recipe { Id = 1, Name = "High Carb", Calories = 100, Carbs = 90, Tags = [] };
        var onTarget   = new Recipe { Id = 2, Name = "Right Carb", Calories = 100, Carbs = 30, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(overshoots, onTarget));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(onTarget));
        Assert.That(result[0].Recipes, Does.Not.Contain(overshoots));
    }

    [Test]
    public async Task FatTarget_PrefersRecipeCloserToTarget()
    {
        SetNutritionPrefs(fat: 10);
        var overshoots = new Recipe { Id = 1, Name = "High Fat", Calories = 100, Fat = 30, Tags = [] };
        var onTarget   = new Recipe { Id = 2, Name = "Right Fat", Calories = 100, Fat = 10, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(overshoots, onTarget));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(onTarget));
        Assert.That(result[0].Recipes, Does.Not.Contain(overshoots));
    }

    [Test]
    public async Task AllMacroTargets_RecipeFitsAll_RecipeIncluded()
    {
        SetNutritionPrefs(calories: 500, protein: 50, carbs: 60, fat: 20);
        var balanced = new Recipe { Id = 1, Name = "Balanced", Calories = 200, Protein = 30, Carbs = 40, Fat = 10, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(balanced));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(balanced));
    }

    [Test]
    public async Task MacroStressTest_OneCal_200Protein_OneCarb_OneFat_OnlyFittingRecipeIncluded()
    {
        // Budget so extreme that only Recipe A (near-zero cal/carb/fat, high protein) survives all 4 checks.
        SetNutritionPrefs(calories: 1, protein: 200, carbs: 1, fat: 1);

        var fitsAll   = new Recipe { Id = 1, Name = "Fits",          Calories = 1,   Protein = 195, Carbs = 0, Fat = 1, Tags = [] };
        var failsCal  = new Recipe { Id = 2, Name = "Fails Calorie", Calories = 100, Protein = 10,  Carbs = 5, Fat = 5, Tags = [] };
        var failsCarb = new Recipe { Id = 3, Name = "Fails Carbs",   Calories = 0,   Protein = 5,   Carbs = 5, Fat = 0, Tags = [] };
        var failsFat  = new Recipe { Id = 4, Name = "Fails Fat",     Calories = 0,   Protein = 5,   Carbs = 0, Fat = 5, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(fitsAll, failsCal, failsCarb, failsFat));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(fitsAll),       "Recipe within all budgets should be recommended");
        Assert.That(result[0].Recipes, Does.Not.Contain(failsCal),  "Recipe exceeding calorie budget should be excluded");
        Assert.That(result[0].Recipes, Does.Not.Contain(failsCarb), "Recipe exceeding carb budget should be excluded");
        Assert.That(result[0].Recipes, Does.Not.Contain(failsFat),  "Recipe exceeding fat budget should be excluded");
    }

    [Test]
    public async Task OnlyProteinTargetSet_HighCarbAndFatNotBlocked()
    {
        SetNutritionPrefs(protein: 50); // no carb/fat targets
        var highCarbFat = new Recipe { Id = 1, Name = "High Carb Fat", Calories = 100, Protein = 30, Carbs = 999, Fat = 999, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(highCarbFat));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(highCarbFat), "Carbs/fat should be ignored when no target is set for them");
    }

    [Test]
    public async Task RunningProteinTotal_IncludesSecondRecipeWhenItImprovesMacroFit()
    {
        // Two 30g recipes total 60g — closer to the 50g target than 30g alone —
        // so the composer keeps both. The old greedy cap dropped the second.
        SetNutritionPrefs(protein: 50);
        var recipeA = new Recipe { Id = 1, Name = "Recipe A", Calories = 100, Protein = 30, Tags = [] };
        var recipeB = new Recipe { Id = 2, Name = "Recipe B", Calories = 100, Protein = 30, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(recipeA, recipeB));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(recipeA));
        Assert.That(result[0].Recipes, Does.Contain(recipeB));
    }

    [Test]
    public async Task MacroTargets_ScaleProportionallyToMealSize()
    {
        // Daily protein = 60g. Small weight=0.5, Large weight=1.5, totalWeight=2.0.
        // Small slot target: round(0.5/2.0 * 60) = 15g; Large: round(1.5/2.0 * 60) = 45g.
        SetNutritionPrefs(protein: 60);
        var captured = new List<RecommendationContext>();
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured.Add(c))
                   .ReturnsAsync([]);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 2,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences =
            [
                new MealPreferenceViewModel { Size = MealSize.Small },
                new MealPreferenceViewModel { Size = MealSize.Large }
            ]
        };

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(captured[0].Meal.ProteinTarget, Is.EqualTo(15), "Small slot protein target");
        Assert.That(captured[1].Meal.ProteinTarget, Is.EqualTo(45), "Large slot protein target");
    }

    // --- GetOneRecipeRecommendation ---

    [Test]
    public async Task GetOneRecipeRecommendation_WhenNoRecipesExist_ReturnsNull()
    {
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync([]);

        var result = await _service.GetOneRecipeRecommendation(_user, DateTime.Today, []);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetOneRecipeRecommendation_WhenRecipesAvailable_ReturnsOneRecipe()
    {
        var recipe = new Recipe { Id = 1, Name = "Pasta", Calories = 400, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>())).ReturnsAsync(Ranked(recipe));

        var result = await _service.GetOneRecipeRecommendation(_user, DateTime.Today, []);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetOneRecipeRecommendation_PassesExcludeIdsAsExcludedRecipeKeys()
    {
        // The exclude list reaches the stream as ExcludedRecipeKeys; the
        // ExcludedRecipeFilter then drops those recipes. (End-to-end exclusion
        // is covered by the filter and stream tests.)
        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetOneRecipeRecommendation(_user, DateTime.Today, [1, 5]);

        Assert.That(captured!.Meal.ExcludedRecipeKeys, Is.EquivalentTo(new[] { "id:1", "id:5" }));
    }

    [Test]
    public async Task GetOneRecipeRecommendation_PrefersUpvotedRecipes()
    {
        // Ranking is the stream's responsibility; mock it returning the upvoted recipe first.
        var upvotedRecipe = new Recipe { Id = 2, Name = "Upvoted Pasta", Calories = 400, Tags = [] };
        var normalRecipe  = new Recipe { Id = 1, Name = "Normal Salad",  Calories = 200, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(upvotedRecipe, normalRecipe));

        var result = await _service.GetOneRecipeRecommendation(_user, DateTime.Today, []);

        Assert.That(result!.Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetOneRecipeRecommendation_HonorsDietaryRestrictions()
    {
        // Restriction filtering is the stream's responsibility; mock it returning only the matching recipe.
        var veganRecipe = new Recipe { Id = 1, Name = "Vegan Bowl", Calories = 400, Tags = [VeganTag] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(veganRecipe));

        var result = await _service.GetOneRecipeRecommendation(_user, DateTime.Today, []);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetOneRecipeRecommendation_PassesEmptyMealContext()
    {
        // The single-recipe flow has no slot context, so calorie/macro targets are null
        // and the preferred-tag set is empty.
        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetOneRecipeRecommendation(_user, DateTime.Today, []);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Meal.CalorieTarget, Is.Null);
        Assert.That(captured.Meal.ProteinTarget, Is.Null);
        Assert.That(captured.Meal.CarbTarget, Is.Null);
        Assert.That(captured.Meal.FatTarget, Is.Null);
        Assert.That(captured.Meal.PreferredTagIds, Is.Empty);
        Assert.That(captured.Meal.ExcludedRecipeKeys, Is.Empty);
    }

    [Test]
    public async Task GetOneRecipeRecommendation_WithSlotTemplate_PassesMacroAndTagContext()
    {
        // Regenerating a recipe should fit the meal: the replaced recipe's
        // macros become the slot's macro targets and its tags the slot tags.
        var slotTemplate = new Recipe
        {
            Id = 10, Name = "Replaced", Protein = 40, Carbs = 50, Fat = 15,
            Tags = [new Tag { Id = 3, Name = "Italian" }]
        };
        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetOneRecipeRecommendation(_user, DateTime.Today, [], slotTemplate);

        Assert.That(captured!.Meal.ProteinTarget, Is.EqualTo(40));
        Assert.That(captured.Meal.CarbTarget, Is.EqualTo(50));
        Assert.That(captured.Meal.FatTarget, Is.EqualTo(15));
        Assert.That(captured.Meal.PreferredTagIds, Does.Contain(3));
    }

    [Test]
    public async Task GetOneRecipeRecommendation_MergesStreamsByScore_HigherScoredExternalRecipeOutranksLocal()
    {
        // Candidates from every stream are merged into one score-ranked pool, so a
        // strongly-scored external recipe outranks a weakly-scored local one even
        // though the local stream is consulted first.
        var localRecipe = new Recipe { Id = 1, Name = "Weak Local", Calories = 100, Tags = [] };
        var externalRecipe = new Recipe { Id = 0, ExternalUri = "ext-1", Name = "Strong External", Calories = 100, Tags = [] };

        var localStream = new Mock<IRecommendationStream>();
        localStream.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync([new ScoredRecipe(localRecipe, 1f)]);
        var externalStream = new Mock<IRecommendationStream>();
        externalStream.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                      .ReturnsAsync([new ScoredRecipe(externalRecipe, 9f)]);

        var service = new MealRecommendationService(
            _userRecipeRepoMock.Object,
            _nutritionRepoMock.Object,
            _dietaryRestrictionRepoMock.Object,
            _foodPrefRepoMock.Object,
            _mealRepoMock.Object,
            _tagRepoMock.Object,
            [localStream.Object, externalStream.Object],
            _pantryServiceMock.Object);

        var result = await service.GetOneRecipeRecommendation(_user, DateTime.Today, []);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Strong External"),
            "the higher-scored external recipe should outrank the local one regardless of stream order");
    }

    // --- Context construction ---

    [Test]
    public async Task BuildContext_PopulatesTagRarityWeightsFromRepository()
    {
        var weights = new Dictionary<int, float> { [1] = 2.3f, [2] = 0.4f };
        _tagRepoMock
            .Setup(r => r.GetTagRarityWeightsAsync())
            .ReturnsAsync(weights);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.User.TagRarityWeights, Is.EqualTo(weights));
    }

    [Test]
    public async Task BuildContext_PopulatesUserPreferredTagIdsFromRepository()
    {
        _foodPrefRepoMock
            .Setup(r => r.GetFoodPreferenceTagIdsAsync(_user.Id))
            .ReturnsAsync([10, 20, 30]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.User.PreferredTagIds, Is.EquivalentTo(new[] { 10, 20, 30 }));
    }

    [Test]
    public async Task BuildContext_PassesSlotTagIdsToMealContext()
    {
        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average, TagIds = [3, 7] }]
        };

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        Assert.That(captured!.Meal.PreferredTagIds, Is.EquivalentTo(new[] { 3, 7 }));
    }

    [Test]
    public async Task BuildContext_PassesMacroTargetsToMealContext()
    {
        SetNutritionPrefs(calories: 600, protein: 50, carbs: 60, fat: 20);
        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.Meal.CalorieTarget, Is.EqualTo(600));
        Assert.That(captured.Meal.ProteinTarget, Is.EqualTo(50));
        Assert.That(captured.Meal.CarbTarget, Is.EqualTo(60));
        Assert.That(captured.Meal.FatTarget, Is.EqualTo(20));
    }

    [Test]
    public async Task BuildContext_CallsStreamOncePerSlot()
    {
        var config = new DayPlanConfigViewModel
        {
            MealCount = 3,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences =
            [
                new MealPreferenceViewModel { Size = MealSize.Average },
                new MealPreferenceViewModel { Size = MealSize.Average },
                new MealPreferenceViewModel { Size = MealSize.Average }
            ]
        };

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, config);

        _streamMock.Verify(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()), Times.Exactly(3));
    }

    [Test]
    public async Task BuildContext_PopulatesPantryIngredientNamesFromPantryService()
    {
        // Pantry item names are normalized into the singular, lowercased keys
        // the scorer compares against.
        _pantryServiceMock
            .Setup(p => p.GetPantryItems(_user.Id))
            .Returns(new List<Ingredient>
            {
                new()
                {
                    DisplayName = "Eggs",
                    IngredientBase = new IngredientBase { Name = "Eggs" },
                    Measurement = new Measurement { Name = "unit" }
                },
                new()
                {
                    DisplayName = "Flour",
                    IngredientBase = new IngredientBase { Name = "Flour" },
                    Measurement = new Measurement { Name = "cup" }
                }
            });

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.User.PantryIngredientNames, Is.EquivalentTo(new[] { "egg", "flour" }));
    }

    [Test]
    public async Task BuildContext_PopulatesRecentRecipeDayOffsetsFromMealHistory()
    {
        // A meal three days before the planned date should surface its recipe
        // at day-offset 3.
        var historicRecipe = new Recipe { Id = 5, Name = "Tuesday Dinner", Tags = [] };
        var pastMeal = new Meal
        {
            Id = 1,
            UserId = _user.Id,
            StartTime = DateTime.Today.AddDays(-3),
            Recipes = [historicRecipe]
        };
        _mealRepoMock
            .Setup(r => r.GetUserMealsByDateRangeAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([pastMeal]);

        RecommendationContext? captured = null;
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .Callback<RecommendationContext>(c => captured = c)
                   .ReturnsAsync([]);

        await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(captured!.User.RecentRecipeDayOffsets.ContainsKey(5), Is.True);
        Assert.That(captured.User.RecentRecipeDayOffsets[5], Does.Contain(3));
    }

    // --- Ranking trust tests (service trusts stream order) ---

    [Test]
    public async Task UpvotedRecipe_OutranksHighCommunityVotePercentageNonUpvotedRecipe()
    {
        // Ranking is the stream's responsibility; mock it returning the upvoted recipe first.
        SetNutritionPrefs(calories: 150);
        var upvoted = new Recipe { Id = 1, Name = "Upvoted", Calories = 100, Tags = [] };
        var popular = new Recipe { Id = 2, Name = "Popular", Calories = 100, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(upvoted, popular));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(upvoted),     "Upvoted recipe should be preferred over high vote% recipe");
        Assert.That(result[0].Recipes, Does.Not.Contain(popular), "High vote% recipe should be displaced by upvoted recipe");
    }

    [Test]
    public async Task VotePercentage_HigherVotePercentageNonUpvotedRecipeSelectedFirst()
    {
        // Ranking is the stream's responsibility; mock it returning the higher-vote recipe first.
        SetNutritionPrefs(calories: 150);
        var highVote = new Recipe { Id = 1, Name = "Popular",   Calories = 100, Tags = [] };
        var lowVote  = new Recipe { Id = 2, Name = "Unpopular", Calories = 100, Tags = [] };
        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(highVote, lowVote));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes, Does.Contain(highVote),    "Recipe with higher vote% should be selected");
        Assert.That(result[0].Recipes, Does.Not.Contain(lowVote), "Recipe with lower vote% should be displaced");
    }

    // --- Soft macro composition (formerly the greedy underfill bug) ---

    [Test]
    public async Task SoftMacroFill_HighProteinRecipeDoesNotBlockSmallerRecipes()
    {
        // bigProtein (30g) alone hits the 30g target exactly, but the composer
        // still fits the two 5g recipes alongside it rather than letting one
        // recipe monopolise the meal — the greedy algorithm stopped at one.
        SetNutritionPrefs(calories: 9999, protein: 30);
        var bigProtein = new Recipe { Id = 1, Name = "Protein Shake", Calories = 100, Protein = 30, Tags = [] };
        var smallProA  = new Recipe { Id = 2, Name = "Light Snack A", Calories = 100, Protein = 5,  Tags = [] };
        var smallProB  = new Recipe { Id = 3, Name = "Light Snack B", Calories = 100, Protein = 5,  Tags = [] };

        _streamMock.Setup(s => s.GetRankedCandidatesAsync(It.IsAny<RecommendationContext>()))
                   .ReturnsAsync(Ranked(bigProtein, smallProA, smallProB));

        var result = await _service.GetRecommendedMealsForUser(_user, DateTime.Today, SingleMealConfig());

        Assert.That(result[0].Recipes.Count, Is.GreaterThanOrEqualTo(2),
            "Should not let one high-macro recipe block all remaining candidates from the meal");
    }
}
