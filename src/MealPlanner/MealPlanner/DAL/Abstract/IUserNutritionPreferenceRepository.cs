using MealPlanner.Models;

namespace MealPlanner.DAL.Abstract;

public interface IUserNutritionPreferenceRepository
{
    public Task<UserNutritionPreference?> GetUsersNutritionPreferenceAsync(string userId);
    public Task SaveNutritionPreferenceAsync(string userId, int? calorieTarget, int? proteinTarget, int? carbTarget, int? fatTarget);
}