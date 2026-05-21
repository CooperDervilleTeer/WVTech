using MealPlanner.Models;

namespace MealPlanner.DAL.Abstract;

public interface IUserDietaryRestrictionRepository
{
    Task<List<DietaryRestriction>> GetAllDietaryRestrictionsAsync();
    Task<List<UserDietaryRestriction>> GetByUserIdAsync(string userId);
    Task SetForUserAsync(string userId, IEnumerable<int> dietaryRestrictionIds);
    Task<List<DietaryRestriction>> GetAllAsync();
    Task<List<int>> GetSelectedIdsByUserIdAsync(string userId);
}
