using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Concrete;

public class TagRepository : Repository<Tag>, ITagRepository
{
    private readonly MealPlannerDBContext _context;

    public TagRepository(MealPlannerDBContext context) : base(context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns a smoothed-IDF rarity weight per tag, keyed by tag id.
    /// weight = log((N + 1) / (1 + df)) where N is the total recipe count and
    /// df is the number of recipes carrying that tag. A common tag (carried
    /// by many recipes) gets a small weight; a rare tag gets a large one.
    /// Always non-negative. Every tag in the table appears in the result —
    /// including ones currently carried by zero recipes — so the scorer can
    /// rely on the lookup rather than falling back to a default.
    /// </summary>
    public async Task<Dictionary<int, float>> GetTagRarityWeightsAsync()
    {
        int totalRecipes = await _context.Recipes.CountAsync();
        var counts = await _dbset
            .Select(t => new { t.Id, Df = t.Recipes.Count })
            .ToListAsync();
        return counts.ToDictionary(
            c => c.Id,
            c => (float)Math.Log((double)(totalRecipes + 1) / (1 + c.Df)));
    }

    public async Task<List<string>> GetTagNamesAsync()
    {
        return await _dbset
            .OrderByDescending(t => t.Recipes.Count)
            .Take(10)
            .Select(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Tag>> GetTagsByPopularityAsync()
    {
        return await _dbset
            .OrderByDescending(t => t.Recipes.Count)
            .ToListAsync();
    }

    public async Task<Tag?> FindByNameAsync(string name)
    {
        return await _dbset
            .FirstOrDefaultAsync(t => t.Name == name);
    }

    public async Task<List<Tag>> GetTagsByIdsAsync(IEnumerable<int> ids)
    {
        var idSet = ids.ToHashSet();
        if (idSet.Count == 0) return [];
        return await _dbset
            .Where(t => idSet.Contains(t.Id))
            .ToListAsync();
    }
}
