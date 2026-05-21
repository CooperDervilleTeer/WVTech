using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Concrete;

public class UserSettingsRepository : Repository<UserProfile>, IUserSettingsRepository
{
    private readonly MealPlannerDBContext _context;

    public UserSettingsRepository(MealPlannerDBContext context) : base(context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByUserIdAsync(string userId)
    {
        return await _dbset.FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<UserProfile> FindOrCreateAsync(string userId)
    {
        var profile = await GetByUserIdAsync(userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.Add(profile);
        }
        return profile;
    }

    public async Task ToggleDarkThemeAsync(string userId)
    {
        var profile = await GetByUserIdAsync(userId);
        if (profile != null)
        {
            if (profile.IsDarkTheme)
            {
                profile.IsDarkTheme = false;
            }
            else
            {
                profile.IsDarkTheme = true;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task SaveZipCodeAsync(string userId, string? zipCode)
    {
        var profile = await GetByUserIdAsync(userId);
        if (profile == null)
        {
            _context.Add(new UserProfile { UserId = userId, ZipCode = zipCode });
        }
        else
        {
            profile.ZipCode = zipCode;
        }
        await _context.SaveChangesAsync();
    }

    public async Task UpsertProfileAsync(string userId, string? displayHandle, bool removePhoto, string? photoData)
    {
        var profile = await GetByUserIdAsync(userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.Add(profile);
        }
        profile.DisplayHandle = string.IsNullOrWhiteSpace(displayHandle)
            ? null
            : displayHandle.Trim().TrimStart('@');
        if (removePhoto)
            profile.ProfilePictureUrl = null;
        else if (!string.IsNullOrEmpty(photoData))
            profile.ProfilePictureUrl = photoData;
        await _context.SaveChangesAsync();
    }
}