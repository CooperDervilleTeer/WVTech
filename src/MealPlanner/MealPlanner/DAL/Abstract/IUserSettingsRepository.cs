using MealPlanner.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace MealPlanner.DAL.Abstract;

public interface IUserSettingsRepository : IRepository<UserProfile>
{
    public Task<UserProfile?> GetByUserIdAsync(string userId);
    public Task<UserProfile> FindOrCreateAsync(string userId);
    public Task ToggleDarkThemeAsync(string userId);
    public Task SaveZipCodeAsync(string userId, string? zipCode);
    public Task UpsertProfileAsync(string userId, string? displayHandle, bool removePhoto, string? photoData);
}