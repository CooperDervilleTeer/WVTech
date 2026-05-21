using MealPlanner.DAL.Abstract;
using MealPlanner.Helpers;
using MealPlanner.Models;
using MealPlanner.Services.Recommendation;
using MealPlanner.ViewModels;

namespace MealPlanner.Services;

public class MealRecommendationService : IMealRecommendationService
{
    private const int _MAX_RECIPES = 5;
    private IUserRecipeRepository _userRecipeRepository;
    private IUserNutritionPreferenceRepository _nutrionRepository;
    private IUserDietaryRestrictionRepository _dietaryRestrictionRepository;
    private IUserFoodPreferenceRepository _foodPreferenceRepository;
    private IMealRepository _mealRepository;
    private ITagRepository _tagRepository;
    private IReadOnlyList<IRecommendationStream> _streams;
    private IPantryService? _pantryService;

    public MealRecommendationService(
        IUserRecipeRepository userRecipeRepository,
        IUserNutritionPreferenceRepository nutritionRepository,
        IUserDietaryRestrictionRepository dietaryRestrictionRepository,
        IUserFoodPreferenceRepository foodPreferenceRepository,
        IMealRepository mealRepository,
        ITagRepository tagRepository,
        IEnumerable<IRecommendationStream> streams,
        IPantryService? pantryService = null)
    {
        _userRecipeRepository = userRecipeRepository;
        _nutrionRepository = nutritionRepository;
        _dietaryRestrictionRepository = dietaryRestrictionRepository;
        _foodPreferenceRepository = foodPreferenceRepository;
        _mealRepository = mealRepository;
        _tagRepository = tagRepository;
        _streams = streams.ToList();
        _pantryService = pantryService;
    }

    private async Task<HashSet<string>> GetRestrictionNamesAsync(string userId)
    {
        var restrictions = await _dietaryRestrictionRepository.GetByUserIdAsync(userId);
        return restrictions
            .Select(r => r.DietaryRestriction?.Name)
            .Where(n => n != null)
            .ToHashSet()!;
    }

    private async Task<UserRecommendationContext> BuildUserContextAsync(User user, DateTime date)
    {
        var restrictionNames = await GetRestrictionNamesAsync(user.Id);
        var userVotes        = await _userRecipeRepository.GetUserVotesByUserIdAsync(user.Id);
        var votePercentages  = await _userRecipeRepository.GetAllVotePercentagesAsync();
        var upvoted          = await _userRecipeRepository.GetUserRecipesByVoteType(user.Id, UserVoteType.UpVote);
        var preferredTagIds  = await _foodPreferenceRepository.GetFoodPreferenceTagIdsAsync(user.Id);
        var pantryNames      = _pantryService?.GetPantryItems(user.Id)
            .Select(i => IngredientNameNormalizer.NormalizeKey(i.IngredientBase.Name))
            .ToHashSet() ?? [];
        var recentOffsets    = await BuildRecentRecipeOffsetsAsync(user, date);
        var tagRarityWeights = await _tagRepository.GetTagRarityWeightsAsync();
        return new UserRecommendationContext(
            restrictionNames, userVotes, votePercentages, upvoted, preferredTagIds, pantryNames,
            recentOffsets, tagRarityWeights);
    }

    // Maps each recipe id to the day-distances at which it appears on the
    // user's planned meals within VarietyScorer.VarietyWindowDays either side
    // of the target date. The target date itself is skipped — repetition
    // within the day plan is the ExcludedRecipeFilter's job.
    private async Task<Dictionary<int, List<int>>> BuildRecentRecipeOffsetsAsync(User user, DateTime date)
    {
        int window = VarietyScorer.VarietyWindowDays;
        var targetDate = date.Date;
        var meals = await _mealRepository.GetUserMealsByDateRangeAsync(
            user, targetDate.AddDays(-window), targetDate.AddDays(window));

        var offsets = new Dictionary<int, List<int>>();
        foreach (var meal in meals)
        {
            foreach (int offset in OccurrenceOffsets(meal, targetDate, window))
            {
                foreach (var recipe in meal.Recipes)
                {
                    if (recipe.Id == 0) continue;
                    if (!offsets.TryGetValue(recipe.Id, out var list))
                        offsets[recipe.Id] = list = [];
                    list.Add(offset);
                }
            }
        }
        return offsets;
    }

    // Day-distances within the window on which a meal occurs — every matching
    // weekday for a weekly meal, otherwise just its scheduled date.
    private static IEnumerable<int> OccurrenceOffsets(Meal meal, DateTime targetDate, int window)
    {
        for (int d = -window; d <= window; d++)
        {
            if (d == 0) continue;
            var day = targetDate.AddDays(d);
            bool occurs = meal.RepeatRule == "Weekly"
                ? MealSchedule.RepeatMatchesDay(meal, day.DayOfWeek)
                : meal.StartTime?.Date == day;
            if (occurs) yield return Math.Abs(d);
        }
    }

    private async Task<List<Recipe>> FetchSlotCandidatesAsync(RecommendationContext ctx)
    {
        var streamResults = _streams.Select(s => s.GetRankedCandidatesAsync(ctx).Result).ToList();

        // Merge every stream's candidates into one pool ranked by score, so a
        // strongly-scored external recipe can outrank a weak local one instead
        // of always sorting behind it. Streams have already applied the
        // per-slot filters (including the ExcludedRecipeFilter); here we
        // de-duplicate across streams and re-sort by score. OrderByDescending
        // is stable, so equal scores keep their stream order (local first).
        var seenKeys = new HashSet<string>();
        var merged = new List<ScoredRecipe>();
        foreach (var streamResult in streamResults)
        {
            foreach (var scored in streamResult)
            {
                var key = RecipeKey.For(scored.Recipe);
                if (key == "uri:") continue;
                if (!seenKeys.Add(key)) continue;
                merged.Add(scored);
            }
        }

        return merged
            .OrderByDescending(x => x.Score)
            .Select(x => x.Recipe)
            .ToList();
    }

