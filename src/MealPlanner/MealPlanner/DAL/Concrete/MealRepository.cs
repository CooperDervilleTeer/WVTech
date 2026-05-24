using MealPlanner.DAL.Abstract;
using MealPlanner.Helpers;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Concrete;

public class MealRepository : Repository<Meal>, IMealRepository
{
    private readonly MealPlannerDBContext _context;
    public MealRepository(MealPlannerDBContext context) : base(context)
    {
        _context = context;
    }

    public override Meal CreateOrUpdate(Meal meal)
    {
        ResolveExternalRecipes(meal);
        return base.CreateOrUpdate(meal);
    }

    // An external recipe selected into a meal is persisted as a URI-only row:
    // Edamam's TOS permits caching only the recipe URI, so no name, nutrition,
    // image or ingredients are stored — display code re-fetches the recipe
    // from Edamam by URI when needed. An already-cached row is reused
    // untouched. (Persisting the Edamam ingredient graph would also collide
    // with the IngredientBase/Measurement unique indexes, and a duplicate
    // ExternalUri violates that unique index — either aborts the meal save.)
    private void ResolveExternalRecipes(Meal meal)
    {
        var recipeSet = _context.Set<Recipe>();
        for (int i = 0; i < meal.Recipes.Count; i++)
        {
            var recipe = meal.Recipes[i];
            if (string.IsNullOrEmpty(recipe.ExternalUri)) continue;

            string uri = recipe.ExternalUri;
            var cached = recipeSet.Local.FirstOrDefault(r => r.ExternalUri == uri)
                         ?? recipeSet.FirstOrDefault(r => r.ExternalUri == uri);
            if (cached != null)
            {
                meal.Recipes[i] = cached;
                continue;
            }

            var shell = new Recipe
            {
                Name = string.Empty,
                Directions = string.Empty,
                ExternalUri = uri
            };
            recipeSet.Add(shell);
            meal.Recipes[i] = shell;
        }
    }

    // Returns the distinct set of recipe ids planned for a user on a given
    // date — sharing the same exact-date + weekly-repeat + exclusion logic as
    // GetUserMealsByDateAsync. `excludeMealId` skips a single meal (the
    // RegenerateMeal flow uses this so the meal being regenerated does not
    // suppress its own replacements).
    public async Task<HashSet<int>> GetUserRecipeIdsForDateAsync(User user, DateTime date, int? excludeMealId = null)
    {
        var meals = await GetUserMealsByDateAsync(user, date);
        return meals
            .Where(m => excludeMealId == null || m.Id != excludeMealId)
            .SelectMany(m => m.Recipes.Select(r => r.Id))
            .Where(id => id != 0)
            .ToHashSet();
    }

    public async Task<List<Meal>> GetUserMealsByDateAsync(User user, DateTime date)
    {
        var start = date;
        var end = date.AddDays(1);

        var exactDateMeals = await _dbset
            .Include(m => m.Recipes)
            .Where(m => m.UserId == user.Id && m.StartTime != null)
            .Where(m => m.StartTime >= start && m.StartTime < end)
            .ToListAsync();

        var weeklyRepeatMeals = await _dbset
            .Include(m => m.Recipes)
            .Where(m => m.UserId == user.Id && m.StartTime != null)
            .Where(m => m.RepeatRule == "Weekly")
            .ToListAsync();

        weeklyRepeatMeals = weeklyRepeatMeals
            .Where(m => MealSchedule.RepeatMatchesDay(m, date.DayOfWeek))
            .ToList();

        var excludedIds = await _context.MealExclusions
            .Where(me => me.ExclusionDate == date.Date)
            .Select(me => me.MealId)
            .ToHashSetAsync();

        var completedIds = await _context.MealCompletions
            .Where(mc => mc.CompletionDate == date.Date)
            .Select(mc => mc.MealId)
            .ToHashSetAsync();

        var meals = exactDateMeals
            .Concat(weeklyRepeatMeals)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .Where(m => !excludedIds.Contains(m.Id))
            .OrderBy(m => m.StartTime)
            .ToList();

        foreach (var meal in meals)
            meal.IsCompleted = completedIds.Contains(meal.Id);

        return meals;
    }

    public async Task<List<Meal>> GetUserMealsByDateWithIngredientsAsync(User user, DateTime date)
    {
        var start = date;
        var end = date.AddDays(1);

        var exactDateMeals = await _dbset
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .Where(m => m.UserId == user.Id && m.StartTime != null)
            .Where(m => m.StartTime >= start && m.StartTime < end)
            .ToListAsync();

        var weeklyRepeatMeals = await _dbset
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .Where(m => m.UserId == user.Id && m.StartTime != null)
            .Where(m => m.RepeatRule == "Weekly")
            .ToListAsync();

        weeklyRepeatMeals = weeklyRepeatMeals
            .Where(m => MealSchedule.RepeatMatchesDay(m, date.DayOfWeek))
            .ToList();

        var excludedIds = await _context.MealExclusions
            .Where(me => me.ExclusionDate == date.Date)
            .Select(me => me.MealId)
            .ToHashSetAsync();

        var completedIds = await _context.MealCompletions
            .Where(mc => mc.CompletionDate == date.Date)
            .Select(mc => mc.MealId)
            .ToHashSetAsync();

        var meals = exactDateMeals
            .Concat(weeklyRepeatMeals)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .Where(m => !excludedIds.Contains(m.Id))
            .OrderBy(m => m.StartTime)
            .ToList();

        foreach (var meal in meals)
            meal.IsCompleted = completedIds.Contains(meal.Id);

        return meals;
    }

