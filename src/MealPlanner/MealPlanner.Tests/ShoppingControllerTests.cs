using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class ShoppingControllerTests
{
    private ShoppingController _controller;
    private MealPlannerDBContext _context;
    private ClaimsPrincipal _claimsPrincipal;
    private Mock<IRegistrationService> _registrationServiceMock;
    private Mock<IShoppingListService> _shoppingListServiceMock;
    private Mock<IPantryService> _pantryServiceMock;
    private Mock<UserManager<User>> _userManagerMock;
    private User _user;

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

        _user = new User { Id = "user-1", FullName = "Gary", UserName = "gary@fakeemail.com" };
        _context.Users.Add(_user);
        _context.SaveChanges();

        _claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            "TestAuth"));

        _registrationServiceMock = new Mock<IRegistrationService>();
        _registrationServiceMock
            .Setup(r => r.FindUserByClaimAsync(_claimsPrincipal))
            .ReturnsAsync(_user);

        _shoppingListServiceMock = new Mock<IShoppingListService>();
        _pantryServiceMock = new Mock<IPantryService>();

        _userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_user);

        _controller = new ShoppingController(
            _shoppingListServiceMock.Object,
            _pantryServiceMock.Object,
            _userManagerMock.Object,
            null!,
            _registrationServiceMock.Object,
            Mock.Of<IMeasurementRepository>(),
            _context);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = _claimsPrincipal }
        };
        _controller.TempData = new TempDataDictionary(_controller.HttpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
        _context.Dispose();
    }

    // --- Scenario 1: item appears in pantry list after add ---

    [Test]
    public async Task AddPantryItem_WithValidModel_RedirectsToPantry()
    {
        var model = new PantryItemViewModel { Name = "Milk", Amount = 2f, Measurement = "cups" };
        _pantryServiceMock
            .Setup(s => s.BuildPantryItem(model.Name, model.Amount, model.Measurement))
            .Returns(new Ingredient { DisplayName = "Milk", IngredientBase = new IngredientBase { Name = "milk" }, Measurement = new Measurement { Name = "cups" }, Amount = 2f });

        var result = await _controller.AddPantryItem(model);

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Pantry"));
    }

    [Test]
    public async Task AddPantryItem_WithValidModel_DelegatesToPantryService()
    {
        var model = new PantryItemViewModel { Name = "Milk", Amount = 2f, Measurement = "cups" };
        var built = new Ingredient
        {
            DisplayName = "Milk",
            IngredientBase = new IngredientBase { Name = IngredientNameNormalizer.NormalizeKey("Milk") },
            Measurement = new Measurement { Name = "cups" },
            Amount = 2f
        };
        _pantryServiceMock
            .Setup(s => s.BuildPantryItem(model.Name, model.Amount, model.Measurement))
            .Returns(built);

        await _controller.AddPantryItem(model);

        _pantryServiceMock.Verify(s => s.AddPantryItem("user-1", built), Times.Once);
    }

    [Test]
    public async Task Pantry_WhenUserHasPantryItems_ReturnsThemInModel()
    {
        var ingredient = new Ingredient
        {
            DisplayName = "Eggs",
            IngredientBase = new IngredientBase { Name = "egg" },
            Measurement = new Measurement { Name = "pieces" },
            Amount = 12f
        };
        _pantryServiceMock
            .Setup(s => s.GetPantryItems("user-1"))
            .Returns(new List<Ingredient> { ingredient });

        var result = await _controller.Pantry() as ViewResult;

        Assert.That(result, Is.Not.Null);
        var items = result!.Model as List<Ingredient>;
        Assert.That(items, Is.Not.Null);
        Assert.That(items!, Has.Count.EqualTo(1));
        Assert.That(items![0].DisplayName, Is.EqualTo("Eggs"));
    }

    // --- Scenario 4: validation prevents empty name ---

    [Test]
    public async Task AddPantryItem_WithEmptyName_RedirectsToPantry()
    {
        var model = new PantryItemViewModel { Name = "", Amount = 3f, Measurement = "cups" };
        _controller.ModelState.AddModelError("Name", "Required");

        var result = await _controller.AddPantryItem(model);

        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Pantry"));
    }

    [Test]
    public async Task AddPantryItem_WithEmptyName_DoesNotAddPantryItem()
    {
        var model = new PantryItemViewModel { Name = "", Amount = 3f, Measurement = "cups" };
        _controller.ModelState.AddModelError("Name", "Required");

        await _controller.AddPantryItem(model);

        _pantryServiceMock.Verify(s => s.BuildPantryItem(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()), Times.Never);
    }

    // ── UpdateItemAmountJson ──────────────────────────────────────

    [Test]
    public async Task UpdateItemAmountJson_WithValidAmount_ReturnsOkAndSaves()
    {
        var result = await _controller.UpdateItemAmountJson(
            new ShoppingController.UpdateAmountRequest(10, "3"));

        Assert.That(result, Is.TypeOf<OkResult>());
        _shoppingListServiceMock.Verify(s => s.UpdateItemAmount("user-1", 10, 3f, "3"), Times.Once);
    }

    [Test]
    public async Task UpdateItemAmountJson_WithFractionalAmount_ReturnsOkAndSaves()
    {
        var result = await _controller.UpdateItemAmountJson(
            new ShoppingController.UpdateAmountRequest(10, "1 1/2"));

        Assert.That(result, Is.TypeOf<OkResult>());
        _shoppingListServiceMock.Verify(s => s.UpdateItemAmount("user-1", 10, 1.5f, "1 1/2"), Times.Once);
    }

    [Test]
    public async Task UpdateItemAmountJson_WithZeroAmount_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItemAmountJson(
            new ShoppingController.UpdateAmountRequest(10, "0"));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        _shoppingListServiceMock.Verify(s => s.UpdateItemAmount(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }

    [Test]
    public async Task UpdateItemAmountJson_WithUnparseableAmount_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItemAmountJson(
            new ShoppingController.UpdateAmountRequest(10, "count"));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        _shoppingListServiceMock.Verify(s => s.UpdateItemAmount(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }

    [Test]
    public async Task UpdateItemAmountJson_WithNegativeAmount_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItemAmountJson(
            new ShoppingController.UpdateAmountRequest(10, "-1"));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        _shoppingListServiceMock.Verify(s => s.UpdateItemAmount(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }

    [Test]
    public async Task UpdateItemAmountJson_WithNullAmount_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItemAmountJson(
            new ShoppingController.UpdateAmountRequest(10, null!));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        _shoppingListServiceMock.Verify(s => s.UpdateItemAmount(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }
}
