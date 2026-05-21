using MealPlanner.Models;
using MealPlanner.ViewModels;

namespace MealPlanner.Services;

public interface IMealRecommendationService
{
    public Task<List<Meal>> GetRecommendedMealsForUser(User user, DateTime mealDate, DayPlanConfigViewModel config, int? excludeMealId = null);
    public Task<Recipe?> GetOneRecipeRecommendation(User user, DateTime date, IEnumerable<int> excludeRecipeIds, Recipe? slotTemplate = null);
}