    public async Task<Recipe?> GetOneRecipeRecommendation(User user, DateTime date, IEnumerable<int> excludeRecipeIds, Recipe? slotTemplate = null)
    {
        var userCtx = await BuildUserContextAsync(user, date);
        var excludeKeys = excludeRecipeIds.Select(id => $"id:{id}").ToHashSet();
        var mealCtx = BuildReplacementSlotContext(slotTemplate, excludeKeys);
        var ctx = new RecommendationContext(userCtx, mealCtx);

        var candidates = await FetchSlotCandidatesAsync(ctx);
        return candidates.FirstOrDefault();
    }

    // Slot context for a single-recipe replacement: macro targets and tag
    // preferences are taken from the recipe being replaced, so the swap stays
    // close in nutrition and theme to what it replaces.
    private static MealRecommendationContext BuildReplacementSlotContext(
        Recipe? slotTemplate, HashSet<string> excludeKeys)
    {
        if (slotTemplate == null)
            return new MealRecommendationContext(null, null, null, null, [], excludeKeys);

        return new MealRecommendationContext(
            null,
            slotTemplate.Protein,
            slotTemplate.Carbs,
            slotTemplate.Fat,
            slotTemplate.Tags.Select(t => t.Id).ToHashSet(),
            excludeKeys);
    }

    public async Task<List<Meal>> GetRecommendedMealsForUser(User user, DateTime mealDate, DayPlanConfigViewModel config, int? excludeMealId = null)
    {
        var result = new List<Meal>();
        var preferences = config.MealPreferences.Any()
            ? new List<MealPreferenceViewModel>(config.MealPreferences)
            : Enumerable.Range(0, config.MealCount)
                .Select(_ => new MealPreferenceViewModel { Size = MealSize.Average })
                .ToList();

        var userCtx = await BuildUserContextAsync(user, mealDate);
        var nutritionPrefs = await _nutrionRepository.GetUsersNutritionPreferenceAsync(user.Id);
        var totalWeight = preferences.Sum(p => p.Size.Weight());

        // Meals already planned for the day eat into the daily macro budget
        // and lock their recipes out of new slots. RegenerateMeal opts its
        // target meal out via excludeMealId so the slot being regenerated
        // recovers its own budget and recipes.
        var plannedMeals = await _mealRepository.GetUserMealsByDateAsync(user, mealDate);
        var keptMeals = excludeMealId == null
            ? plannedMeals
            : plannedMeals.Where(m => m.Id != excludeMealId).ToList();
        var plannedRecipes = keptMeals.SelectMany(m => m.Recipes).ToList();
        int consumedCalories = plannedRecipes.Sum(r => r.Calories);
        int consumedProtein  = plannedRecipes.Sum(r => r.Protein);
        int consumedCarbs    = plannedRecipes.Sum(r => r.Carbs);
        int consumedFat      = plannedRecipes.Sum(r => r.Fat);

        int? remainingCalorieTarget = nutritionPrefs?.CalorieTarget is int cal
            ? Math.Max(0, cal - consumedCalories) : null;
        int? remainingProteinTarget = nutritionPrefs?.ProteinTarget is int p
            ? Math.Max(0, p - consumedProtein) : null;
        int? remainingCarbTarget = nutritionPrefs?.CarbTarget is int c
            ? Math.Max(0, c - consumedCarbs) : null;
        int? remainingFatTarget = nutritionPrefs?.FatTarget is int f
            ? Math.Max(0, f - consumedFat) : null;

        var usedKeys = plannedRecipes
            .Select(r => RecipeKey.For(r))
            .Where(k => k != "uri:")
            .ToHashSet();
        int mealIndex = 0;
        foreach (var pref in preferences)
        {
            double weight = pref.Size.Weight() / totalWeight;
            var calorieTarget = remainingCalorieTarget.HasValue
                ? (int)Math.Round(weight * remainingCalorieTarget.Value)
                : pref.Size.Calories();
            var proteinTarget = remainingProteinTarget.HasValue
                ? (int)Math.Round(weight * remainingProteinTarget.Value)
                : (int?)null;
            var carbTarget = remainingCarbTarget.HasValue
                ? (int)Math.Round(weight * remainingCarbTarget.Value)
                : (int?)null;
            var fatTarget = remainingFatTarget.HasValue
                ? (int)Math.Round(weight * remainingFatTarget.Value)
                : (int?)null;

            var mealCtx = new MealRecommendationContext(
                calorieTarget,
                proteinTarget,
                carbTarget,
                fatTarget,
                pref.TagIds.ToHashSet(),
                new HashSet<string>(usedKeys));
            var ctx = new RecommendationContext(userCtx, mealCtx);

            var candidates = await FetchSlotCandidatesAsync(ctx);

            var recipes = MealComposer.Compose(
                candidates, calorieTarget, _MAX_RECIPES, proteinTarget, carbTarget, fatTarget);
            foreach (var r in recipes) usedKeys.Add(RecipeKey.For(r));

            result.Add(new Meal
            {
                User = user,
                UserId = user.Id,
                Title = !string.IsNullOrWhiteSpace(pref.Title) ? pref.Title : $"Meal {++mealIndex}",
                StartTime = mealDate,
                Recipes = recipes
            });
        }

        return result;
    }
}
