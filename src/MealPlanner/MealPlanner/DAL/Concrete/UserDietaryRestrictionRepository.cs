using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Concrete;

public class UserDietaryRestrictionRepository : IUserDietaryRestrictionRepository
{
    private readonly MealPlannerDBContext _context;

    public UserDietaryRestrictionRepository(MealPlannerDBContext context)
    {
        _context = context;
    }

    public async Task<List<DietaryRestriction>> GetAllDietaryRestrictionsAsync()
    {
        return await _context.DietaryRestrictions
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<List<UserDietaryRestriction>> GetByUserIdAsync(string userId)
    {
        return await _context.UserDietaryRestrictions
            .Include(x => x.DietaryRestriction)
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<List<DietaryRestriction>> GetAllAsync()
    {
        return await _context.DietaryRestrictions.OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<List<int>> GetSelectedIdsByUserIdAsync(string userId)
    {
        return await _context.UserDietaryRestrictions
            .Where(x => x.UserId == userId)
            .Select(x => x.DietaryRestrictionId)
            .ToListAsync();
    }

    public async Task SetForUserAsync(string userId, IEnumerable<int> dietaryRestrictionIds)
    {
        var existing = await _context.UserDietaryRestrictions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _context.UserDietaryRestrictions.RemoveRange(existing);

        var toAdd = dietaryRestrictionIds
            .Distinct()
            .Select(id => new UserDietaryRestriction
            {
                UserId = userId,
                DietaryRestrictionId = id
            })
            .ToList();

        await _context.UserDietaryRestrictions.AddRangeAsync(toAdd);
        await _context.SaveChangesAsync();
    }
}
