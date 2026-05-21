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

namespace MealPlanner.Tests;

[TestFixture]
public class FoodEntriesTests
{
    private FoodEntriesController _controller;

    [SetUp]
    public void SetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new MealPlannerDBContext(contextOptions);

        // Needed for SQLite in-memory: create schema
        context.Database.EnsureCreated();

        var recipeRepo = new Mock<IRecipeRepository>();
        var userRecipeRepo = new Mock<IUserRecipeRepository>();

        // Recipe WITH nutrition values
        recipeRepo
            .Setup(r => r.ReadRecipeWithIngredientsAsync(1))
            .ReturnsAsync(new Recipe
            {
                Id = 1,
                Name = "Oatmeal Cookies",
                Directions = "",
                Calories = 450,
                Protein = 12,
                Carbs = 60,
                Fat = 18
            });

        // Recipe with NO nutrition (all defaults to 0)
        recipeRepo
            .Setup(r => r.ReadRecipeWithIngredientsAsync(2))
            .ReturnsAsync(new Recipe
            {
                Id = 2,
                Name = "Plain Rice",
                Directions = ""
                // Calories/Protein/Carbs/Fat default to 0
            });

        // Not found
        recipeRepo
            .Setup(r => r.ReadRecipeWithIngredientsAsync(It.Is<int>(id => id != 1 && id != 2)))
            .ReturnsAsync((Recipe?)null);

        var nutritionService = new Mock<INutritionProgressService>();
        var registrationService = new Mock<IRegistrationService>();
        // New context instance for controller (same options)
        var controllerContext = new MealPlannerDBContext(contextOptions);
        var externalRecipeService = new Mock<IExternalRecipeService>();
        var tagRepo = new Mock<ITagRepository>();
        tagRepo.Setup(r => r.GetTagNamesAsync()).ReturnsAsync([]);
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
        _controller = new FoodEntriesController(
            recipeRepo.Object,
            tagRepo.Object,
            userRecipeRepo.Object,
            controllerContext,
            registrationService.Object,
            mockEnv.Object,
            blobContainer: null,
            externalRecipeService.Object,
            nutritionService.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [TearDown]
    public void TearDown() => _controller.Dispose();

    [Test]
    public async Task RecipesPlural_ReturnsView()
    {
        // Act
        var result = await _controller.Recipes();

        // Assert
        Assert.That(result, Is.TypeOf<ViewResult>());
    }

    [Test]
    public async Task RecipesSingular_ReturnsRecipeView_IfIdIsInRecipeRepo()
    {
        // Act
        var result = await _controller.Recipes(1);

        // Assert
        Assert.That(result, Is.TypeOf<ViewResult>());
        var view = result as ViewResult;

        Assert.That(view!.ViewName, Is.EqualTo("SingleRecipe"));
        Assert.That(view.Model, Is.TypeOf<RecipeViewModel>());
    }

    [Test]
    public async Task RecipesSingular_ReturnsNotFound_IfIdNotInRecipeRepo()
    {
        // Act
        var result = await _controller.Recipes(-1);

        // Assert
        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
    }

    [Test]
    public async Task RecipesSingular_ModelContainsNutritionValues_WhenRecipeHasNutrition()
    {
        // Act
        var result = await _controller.Recipes(1);

        // Assert
        Assert.That(result, Is.TypeOf<ViewResult>());
        var view = (ViewResult)result;

        Assert.That(view.ViewName, Is.EqualTo("SingleRecipe"));
        Assert.That(view.Model, Is.TypeOf<RecipeViewModel>());

        var model = (RecipeViewModel)view.Model!;
        Assert.That(model.Calories, Is.EqualTo(450));
        Assert.That(model.Protein, Is.EqualTo(12));
        Assert.That(model.Carbs, Is.EqualTo(60));
        Assert.That(model.Fat, Is.EqualTo(18));
    }

    [Test]
    public async Task RecipesSingular_ModelNutritionDefaultsToZero_WhenRecipeNutritionIsMissing()
    {
        // Act
        var result = await _controller.Recipes(2);

        // Assert
        Assert.That(result, Is.TypeOf<ViewResult>());
        var view = (ViewResult)result;

        Assert.That(view.ViewName, Is.EqualTo("SingleRecipe"));
        Assert.That(view.Model, Is.TypeOf<RecipeViewModel>());

        var model = (RecipeViewModel)view.Model!;
        Assert.That(model.Calories, Is.EqualTo(0));
        Assert.That(model.Protein, Is.EqualTo(0));
        Assert.That(model.Carbs, Is.EqualTo(0));
        Assert.That(model.Fat, Is.EqualTo(0));
    }
}