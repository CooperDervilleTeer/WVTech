using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class MealControllerTests
{
    private MealController _controller;
    private MealPlannerDBContext _context;
    private ClaimsPrincipal _user;

    private Mock<IMealRepository> _mealRepoMock;
    private Mock<IRecipeRepository> _recipeRepoMock;
    private Mock<IRegistrationService> _registrationServiceMock;
    private Mock<IMealRecommendationService> _reccServiceMock;
    private Mock<ITagRepository> _tagRepoMock;

    [SetUp]
    public void SetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(connection)
            .Options;

        _context = new MealPlannerDBContext(contextOptions);
        _context.Database.EnsureCreated();

        _user = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "testuser")
            },
            "TestAuth"));

        _registrationServiceMock = new Mock<IRegistrationService>();
        _registrationServiceMock.Setup(r => r.FindUserByClaimAsync(_user))
            .ReturnsAsync(new User { Id = "user-1", FullName = "testuser" });

        _recipeRepoMock = new Mock<IRecipeRepository>();
        _mealRepoMock = new Mock<IMealRepository>();
        _mealRepoMock
            .Setup(r => r.GetUserMealsByDateAsync(It.IsAny<User>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _mealRepoMock
            .Setup(r => r.GetUserRecipeIdsForDateAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
            .ReturnsAsync([]);
        _reccServiceMock = new Mock<IMealRecommendationService>();
        _tagRepoMock = new Mock<ITagRepository>();
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([]);

        _controller = new MealController(
            _registrationServiceMock.Object,
            _recipeRepoMock.Object,
            _mealRepoMock.Object,
            _context,
            _tagRepoMock.Object,
            _reccServiceMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = _user }
        };

        _controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
        _context.Dispose();
    }

    [Test]
    public async Task ViewMeal_WhenMealExistsForUser_ReturnsViewResult()
    {
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = new List<Recipe>() };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);

        var result = await _controller.ViewMeal(1);

        Assert.That(result, Is.TypeOf<ViewResult>());
    }

    [Test]
    public async Task ViewMeal_WhenMealExistsForUser_ReturnsMealAsModel()
    {
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = new List<Recipe>() };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);

        var result = await _controller.ViewMeal(1) as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Model, Is.TypeOf<Meal>());

        var model = (Meal)result.Model!;
        Assert.That(model.Id, Is.EqualTo(1));
        Assert.That(model.UserId, Is.EqualTo("user-1"));
    }

    [Test]
    public async Task ViewMeal_IncludesRecipes()
    {
        var recipe1 = new Recipe { Id = 1, Name = "Recipe 1", Directions = "D1" };
        var recipe2 = new Recipe { Id = 2, Name = "Recipe 2", Directions = "D2" };
        var meal = new Meal { Id = 1, UserId = "user-1" };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal))
            .Callback(() => meal.Recipes = new List<Recipe> { recipe1, recipe2 })
            .Returns(Task.CompletedTask);

        var result = await _controller.ViewMeal(1) as ViewResult;
        var model = (Meal)result!.Model!;

        Assert.That(model.Recipes, Is.Not.Null);
        Assert.That(model.Recipes.Count, Is.EqualTo(2));
        Assert.That(model.Recipes[0].Name, Is.EqualTo("Recipe 1"));
        Assert.That(model.Recipes[1].Name, Is.EqualTo("Recipe 2"));
    }

    [Test]
    public async Task ViewMeal_WhenMealDoesNotExist_ReturnsNotFound()
    {
        _mealRepoMock.Setup(r => r.ReadAsync(999)).ReturnsAsync((Meal)null);

        var result = await _controller.ViewMeal(999);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task ViewMeal_WhenMealBelongsToDifferentUser_ReturnsNotFound()
    {
        var meal = new Meal { Id = 1, UserId = "other-user", Recipes = new List<Recipe>() };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);

        var result = await _controller.ViewMeal(1);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task EditMeal_Get_WhenMealExistsForUser_ReturnsViewResult()
    {
        var meal = new Meal
        {
            Id = 10,
            UserId = "user-1",
            Recipes = new List<Recipe>()
        };

        _mealRepoMock.Setup(r => r.ReadAsync(10)).ReturnsAsync(meal);

        var result = await _controller.EditMeal(10);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var viewResult = (ViewResult)result;
        Assert.That(viewResult.Model, Is.TypeOf<EditMealViewModel>());
        var model = (EditMealViewModel)viewResult.Model!;
        Assert.That(model.Id, Is.EqualTo(10));
    }

    [Test]
    public async Task EditMeal_Get_WhenMealDoesNotExist_ReturnsNotFound()
    {
        _mealRepoMock.Setup(r => r.ReadAsync(999)).ReturnsAsync((Meal)null);

        var result = await _controller.EditMeal(999);
        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GenerateMeal_RedirectsToViewMeal_ForNewMeal()
    {
        // Arrange
        CreateMealViewModel vm = new CreateMealViewModel()
        {
            Title = "Test",
            SelectedMonth = 1,
            SelectedDay = 1
        };

        Meal meal = new Meal()
        {
            Id = 100,
            Title = vm.Title,
            StartTime = new DateTime(DateTime.Today.Year, vm.SelectedMonth, vm.SelectedDay)
        };

        _reccServiceMock.Setup(s => s.GetRecommendedMealsForUser(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([new Meal { Recipes = [new Recipe()] }]);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(It.IsAny<Meal>())).Returns(meal);

        // Act
        var result = await _controller.GenerateMeal(vm);

        // Assert
        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        var redirectResult = result as RedirectToActionResult;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(redirectResult!.ActionName, Is.EqualTo("ViewMeal"));
            Assert.That(redirectResult.RouteValues!["id"], Is.EqualTo(meal.Id));
        }
    }

    [Test]
    public async Task GenerateMeal_SetsIsGeneratedTrue_OnCreatedMeal()
    {
        var vm = new CreateMealViewModel { Title = "Test", SelectedMonth = 1, SelectedDay = 1 };
        Meal? captured = null;
        _reccServiceMock.Setup(s => s.GetRecommendedMealsForUser(
                It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([new Meal { Recipes = [new Recipe()] }]);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(It.IsAny<Meal>()))
            .Callback<Meal>(m => captured = m)
            .Returns<Meal>(m => { m.Id = 1; return m; });

        await _controller.GenerateMeal(vm);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.IsGenerated, Is.True);
    }

    [Test]
    public async Task SelectMeal_Get_ReturnsView_WithDistinctUserMeals()
    {
        var meals = new List<Meal>
        {
            new Meal { Id = 1, UserId = "user-1", Title = "Breakfast" },
            new Meal { Id = 2, UserId = "user-1", Title = "Lunch" }
        };

        _mealRepoMock.Setup(r => r.GetDistinctUserMealsAsync(It.IsAny<User>()))
            .ReturnsAsync(meals);

        var result = await _controller.SelectMeal();

        Assert.That(result, Is.TypeOf<ViewResult>());
        var view = (ViewResult)result;
        Assert.That(view.Model, Is.TypeOf<MealPlanner.ViewModels.SelectMealViewModel>());
        var model = (MealPlanner.ViewModels.SelectMealViewModel)view.Model!;
        Assert.That(model.Meals.Count, Is.EqualTo(2));
        Assert.That(model.Meals[0].Title, Is.EqualTo("Breakfast"));
        Assert.That(model.SelectedDate.Date, Is.EqualTo(DateTime.Today));
    }

    [Test]
    public async Task AddMealToDay_WhenMealBelongsToUser_ClonesToToday_AndRedirectsToHomeIndex()
    {
        var source = new Meal
        {
            Id = 5,
            UserId = "user-1",
            Title = "FavMeal",
            StartTime = new DateTime(2020, 1, 1),
            RepeatRule = null,
            Recipes = new List<Recipe> { new Recipe { Id = 7, Name = "R", Directions = "D" } }
        };

        _mealRepoMock.Setup(r => r.ReadAsync(5)).ReturnsAsync(source);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(source)).Returns(Task.CompletedTask);

        Meal? captured = null;
        _mealRepoMock.Setup(r => r.CreateOrUpdate(It.IsAny<Meal>()))
            .Callback<Meal>(m => captured = m)
            .Returns<Meal>(m => m);

        var result = await _controller.AddMealToDay(5);

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Home"));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Title, Is.EqualTo("FavMeal"));
        Assert.That(captured.UserId, Is.EqualTo("user-1"));
        Assert.That(captured.StartTime!.Value.Date, Is.EqualTo(DateTime.Today));
        Assert.That(captured.Recipes.Count, Is.EqualTo(1));
        Assert.That(captured.Recipes[0].Id, Is.EqualTo(7));
    }

    [Test]
    public async Task AddMealToDay_WhenMealNotFound_ReturnsNotFound()
    {
        _mealRepoMock.Setup(r => r.ReadAsync(999)).ReturnsAsync((Meal)null!);

        var result = await _controller.AddMealToDay(999);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task AddMealToDay_WhenMealBelongsToOtherUser_ReturnsNotFound()
    {
        var source = new Meal { Id = 5, UserId = "other-user", Title = "X", Recipes = new List<Recipe>() };
        _mealRepoMock.Setup(r => r.ReadAsync(5)).ReturnsAsync(source);

        var result = await _controller.AddMealToDay(5);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    // RemoveMealTemplate

    [Test]
    public async Task RemoveMealTemplate_WhenMealBelongsToUser_DeletesAllMealsWithSameTitle_AndRedirectsToSelectMeal()
    {
        _context.Users.Add(new User
        {
            Id = "user-1",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            Email = "test@test.com",
            NormalizedEmail = "TEST@TEST.COM",
            ConcurrencyStamp = "stamp",
            SecurityStamp = "sec-stamp"
        });
        _context.Meals.AddRange(
            new Meal { Id = 10, UserId = "user-1", Title = "Breakfast" },
            new Meal { Id = 11, UserId = "user-1", Title = "Breakfast" },
            new Meal { Id = 12, UserId = "user-1", Title = "Lunch" }
        );
        _context.SaveChanges();

        _mealRepoMock.Setup(r => r.ReadAsync(10))
            .ReturnsAsync(new Meal { Id = 10, UserId = "user-1", Title = "Breakfast" });

        _mealRepoMock.Setup(r => r.RemoveAllMealsWithSameTitleAsync("user-1", "Breakfast"))
            .Callback<string, string>((uid, title) =>
            {
                var toRemove = _context.Meals.Where(m => m.UserId == uid && m.Title == title).ToList();
                _context.Meals.RemoveRange(toRemove);
                _context.SaveChanges();
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.RemoveMealTemplate(10, null);

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("SelectMeal"));

        var remaining = _context.Meals.ToList();
        Assert.That(remaining.Count, Is.EqualTo(1));
        Assert.That(remaining[0].Title, Is.EqualTo("Lunch"));
    }

    [Test]
    public async Task RemoveMealTemplate_WhenMealNotFound_ReturnsNotFound()
    {
        _mealRepoMock.Setup(r => r.ReadAsync(999)).ReturnsAsync((Meal)null!);

        var result = await _controller.RemoveMealTemplate(999, null);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task RemoveMealTemplate_WhenMealBelongsToDifferentUser_ReturnsNotFound()
    {
        var otherMeal = new Meal { Id = 20, UserId = "other-user", Title = "Stolen" };
        _mealRepoMock.Setup(r => r.ReadAsync(20)).ReturnsAsync(otherMeal);

        var result = await _controller.RemoveMealTemplate(20, null);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    // MealSize enum

    [Test]
    public void MealSize_SnackSizes_AreIdentifiedAsSnacks()
    {
        Assert.That(MealSize.SmallSnack.IsSnack(), Is.True);
        Assert.That(MealSize.LargeSnack.IsSnack(), Is.True);
    }

    [Test]
    public void MealSize_MealSizes_AreNotIdentifiedAsSnacks()
    {
        Assert.That(MealSize.Small.IsSnack(), Is.False);
        Assert.That(MealSize.Average.IsSnack(), Is.False);
        Assert.That(MealSize.Large.IsSnack(), Is.False);
    }

    // NewMeal GET

    [Test]
    public async Task NewMeal_Get_PassesAvailableTagsToViewBag()
    {
        var tags = new List<Tag> { new Tag { Id = 1, Name = "Vegan" } };
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync(tags);

        var result = (ViewResult)await _controller.NewMeal((string?)null);

        Assert.That(_controller.ViewBag.AvailableTags, Is.Not.Null);
        Assert.That(((List<Tag>)_controller.ViewBag.AvailableTags).Count, Is.EqualTo(1));
    }

    // GenerateDayPlan POST

    [Test]
    public async Task GenerateDayPlan_Post_CallsRecommendationServiceWithConfig()
    {
        var config = new DayPlanConfigViewModel
        {
            MealCount = 3,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences =
            [
                new MealPreferenceViewModel { Size = MealSize.Average },
                new MealPreferenceViewModel { Size = MealSize.Small },
                new MealPreferenceViewModel { Size = MealSize.Large }
            ]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(
                It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([]);

        await _controller.GenerateDayPlan(config);

        _reccServiceMock.Verify(
            s => s.GetRecommendedMealsForUser(
                It.IsAny<User>(), It.IsAny<DateTime>(), config),
            Times.Once);
    }

    [Test]
    public async Task GenerateDayPlan_Post_RedirectsToDayPlanSummary()
    {
        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average }]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(
                It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([]);

        var result = await _controller.GenerateDayPlan(config);

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("DayPlanSummary"));
    }

    // DayPlanSummary GET

    [Test]
    public async Task DayPlanSummary_WithNoTempData_RedirectsToHomeIndex()
    {
        var result = await _controller.DayPlanSummary(DateTime.Today.ToString("yyyy-MM-dd"));

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Home"));
    }

    [Test]
    public async Task DayPlanSummary_WithTempData_CallsGetMealsByIdsAsync()
    {
        _controller.TempData["GeneratedMealIds"] = "10,20";
        _mealRepoMock
            .Setup(r => r.GetMealsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync([]);
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([]);

        await _controller.DayPlanSummary(DateTime.Today.ToString("yyyy-MM-dd"));

        _mealRepoMock.Verify(
            r => r.GetMealsByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 10, 20 }))),
            Times.Once);
    }

    [Test]
    public async Task DayPlanSummary_WithTempData_IncludesAvailableTagsInViewModel()
    {
        _controller.TempData["GeneratedMealIds"] = "1";
        var tags = new List<Tag> { new Tag { Id = 3, Name = "Vegan" } };
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync(tags);
        _mealRepoMock
            .Setup(r => r.GetMealsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync([]);

        var result = (ViewResult)await _controller.DayPlanSummary(DateTime.Today.ToString("yyyy-MM-dd"));
        var model = (DayPlanSummaryViewModel)result.Model!;

        Assert.That(model.AvailableTags, Is.Not.Empty);
        Assert.That(model.AvailableTags.First().Name, Is.EqualTo("Vegan"));
    }

    // Custom tag resolution

    [Test]
    public async Task GenerateDayPlan_Post_WithCustomTagName_ResolvesTagAndAddsToPreferences()
    {
        var tag = new Tag { Id = 42, Name = "Vegan" };
        _tagRepoMock.Setup(r => r.FindByNameAsync("Vegan")).ReturnsAsync(tag);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average, CustomTagName = "Vegan" }]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([]);

        await _controller.GenerateDayPlan(config);

        Assert.That(config.MealPreferences[0].TagIds, Does.Contain(42));
    }

    [Test]
    public async Task GenerateDayPlan_Post_WithUnknownCustomTagName_DoesNotAddTag()
    {
        _tagRepoMock.Setup(r => r.FindByNameAsync("Unknown")).ReturnsAsync((Tag?)null);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average, CustomTagName = "Unknown" }]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([]);

        await _controller.GenerateDayPlan(config);

        Assert.That(config.MealPreferences[0].TagIds, Is.Empty);
    }

    [Test]
    public async Task GenerateDayPlan_Post_WithMatchingTitle_AddsMatchingTagToPreferences()
    {
        var tag = new Tag { Id = 7, Name = "Vegan" };
        _tagRepoMock.Setup(r => r.FindByNameAsync("Vegan")).ReturnsAsync(tag);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average, Title = "Vegan" }]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([]);

        await _controller.GenerateDayPlan(config);

        Assert.That(config.MealPreferences[0].TagIds, Does.Contain(7));
    }

    [Test]
    public async Task GenerateDayPlan_Post_WithNonMatchingTitle_DoesNotAddTag()
    {
        _tagRepoMock.Setup(r => r.FindByNameAsync("Weekend Brunch")).ReturnsAsync((Tag?)null);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences = [new MealPreferenceViewModel { Size = MealSize.Average, Title = "Weekend Brunch" }]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([]);

        await _controller.GenerateDayPlan(config);

        Assert.That(config.MealPreferences[0].TagIds, Is.Empty);
    }

    [Test]
    public async Task GenerateDayPlan_SetsIsGeneratedTrue_OnAllGeneratedMeals()
    {
        var config = new DayPlanConfigViewModel
        {
            MealCount = 2,
            SelectedMonth = DateTime.Today.Month,
            SelectedDay = DateTime.Today.Day,
            MealPreferences =
            [
                new MealPreferenceViewModel { Size = MealSize.Average },
                new MealPreferenceViewModel { Size = MealSize.Average }
            ]
        };

        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(
                It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>()))
            .ReturnsAsync([new Meal { Title = "Lunch" }, new Meal { Title = "Dinner" }]);

        var capturedMeals = new List<Meal>();
        _mealRepoMock.Setup(r => r.CreateOrUpdate(It.IsAny<Meal>()))
            .Callback<Meal>(m => capturedMeals.Add(m))
            .Returns<Meal>(m => m);

        await _controller.GenerateDayPlan(config);

        Assert.That(capturedMeals, Is.Not.Empty);
        Assert.That(capturedMeals.All(m => m.IsGenerated), Is.True);
    }

    // RegenerateRecipe

    [Test]
    public async Task RegenerateRecipe_WhenMealNotFound_ReturnsNotFound()
    {
        _mealRepoMock.Setup(r => r.ReadAsync(99)).ReturnsAsync((Meal)null!);

        var result = await _controller.RegenerateRecipe(99, 1);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task RegenerateRecipe_WhenMealBelongsToDifferentUser_ReturnsNotFound()
    {
        var meal = new Meal { Id = 1, UserId = "other-user", Recipes = [] };
        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);

        var result = await _controller.RegenerateRecipe(1, 10);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task RegenerateRecipe_WhenNoAlternativeAvailable_ReturnsNoAlternativeJson()
    {
        var recipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [recipe] };
        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _reccServiceMock
            .Setup(s => s.GetOneRecipeRecommendation(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((Recipe?)null);

        var result = await _controller.RegenerateRecipe(1, 10);

        var json = result as JsonResult;
        Assert.That(json, Is.Not.Null);
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(json!.Value));
        Assert.That(doc.RootElement.GetProperty("noAlternative").GetBoolean(), Is.True);
    }

    [Test]
    public async Task RegenerateRecipe_WhenReplacementFound_ReturnsNewRecipeJson()
    {
        var oldRecipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [oldRecipe] };
        var newRecipe = new Recipe { Id = 20, Name = "New Recipe", Calories = 400 };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _reccServiceMock
            .Setup(s => s.GetOneRecipeRecommendation(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(newRecipe);

        var result = await _controller.RegenerateRecipe(1, 10);

        var json = result as JsonResult;
        Assert.That(json, Is.Not.Null);
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(json!.Value));
        Assert.That(doc.RootElement.GetProperty("newRecipe").GetProperty("Id").GetInt32(), Is.EqualTo(20));
    }

    [Test]
    public async Task RegenerateRecipe_WhenReplacementFound_ReplacedRecipeIdReturnedInJson()
    {
        var oldRecipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [oldRecipe] };
        var newRecipe = new Recipe { Id = 20, Name = "New Recipe", Calories = 400 };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _reccServiceMock
            .Setup(s => s.GetOneRecipeRecommendation(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(newRecipe);

        var result = await _controller.RegenerateRecipe(1, 10);

        var json = result as JsonResult;
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(json!.Value));
        Assert.That(doc.RootElement.GetProperty("replacedRecipeId").GetInt32(), Is.EqualTo(10));
    }

    [Test]
    public async Task RegenerateRecipe_WhenReplacementFound_CallsSaveChanges()
    {
        var oldRecipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [oldRecipe] };
        var newRecipe = new Recipe { Id = 20, Name = "New Recipe", Calories = 400 };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _reccServiceMock
            .Setup(s => s.GetOneRecipeRecommendation(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(newRecipe);

        await _controller.RegenerateRecipe(1, 10);

        _mealRepoMock.Verify(r => r.CreateOrUpdate(meal), Times.Once);
    }

    [Test]
    public async Task RegenerateRecipe_ExcludesAllCurrentRecipesFromRecommendation()
    {
        var recipe1 = new Recipe { Id = 10, Name = "Recipe 1", Calories = 200 };
        var recipe2 = new Recipe { Id = 11, Name = "Recipe 2", Calories = 200 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [recipe1, recipe2] };
        var newRecipe = new Recipe { Id = 20, Name = "New Recipe", Calories = 300 };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _reccServiceMock
            .Setup(s => s.GetOneRecipeRecommendation(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(newRecipe);

        await _controller.RegenerateRecipe(1, 10);

        _reccServiceMock.Verify(s => s.GetOneRecipeRecommendation(
            It.IsAny<User>(),
            It.IsAny<DateTime>(),
            It.Is<IEnumerable<int>>(ids => ids.Contains(10) && ids.Contains(11))),
            Times.Once);
    }

    [Test]
    public async Task RegenerateRecipe_PassesReplacedRecipeAsSlotTemplate()
    {
        var oldRecipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [oldRecipe] };
        var newRecipe = new Recipe { Id = 20, Name = "New Recipe", Calories = 400 };
        var loadedTemplate = new Recipe { Id = 10, Name = "Old Recipe", Protein = 30 };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _recipeRepoMock.Setup(r => r.ReadRecipeWithIngredientsAsync(10)).ReturnsAsync(loadedTemplate);
        _reccServiceMock
            .Setup(s => s.GetOneRecipeRecommendation(
                It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>(),
                It.IsAny<Recipe>()))
            .ReturnsAsync(newRecipe);

        await _controller.RegenerateRecipe(1, 10);

        _reccServiceMock.Verify(s => s.GetOneRecipeRecommendation(
            It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<int>>(),
            It.Is<Recipe>(r => r != null && r.Id == 10)),
            Times.Once);
    }

    // RegenerateMeal

    [Test]
    public async Task RegenerateMeal_PassesMealIdToExcludeItFromTheRecommendation()
    {
        // The service owns the load-the-day-and-exclude-other-meals logic;
        // the controller just tells it which meal is being regenerated.
        var meal = new Meal { Id = 1, UserId = "user-1", StartTime = DateTime.Today, Recipes = [] };
        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(It.IsAny<Meal>())).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(It.IsAny<Meal>())).Returns(meal);
        _reccServiceMock
            .Setup(s => s.GetRecommendedMealsForUser(
                It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>(),
                It.IsAny<int?>()))
            .ReturnsAsync([]);

        await _controller.RegenerateMeal(1, new MealPreferenceViewModel { Size = MealSize.Average });

        _reccServiceMock.Verify(s => s.GetRecommendedMealsForUser(
            It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DayPlanConfigViewModel>(),
            1),
            Times.Once);
    }

    // SwapRecipe

    [Test]
    public async Task SwapRecipe_WhenMealNotFound_ReturnsNotFound()
    {
        _mealRepoMock.Setup(r => r.ReadAsync(99)).ReturnsAsync((Meal)null!);

        var result = await _controller.SwapRecipe(99, 1, 2);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task SwapRecipe_WhenMealBelongsToDifferentUser_ReturnsNotFound()
    {
        var meal = new Meal { Id = 1, UserId = "other-user", Recipes = [] };
        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);

        var result = await _controller.SwapRecipe(1, 10, 20);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task SwapRecipe_RemovesOldRecipeAndAddsNewOne()
    {
        var oldRecipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var newRecipe = new Recipe { Id = 20, Name = "Restored Recipe", Calories = 250 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [oldRecipe] };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _recipeRepoMock.Setup(r => r.Read(20)).Returns(newRecipe);

        await _controller.SwapRecipe(1, 10, 20);

        Assert.That(meal.Recipes.Any(r => r.Id == 10), Is.False);
        Assert.That(meal.Recipes.Any(r => r.Id == 20), Is.True);
    }

    [Test]
    public async Task SwapRecipe_WhenReplacementFound_ReturnsJson()
    {
        var oldRecipe = new Recipe { Id = 10, Name = "Old Recipe", Calories = 300 };
        var newRecipe = new Recipe { Id = 20, Name = "Restored Recipe", Calories = 250 };
        var meal = new Meal { Id = 1, UserId = "user-1", Recipes = [oldRecipe] };

        _mealRepoMock.Setup(r => r.ReadAsync(1)).ReturnsAsync(meal);
        _mealRepoMock.Setup(r => r.LoadRecipesAsync(meal)).Returns(Task.CompletedTask);
        _mealRepoMock.Setup(r => r.CreateOrUpdate(meal)).Returns(meal);
        _recipeRepoMock.Setup(r => r.Read(20)).Returns(newRecipe);

        var result = await _controller.SwapRecipe(1, 10, 20);

        var json = result as JsonResult;
        Assert.That(json, Is.Not.Null);
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(json!.Value));
        Assert.That(doc.RootElement.GetProperty("restoredRecipe").GetProperty("Id").GetInt32(), Is.EqualTo(20));
    }

    // DeleteMeal

    private void SetUpContextUser()
    {
        _context.Users.Add(new User
        {
            Id = "user-1",
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            Email = "test@test.com",
            NormalizedEmail = "TEST@TEST.COM",
            ConcurrencyStamp = "stamp",
            SecurityStamp = "sec-stamp"
        });
        _context.SaveChanges();
    }

    [Test]
    public async Task DeleteMeal_NonWeeklyMeal_KeepsMealInDatabase()
    {
        SetUpContextUser();
        var meal = new Meal { UserId = "user-1", Title = "Lunch", StartTime = DateTime.Today };
        _context.Meals.Add(meal);
        _context.SaveChanges();

        await _controller.DeleteMeal(meal.Id, DateTime.Today.ToString("yyyy-MM-dd"), "home");

        Assert.That(_context.Meals.Any(m => m.Id == meal.Id), Is.True);
    }

    [Test]
    public async Task DeleteMeal_NonWeeklyMeal_AddsDateExclusion()
    {
        SetUpContextUser();
        var meal = new Meal { UserId = "user-1", Title = "Lunch", StartTime = DateTime.Today };
        _context.Meals.Add(meal);
        _context.SaveChanges();

        await _controller.DeleteMeal(meal.Id, DateTime.Today.ToString("yyyy-MM-dd"), "home");

        Assert.That(_context.MealExclusions.Any(e => e.MealId == meal.Id && e.ExclusionDate == DateTime.Today), Is.True);
    }

    [Test]
    public async Task DeleteMeal_NonWeeklyMeal_RedirectsToHomeIndex_WhenSourceIsHome()
    {
        SetUpContextUser();
        var meal = new Meal { UserId = "user-1", Title = "Lunch", StartTime = DateTime.Today };
        _context.Meals.Add(meal);
        _context.SaveChanges();

        var result = await _controller.DeleteMeal(meal.Id, DateTime.Today.ToString("yyyy-MM-dd"), "home");

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Home"));
    }

    [Test]
    public async Task DeleteMeal_WeeklyMeal_WithDeleteAll_RemovesMealFromDatabase()
    {
        SetUpContextUser();
        var meal = new Meal { UserId = "user-1", Title = "Weekly Lunch", StartTime = DateTime.Today, RepeatRule = "Weekly" };
        _context.Meals.Add(meal);
        _context.SaveChanges();
        var savedId = meal.Id;

        await _controller.DeleteMeal(savedId, DateTime.Today.ToString("yyyy-MM-dd"), "home", deleteAll: true);

        Assert.That(_context.Meals.Any(m => m.Id == savedId), Is.False);
    }

    [Test]
    public async Task DeleteMeal_WeeklyMeal_WithoutDeleteAll_KeepsMealAndAddsExclusion()
    {
        SetUpContextUser();
        var meal = new Meal { UserId = "user-1", Title = "Weekly Lunch", StartTime = DateTime.Today, RepeatRule = "Weekly" };
        _context.Meals.Add(meal);
        _context.SaveChanges();

        await _controller.DeleteMeal(meal.Id, DateTime.Today.ToString("yyyy-MM-dd"), "home", deleteAll: false);

        Assert.That(_context.Meals.Any(m => m.Id == meal.Id), Is.True);
        Assert.That(_context.MealExclusions.Any(e => e.MealId == meal.Id), Is.True);
    }
}