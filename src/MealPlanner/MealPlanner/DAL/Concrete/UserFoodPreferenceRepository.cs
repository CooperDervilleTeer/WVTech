using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Concrete;

public class UserFoodPreferenceRepository : Repository<UserFoodPreference>, IUserFoodPreferenceRepository
{
    private readonly MealPlannerDBContext _context;
    private readonly ITagRepository _tagRepository;

    public UserFoodPreferenceRepository(MealPlannerDBContext context, ITagRepository tagRepository) : base(context)
    {
        _context = context;
        _tagRepository = tagRepository;
    }

    public async Task<List<string>> GetFoodPreferenceNamesAsync(string userId)
        => await _dbset
            .Where(p => p.UserId == userId)
            .Include(p => p.Tag)
            .Select(p => p.Tag.Name)
            .ToListAsync();

    public async Task<HashSet<int>> GetFoodPreferenceTagIdsAsync(string userId)
        => (await _dbset
            .Where(p => p.UserId == userId)
            .Select(p => p.TagId)
            .ToListAsync())
            .ToHashSet();

    public async Task AddFoodPreferencesAsync(string userId, List<string> tagNames)
    {
        var existingTagIds = await _dbset
            .Where(p => p.UserId == userId)
            .Select(p => p.TagId)
            .ToListAsync();

        foreach (var name in tagNames)
        {
            var tag = await _tagRepository.FindByNameAsync(name)
                      ?? _tagRepository.CreateOrUpdate(new Tag { Name = name });

            if (!existingTagIds.Contains(tag.Id))
            {
                var pref = tag.Id > 0
                    ? new UserFoodPreference { UserId = userId, TagId = tag.Id }
                    : new UserFoodPreference { UserId = userId, Tag = tag };
                _dbset.Add(pref);
            }
        }
        await _context.SaveChangesAsync();
    }

    public async Task RemoveFoodPreferenceAsync(string userId, string tagName)
    {
        var tag = await _tagRepository.FindByNameAsync(tagName);
        if (tag == null) return;

        var pref = await _dbset.FindAsync(userId, tag.Id);
        if (pref != null) _dbset.Remove(pref);
        await _context.SaveChangesAsync();
    }
}
