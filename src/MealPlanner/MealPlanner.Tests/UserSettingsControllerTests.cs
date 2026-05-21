using System.Data.Common;
using System.Security.Claims;
using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class UserSettingsControllerTests
{
    private DbConnection _connection = null!;
    private DbContextOptions<MealPlannerDBContext> _contextOptions;
    private Mock<IUserFoodPreferenceRepository> _foodPrefRepoMock = null!;
    private Mock<ITagRepository> _tagRepoMock = null!;
    private Mock<IUserNutritionPreferenceRepository> _nutritionPrefRepoMock = null!;
    private Mock<IUserDietaryRestrictionRepository> _dietaryRestrictionRepoMock = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new MealPlannerDBContext(_contextOptions);
        ctx.Database.EnsureCreated();

        _foodPrefRepoMock = new Mock<IUserFoodPreferenceRepository>();
        _tagRepoMock = new Mock<ITagRepository>();
        _tagRepoMock.Setup(r => r.GetTagNamesAsync()).ReturnsAsync([]);
        _nutritionPrefRepoMock = new Mock<IUserNutritionPreferenceRepository>();
        _nutritionPrefRepoMock.Setup(r => r.GetUsersNutritionPreferenceAsync(It.IsAny<string>())).ReturnsAsync((UserNutritionPreference?)null);
        _dietaryRestrictionRepoMock = new Mock<IUserDietaryRestrictionRepository>();
        _dietaryRestrictionRepoMock.Setup(r => r.GetAllDietaryRestrictionsAsync()).ReturnsAsync([]);
        _dietaryRestrictionRepoMock.Setup(r => r.GetByUserIdAsync(It.IsAny<string>())).ReturnsAsync([]);
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    MealPlannerDBContext CreateContext() => new(_contextOptions);

    private UserSettingsController CreateController(string userId = "user-1")
    {
        var userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var controller = new UserSettingsController(
            new Mock<IUserSettingsRepository>().Object,
            new Mock<IUserSettingsService>().Object,
            _tagRepoMock.Object,
            _foodPrefRepoMock.Object,
            _nutritionPrefRepoMock.Object,
            _dietaryRestrictionRepoMock.Object,
            userManagerMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth"))
            }
        };
        return controller;
    }

    // ── Index ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Index_ReturnsViewWithFoodPreferenceViewModel()
    {
        _foodPrefRepoMock.Setup(r => r.GetFoodPreferenceNamesAsync("user-1")).ReturnsAsync([]);

        var result = await CreateController().Index() as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Model, Is.InstanceOf<FoodPreferenceViewModel>());
    }

    [Test]
    public async Task Index_PopulatesAvailableTagsFromTagRepository()
    {
        _foodPrefRepoMock.Setup(r => r.GetFoodPreferenceNamesAsync("user-1")).ReturnsAsync([]);
        _tagRepoMock.Setup(r => r.GetTagNamesAsync()).ReturnsAsync(["Italian", "Cheap"]);

        var result = await CreateController().Index() as ViewResult;
        var vm = result!.Model as FoodPreferenceViewModel;

        Assert.That(vm!.AvailableTags, Is.EquivalentTo(new[] { "Italian", "Cheap" }));
    }

    [Test]
    public async Task Index_PopulatesCurrentPreferencesFromFoodPrefRepository()
    {
        _foodPrefRepoMock.Setup(r => r.GetFoodPreferenceNamesAsync("user-1")).ReturnsAsync(["Italian"]);

        var result = await CreateController().Index() as ViewResult;
        var vm = result!.Model as FoodPreferenceViewModel;

        Assert.That(vm!.CurrentPreferences, Contains.Item("Italian"));
    }

    // ── SaveFoodPreferences ──────────────────────────────────────────────────

    [Test]
    public async Task SaveFoodPreferences_CallsAddFoodPreferencesWithCorrectArgs()
    {
        _foodPrefRepoMock.Setup(r => r.AddFoodPreferencesAsync("user-1", It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);

        var vm = new FoodPreferenceViewModel { NewPreferences = ["Italian"] };
        await CreateController().SaveFoodPreferences(vm);

        _foodPrefRepoMock.Verify(r => r.AddFoodPreferencesAsync("user-1", It.Is<List<string>>(l => l.Contains("Italian"))), Times.Once);
    }

    [Test]
    public async Task SaveFoodPreferences_DoesNotCallAddWhenNewPreferencesEmpty()
    {
        var vm = new FoodPreferenceViewModel { NewPreferences = [] };
        await CreateController().SaveFoodPreferences(vm);

        _foodPrefRepoMock.Verify(r => r.AddFoodPreferencesAsync(It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Test]
    public async Task SaveFoodPreferences_RedirectsToIndex()
    {
        _foodPrefRepoMock.Setup(r => r.AddFoodPreferencesAsync(It.IsAny<string>(), It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController().SaveFoodPreferences(new FoodPreferenceViewModel { NewPreferences = ["Italian"] });

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
        Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Index"));
    }

    // ── RemoveFoodPreference ─────────────────────────────────────────────────

    [Test]
    public async Task RemoveFoodPreference_CallsRemoveWithCorrectArgs()
    {
        _foodPrefRepoMock.Setup(r => r.RemoveFoodPreferenceAsync("user-1", "Italian"))
            .Returns(Task.CompletedTask);

        await CreateController().RemoveFoodPreference("Italian");

        _foodPrefRepoMock.Verify(r => r.RemoveFoodPreferenceAsync("user-1", "Italian"), Times.Once);
    }

    [Test]
    public async Task RemoveFoodPreference_RedirectsToIndex()
    {
        _foodPrefRepoMock.Setup(r => r.RemoveFoodPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController().RemoveFoodPreference("Italian");

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
        Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Index"));
    }
}
