using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace MealPlanner.Tests;

[TestFixture]
public class WVT51NutritionSummaryTests
{
    private FoodEntriesController _controller;
    private Mock<INutritionProgressService> _nutritionServiceMock;

    private static readonly MacroTargets _dailyTargets = new(2000, 50, 250, 70);

    private static readonly NutritionProgressDto _rangeDto = new(
        UserId: "user-1",
        StartDay: DateOnly.FromDateTime(DateTime.Today.AddDays(-29)),
        EndDay: DateOnly.FromDateTime(DateTime.Today),
        Targets: _dailyTargets,
        Totals: new MacroTotals(42000, 1050, 5400, 1500)
    );

    private static readonly List<DailyNutritionDto> _thirtyDays =
        Enumerable.Range(0, 30)
            .Select(i => new DailyNutritionDto(
                DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i)),
                i % 2 == 0 ? 1800 : 500,
                20, 60, 15))
            .ToList();

    [SetUp]
    public void SetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(connection)
            .Options;

        using var seedContext = new MealPlannerDBContext(contextOptions);
        seedContext.Database.EnsureCreated();

        _nutritionServiceMock = new Mock<INutritionProgressService>();

        _nutritionServiceMock
            .Setup(s => s.GetRangeProgressAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(_rangeDto);

        _nutritionServiceMock
            .Setup(s => s.GetDailyBreakdownAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(_thirtyDays);

        var controllerContext = new MealPlannerDBContext(contextOptions);

        _controller = new FoodEntriesController(
            new Mock<IRecipeRepository>().Object,
            new Mock<ITagRepository>().Object,
            new Mock<IUserRecipeRepository>().Object,
            controllerContext,
            new Mock<IRegistrationService>().Object,
            Mock.Of<IMeasurementRepository>(),
            new Mock<IWebHostEnvironment>().Object,
            nutritionProgressService: _nutritionServiceMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }, "TestAuth"))
            }
        };
    }

    [TearDown]
    public void TearDown() => _controller.Dispose();

    [Test]
    public async Task NutritionSummary_ReturnsView_WhenServiceIsPresent()
    {
        var result = await _controller.NutritionSummary();

        Assert.That(result, Is.TypeOf<ViewResult>());
    }

    [Test]
    public async Task NutritionSummary_ActiveTab_IsWeekly_ByDefault()
    {
        var result = (ViewResult)await _controller.NutritionSummary();
        var model  = (NutritionSummaryViewModel)result.Model!;

        Assert.That(model.ActiveTab, Is.EqualTo("weekly"));
    }

    [Test]
    public async Task NutritionSummary_ActiveTab_IsMonthly_WhenTabParamIsMonthly()
    {
        var result = (ViewResult)await _controller.NutritionSummary(tab: "monthly");
        var model  = (NutritionSummaryViewModel)result.Model!;

        Assert.That(model.ActiveTab, Is.EqualTo("monthly"));
    }

    [Test]
    public async Task NutritionSummary_AlwaysFetches30Days()
    {
        await _controller.NutritionSummary();

        _nutritionServiceMock.Verify(
            s => s.GetDailyBreakdownAsync(
                "user-1",
                It.Is<DateOnly>(d => d == DateOnly.FromDateTime(DateTime.Today.AddDays(-29))),
                It.Is<DateOnly>(d => d == DateOnly.FromDateTime(DateTime.Today))),
            Times.Once);
    }

    [Test]
    public async Task NutritionSummary_AllDays_Has30Entries()
    {
        var result = (ViewResult)await _controller.NutritionSummary();
        var model  = (NutritionSummaryViewModel)result.Model!;

        Assert.That(model.AllDays, Has.Count.EqualTo(30));
    }

    [Test]
    public async Task NutritionSummary_DailyTargets_MatchServiceResponse()
    {
        var result = (ViewResult)await _controller.NutritionSummary();
        var model  = (NutritionSummaryViewModel)result.Model!;

        Assert.That(model.DailyTargets.Calories, Is.EqualTo(_dailyTargets.Calories));
        Assert.That(model.DailyTargets.Protein,  Is.EqualTo(_dailyTargets.Protein));
    }

    [Test]
    public async Task NutritionSummary_Returns500_WhenServiceIsNull()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(connection).Options;
        using var ctx = new MealPlannerDBContext(contextOptions);
        ctx.Database.EnsureCreated();

        var controller = new FoodEntriesController(
            new Mock<IRecipeRepository>().Object,
            new Mock<ITagRepository>().Object,
            new Mock<IUserRecipeRepository>().Object,
            ctx,
            new Mock<IRegistrationService>().Object,
            Mock.Of<IMeasurementRepository>(),
            new Mock<IWebHostEnvironment>().Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }, "TestAuth"))
            }
        };

        var result = await controller.NutritionSummary();

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(500));
    }
}
