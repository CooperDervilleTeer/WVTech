using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Abstract;

public class UserNutritionPreferenceRepository: Repository<UserNutritionPreference>, IUserNutritionPreferenceRepository
{
    private readonly MealPlannerDBContext _context;

    public UserNutritionPreferenceRepository(MealPlannerDBContext context):base(context)
    {
        _context = context;
    }

    public async Task<UserNutritionPreference?> GetUsersNutritionPreferenceAsync(string userId)
    {
        return await _dbset.Where(p => p.UserId == userId).FirstOrDefaultAsync();
    }

    public async Task SaveNutritionPreferenceAsync(string userId, int? calorieTarget, int? proteinTarget, int? carbTarget, int? fatTarget)
    {
        var pref = await GetUsersNutritionPreferenceAsync(userId);
        if (pref == null)
        {
            pref = new UserNutritionPreference { UserId = userId };
            _dbset.Add(pref);
        }
        pref.CalorieTarget = calorieTarget;
        pref.ProteinTarget = proteinTarget;
        pref.CarbTarget    = carbTarget;
        pref.FatTarget     = fatTarget;
        await _context.SaveChangesAsync();
    }
}