    public async Task<List<Meal>> GetUserMealsByDateRangeAsync(User user, DateTime start, DateTime end)
    {
        var rangeEnd = end.AddDays(1);

        var exactMeals = await _dbset
            .Include(m => m.Recipes)
            .Where(m => m.UserId == user.Id && m.StartTime != null)
            .Where(m => m.StartTime >= start && m.StartTime < rangeEnd)
            .ToListAsync();

        var daysInRange = Enumerable.Range(0, (end.Date - start.Date).Days + 1)
            .Select(d => start.AddDays(d).DayOfWeek)
            .ToHashSet();

        var weeklyMeals = await _dbset
            .Include(m => m.Recipes)
            .Where(m => m.UserId == user.Id && m.RepeatRule == "Weekly" && m.StartTime != null)
            .Where(m => m.StartTime!.Value.Date <= end.Date)
            .ToListAsync();

        weeklyMeals = weeklyMeals
            .Where(m => daysInRange.Any(day => MealSchedule.RepeatMatchesDay(m, day)))
            .ToList();

        return exactMeals
            .Concat(weeklyMeals)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .OrderBy(m => m.StartTime)
            .ToList();
    }

    public async Task<List<Meal>> GetUserMealsByDateRangeWithIngredientsAsync(User user, DateTime start, DateTime end)
    {
        var rangeEnd = end.AddDays(1);

        var exactMeals = await _dbset
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .Where(m => m.UserId == user.Id && m.StartTime != null)
            .Where(m => m.StartTime >= start && m.StartTime < rangeEnd)
            .ToListAsync();

        var daysInRange = Enumerable.Range(0, (end.Date - start.Date).Days + 1)
            .Select(d => start.AddDays(d).DayOfWeek)
            .ToHashSet();

        var weeklyMeals = await _dbset
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .Where(m => m.UserId == user.Id && m.RepeatRule == "Weekly" && m.StartTime != null)
            .Where(m => m.StartTime!.Value.Date <= end.Date)
            .ToListAsync();

        weeklyMeals = weeklyMeals
            .Where(m => daysInRange.Any(day => MealSchedule.RepeatMatchesDay(m, day)))
            .ToList();

        return exactMeals
            .Concat(weeklyMeals)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .OrderBy(m => m.StartTime)
            .ToList();
    }

    public async Task<List<Meal>> GetDistinctUserMealsAsync(User user)
    {
        var userMeals = await _dbset
            .Include(m => m.Recipes)
            .Where(m => m.UserId == user.Id && !m.IsGenerated)
            .OrderByDescending(m => m.StartTime)
            .ToListAsync();

        return userMeals
            .GroupBy(m => m.Title)
            .Select(g => g.First())
            .OrderBy(m => m.Title)
            .ToList();
    }

    // Load recipes for a meal
    public async Task LoadRecipesAsync(Meal meal)
    {
        if (meal == null) return;
        await _context.Entry(meal).Collection(m => m.Recipes).LoadAsync();
    }

    // Update meal properties & recipes
    public async Task UpdateMealAsync(Meal meal, Meal updatedData, IEnumerable<int> recipeIds)
{
    // Update basic properties
    meal.StartTime = updatedData.StartTime;
    meal.RepeatRule = updatedData.RepeatRule;

    // Load current recipes
    await _context.Entry(meal).Collection(m => m.Recipes).LoadAsync();

    // Clear old recipes
    meal.Recipes.Clear();

    // Add selected recipes
    var recipes = await _context.Recipes
        .Where(r => recipeIds.Contains(r.Id))
        .ToListAsync();

    meal.Recipes.AddRange(recipes);

    await _context.SaveChangesAsync();
}

    public async Task<Meal?> ReadAsync(int id)
    {
        return await _dbset.FindAsync(id);
    }

    public async Task<Meal?> ReadWithIngredientsAsync(int id)
    {
        return await _dbset
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    // Get multiple meals, include recipes
    public async Task<List<Meal>> GetMealsByIdsAsync(IEnumerable<int> ids)
    {
        var idSet = ids.ToHashSet();
        return await _dbset
            .Include(m => m.Recipes)
            .Where(m => idSet.Contains(m.Id))
            .ToListAsync();
    }

    public async Task RemoveAllMealsWithSameTitleAsync(string userId, string title)
    {
        var meals = await _dbset
            .Where(m => m.UserId == userId && m.Title == title)
            .ToListAsync();
        _dbset.RemoveRange(meals);
        await _context.SaveChangesAsync();
    }
}