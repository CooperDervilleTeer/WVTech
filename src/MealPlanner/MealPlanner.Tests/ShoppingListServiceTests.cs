using System;
using System.Collections.Generic;
using System.Linq;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.DAL.Abstract;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

public class ShoppingListServiceTests
{
    private Mock<IShoppingListRepository> _repo;
    private Mock<IMealRepository> _mealRepo;
    private Mock<IIngredientBaseRepository> _ingredientBaseRepo;
    private Mock<IRepository<Measurement>> _measurementRepo;
    private ShoppingListService _service;

    private static readonly Measurement _cup = new Measurement { Id = 1, Name = "Cup", Abbreviation = "cup" };
    private static readonly Measurement _count = new Measurement { Id = 2, Name = "Count", Abbreviation = "Count" };

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<IShoppingListRepository>();
        _mealRepo = new Mock<IMealRepository>();
        _ingredientBaseRepo = new Mock<IIngredientBaseRepository>();
        _measurementRepo = new Mock<IRepository<Measurement>>();

        _ingredientBaseRepo
            .Setup(r => r.FindOrCreateByName(It.IsAny<string>()))
            .Returns((string name) => new IngredientBase { Name = IngredientNameNormalizer.NormalizeKey(name) });

        _measurementRepo
            .Setup(r => r.ReadAll())
            .Returns(new List<Measurement> { _cup, _count });

        _repo.Setup(r => r.GetByUserId(It.IsAny<string>())).Returns(new List<ShoppingListItem>());
        _repo.Setup(r => r.GetDismissedIngredientBaseIds(It.IsAny<string>())).Returns(new HashSet<int>());

        _service = new ShoppingListService(_repo.Object, _mealRepo.Object, _ingredientBaseRepo.Object, _measurementRepo.Object);
    }

    private static Meal MealWithRecipe(Recipe recipe, int mealId = 0) =>
        new Meal { Id = mealId, UserId = "user-1", Recipes = new List<Recipe> { recipe } };

    private static Recipe RecipeWithIngredient(int recipeId, IngredientBase ingredientBase, Measurement measurement, float amount) =>
        new Recipe
        {
            Id = recipeId,
            Name = $"Recipe{recipeId}",
            Ingredients = new List<Ingredient>
            {
                new Ingredient { IngredientBase = ingredientBase, Measurement = measurement, Amount = amount }
            }
        };

    [Test]
    public async Task SyncFromDateRange_TwoMealsSameRecipe_DoublesIngredientAmount()
    {
        var ingredientBase = new IngredientBase { Id = 1, Name = "flour" };
        var recipe = RecipeWithIngredient(10, ingredientBase, _cup, 1f);
        var meals = new List<Meal>
        {
            MealWithRecipe(recipe, mealId: 1),
            MealWithRecipe(recipe, mealId: 2),
        };
        _mealRepo
            .Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(meals);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, DateTime.Today, DateTime.Today.AddDays(1));

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Name == "flour" && i.Amount == 2f
        )), Times.Once);
    }

    [Test]
    public async Task SyncFromDateRange_TwoMealsDifferentRecipesSameIngredient_SumsAmounts()
    {
        var ingredientBase = new IngredientBase { Id = 1, Name = "sugar" };
        var recipeA = RecipeWithIngredient(10, ingredientBase, _cup, 1f);
        var recipeB = RecipeWithIngredient(11, ingredientBase, _cup, 2f);
        var meals = new List<Meal>
        {
            MealWithRecipe(recipeA, mealId: 1),
            MealWithRecipe(recipeB, mealId: 2),
        };
        _mealRepo
            .Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(meals);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, DateTime.Today, DateTime.Today.AddDays(1));

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Name == "sugar" && i.Amount == 3f
        )), Times.Once);
    }

    [Test]
    public async Task SyncFromDateRange_OneMeal_AddsIngredientOnce()
    {
        var ingredientBase = new IngredientBase { Id = 1, Name = "egg" };
        var recipe = RecipeWithIngredient(10, ingredientBase, _count, 2f);
        _mealRepo
            .Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Meal> { MealWithRecipe(recipe, mealId: 1) });

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, DateTime.Today, DateTime.Today);

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Name == "egg" && i.Amount == 2f
        )), Times.Once);
    }

    [Test]
    public async Task SyncFromDateRange_NoMeals_AddsNothing()
    {
        _mealRepo
            .Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Meal>());

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, DateTime.Today, DateTime.Today);

        _repo.Verify(r => r.Add(It.IsAny<ShoppingListItem>()), Times.Never);
    }

    [Test]
    public async Task SyncFromDateRange_ManualItemAlreadyCoversIngredient_SkipsAutoAdd()
    {
        var ingredientBase = new IngredientBase { Id = 1, Name = "butter" };
        var recipe = RecipeWithIngredient(10, ingredientBase, _cup, 1f);
        _mealRepo
            .Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Meal> { MealWithRecipe(recipe, mealId: 1) });
        _repo.Setup(r => r.GetByUserId("user-1")).Returns(new List<ShoppingListItem>
        {
            new ShoppingListItem
            {
                UserId = "user-1",
                IngredientBase = ingredientBase,
                MeasurementId = _cup.Id,
                IsAutoAdded = false
            }
        });

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, DateTime.Today, DateTime.Today);

        _repo.Verify(r => r.Add(It.IsAny<ShoppingListItem>()), Times.Never);
    }

    [Test]
    public void AddItem_ShouldAddShoppingListItem_WhenNameIsValid()
    {
        _service.AddItem("user-1", "Milk", 1, "cup");

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.UserId == "user-1" &&
            i.IngredientBase.Name == IngredientNameNormalizer.NormalizeKey("Milk")
        )), Times.Once);
    }

    [Test]
    public void AddItem_ShouldNormalizeIngredientBaseName_WhenNameHasWhitespaceAndIsPlural()
    {
        _service.AddItem("user-1", "  Eggs  ", 1, "cup");

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Name == IngredientNameNormalizer.NormalizeKey("Eggs")
        )), Times.Once);
    }

    [Test]
    public void AddItem_ShouldThrowArgumentException_WhenNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.AddItem("user-1", "", 1, ""));
        _repo.Verify(r => r.Add(It.IsAny<ShoppingListItem>()), Times.Never);
    }

    [Test]
    public void AddItem_ShouldThrowArgumentException_WhenNameIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => _service.AddItem("user-1", "   ", 1, ""));
        _repo.Verify(r => r.Add(It.IsAny<ShoppingListItem>()), Times.Never);
    }

    [Test]
    public void RemoveItem_ShouldCallRepositoryRemove_WhenItemExists()
    {
        _service.RemoveItem(5, "user-1");

        _repo.Verify(r => r.Remove(5, "user-1"), Times.Once);
    }

    [Test]
    public void RemoveItem_ShouldThrowArgumentException_WhenItemIdIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => _service.RemoveItem(0, "user-1"));
        _repo.Verify(r => r.Remove(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void UpdateItemAmount_ShouldCallRepositoryUpdate_WhenValid()
    {
        _service.UpdateItemAmount("user-1", 42, 3.5f);

        _repo.Verify(r => r.UpdateAmountByIngredientBase("user-1", 42, 3.5f), Times.Once);
    }

    [Test]
    public void UpdateItemAmount_ShouldThrowArgumentException_WhenAmountIsNegative()
    {
        // Confirm the same id/user succeeds with a valid amount, proving the negative case is not a false positive
        _service.UpdateItemAmount("user-1", 42, 1f);
        _repo.Verify(r => r.UpdateAmountByIngredientBase("user-1", 42, 1f), Times.Once);
        _repo.Invocations.Clear();

        Assert.Throws<ArgumentException>(() => _service.UpdateItemAmount("user-1", 42, -1f));
        _repo.Verify(r => r.UpdateAmountByIngredientBase(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }

    [Test]
    public void UpdateItemAmount_ShouldThrowArgumentException_WhenAmountIsZero()
    {
        Assert.Throws<ArgumentException>(() => _service.UpdateItemAmount("user-1", 42, 0f));
        _repo.Verify(r => r.UpdateAmountByIngredientBase(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }

    [Test]
    public void GetItemsForUser_ShouldReturnOnlyThatUsersItems()
    {
        var ingredientBase = new IngredientBase { Id = 1, Name = "milk" };
        var measurement = new Measurement { Id = 1, Name = "Cup(s)" };
        var expectedItems = new List<ShoppingListItem>
        {
            new ShoppingListItem { Id = 1, UserId = "user-1", IngredientBase = ingredientBase, Measurement = measurement },
            new ShoppingListItem { Id = 2, UserId = "user-1", IngredientBase = new IngredientBase { Id = 2, Name = "egg" }, Measurement = measurement }
        };

        _repo.Setup(r => r.GetByUserId("user-1")).Returns(expectedItems);

        var result = _service.GetItemsForUser("user-1");

        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.All(item => item.UserId == "user-1"), Is.True);
    }
}
