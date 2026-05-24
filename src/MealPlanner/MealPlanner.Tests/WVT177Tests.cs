using System.Security.Claims;
using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using MealPlanner.Models.DTO;
using MealPlanner.Services;
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
public class WVT177FractionParserTests
{
    [TestCase("1/2", 0.5f)]
    [TestCase("1 1/2", 1.5f)]
    [TestCase("0.75", 0.75f)]
    [TestCase("3", 3f)]
    [TestCase("2 3/4", 2.75f)]
    [TestCase("1/4", 0.25f)]
    public void ParseAmount_ValidInput_ReturnsExpectedFloat(string input, float expected)
    {
        var result = FractionParser.ParseAmount(input);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value, Is.EqualTo(expected).Within(0.01f));
    }

    [TestCase("abc")]
    [TestCase("one half")]
    public void ParseAmount_InvalidInput_ReturnsNull(string input)
    {
        Assert.That(FractionParser.ParseAmount(input), Is.Null);
    }

    [Test]
    public void ParseAmount_NullInput_ReturnsNull()
    {
        Assert.That(FractionParser.ParseAmount(null), Is.Null);
    }

    [Test]
    public void ParseAmount_EmptyInput_ReturnsNull()
    {
        Assert.That(FractionParser.ParseAmount(""), Is.Null);
    }

    [Test]
    public void ParseAmount_NegativeNumber_ReturnsNull()
    {
        Assert.That(FractionParser.ParseAmount("-1"), Is.Null);
    }

    [TestCase(0.5f, "1/2")]
    [TestCase(1.5f, "1 1/2")]
    [TestCase(1.0f, "1")]
    [TestCase(2.0f, "2")]
    [TestCase(0.25f, "1/4")]
    [TestCase(0.75f, "3/4")]
    public void FormatAmount_CommonValues_ReturnsExpectedString(float input, string expected)
    {
        Assert.That(FractionParser.FormatAmount(input), Is.EqualTo(expected));
    }

    [Test]
    public void ParseAmount_RoundTrip_PreservesValue()
    {
        const float original = 1.5f;
        var formatted = FractionParser.FormatAmount(original);
        var parsed = FractionParser.ParseAmount(formatted);
        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.Value, Is.EqualTo(original).Within(0.01f));
    }
}

[TestFixture]
public class WVT177UpdateMeasurementTests
{
    private ShoppingController _controller = null!;
    private MealPlannerDBContext _context = null!;
    private Mock<UserManager<User>> _userManagerMock = null!;
    private readonly User _user = new() { Id = "user-1" };

    [SetUp]
    public void SetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        _context = new MealPlannerDBContext(
            new DbContextOptionsBuilder<MealPlannerDBContext>().UseSqlite(connection).Options);
        _context.Database.EnsureCreated();

        var userStore = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            userStore.Object, null, null, null, null, null, null, null, null);
        _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_user);

        _context.Users.Add(_user);
        _context.SaveChanges();

        var shoppingListService = new ShoppingListService(
            new ShoppingListRepository(_context),
            Mock.Of<IMealRepository>(),
            Mock.Of<IIngredientBaseRepository>(),
            Mock.Of<IRepository<Measurement>>());

        _controller = new ShoppingController(
            shoppingListService,
            Mock.Of<IPantryService>(),
            _userManagerMock.Object,
            null!,
            Mock.Of<IRegistrationService>(),
            new MeasurementRepository(_context),
            _context);

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
        _controller.TempData = new TempDataDictionary(_controller.HttpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
        _context.Dispose();
    }

    private (Measurement measurement, ShoppingListItem item) SeedItemWithMeasurement(string name, string abbrev)
    {
        var ingredientBase = new IngredientBase { Name = $"ingredient-{abbrev}" };
        var measurement = new Measurement { Name = name, Abbreviation = abbrev, SortOrder = 1 };
        _context.Set<IngredientBase>().Add(ingredientBase);
        _context.Set<Measurement>().Add(measurement);
        _context.SaveChanges();

        var item = new ShoppingListItem
        {
            UserId = "user-1",
            IngredientBaseId = ingredientBase.Id,
            MeasurementId = measurement.Id,
            Amount = 1f
        };
        _context.ShoppingListItems.Add(item);
        _context.SaveChanges();
        return (measurement, item);
    }

    [Test]
    public async Task UpdateItemMeasurementJson_ValidAbbreviation_ReturnsOk()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");
        var newMeasurement = new Measurement { Name = "cup", Abbreviation = "cup", SortOrder = 2 };
        _context.Set<Measurement>().Add(newMeasurement);
        _context.SaveChanges();

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, "cup"));

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_ValidAbbreviation_UpdatesItemMeasurementInDb()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");
        var newMeasurement = new Measurement { Name = "cup", Abbreviation = "cup", SortOrder = 2 };
        _context.Set<Measurement>().Add(newMeasurement);
        _context.SaveChanges();

        await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, "cup"));

        var updated = await _context.ShoppingListItems.FindAsync(item.Id);
        Assert.That(updated!.MeasurementId, Is.EqualTo(newMeasurement.Id));
    }

    [Test]
    public async Task UpdateItemMeasurementJson_ValidMeasurementName_AlsoMatches()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");
        var newMeasurement = new Measurement { Name = "tablespoon", Abbreviation = "tbsp", SortOrder = 3 };
        _context.Set<Measurement>().Add(newMeasurement);
        _context.SaveChanges();

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, "tablespoon"));

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_UnknownMeasurement_CreatesAndReturnsOk()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, "zorbatron"));

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_EmptyMeasurement_ReturnsBadRequest()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, ""));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_WhitespaceMeasurement_ReturnsBadRequest()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, "   "));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_ItemBelongsToDifferentUser_ReturnsNotFound()
    {
        var ingredientBase = new IngredientBase { Name = "other-ingredient" };
        var measurement = new Measurement { Name = "ounce", Abbreviation = "oz", SortOrder = 1 };
        _context.Set<IngredientBase>().Add(ingredientBase);
        _context.Set<Measurement>().Add(measurement);
        _context.SaveChanges();

        var otherItem = new ShoppingListItem
        {
            UserId = "other-user",
            IngredientBaseId = ingredientBase.Id,
            MeasurementId = measurement.Id,
            Amount = 1f
        };
        _context.ShoppingListItems.Add(otherItem);
        _context.SaveChanges();

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(otherItem.Id, "oz"));

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_NonexistentItem_ReturnsNotFound()
    {
        var measurement = new Measurement { Name = "ounce", Abbreviation = "oz", SortOrder = 1 };
        _context.Set<Measurement>().Add(measurement);
        _context.SaveChanges();

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(99999, "oz"));

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task UpdateItemMeasurementJson_ValidAbbreviation_ReturnsAbbreviationInBody()
    {
        var (_, item) = SeedItemWithMeasurement("ounce", "oz");
        var newMeasurement = new Measurement { Name = "cup", Abbreviation = "cup", SortOrder = 2 };
        _context.Set<Measurement>().Add(newMeasurement);
        _context.SaveChanges();

        var result = await _controller.UpdateItemMeasurementJson(
            new ShoppingController.UpdateMeasurementRequest(item.Id, "cup")) as OkObjectResult;

        Assert.That(result, Is.Not.Null);
        var body = result!.Value;
        var abbrev = body?.GetType().GetProperty("abbreviation")?.GetValue(body)?.ToString();
        Assert.That(abbrev, Is.EqualTo("cup"));
    }
}

[TestFixture]
public class WVT177KrogerExportBlockedTests
{
    private Mock<UserManager<User>> _userManagerMock = null!;
    private Mock<IKrogerExportService> _exportServiceMock = null!;
    private Mock<IKrogerService> _krogerServiceMock = null!;
    private Mock<IUserSettingsRepository> _userSettingsRepoMock = null!;
    private Mock<IShoppingListRepository> _shoppingListRepoMock = null!;
    private ShoppingListService _shoppingListService = null!;
    private KrogerController _controller = null!;
    private TestSession _session = null!;

    private const string TestUserId = "user-1";

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            userStore.Object, null, null, null, null, null, null, null, null);
        _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new User { Id = TestUserId });

        _exportServiceMock = new Mock<IKrogerExportService>();
        _krogerServiceMock = new Mock<IKrogerService>();
        _userSettingsRepoMock = new Mock<IUserSettingsRepository>();
        _userSettingsRepoMock.Setup(r => r.SaveZipCodeAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _shoppingListRepoMock = new Mock<IShoppingListRepository>();

        _shoppingListService = new ShoppingListService(
            _shoppingListRepoMock.Object,
            Mock.Of<IMealRepository>(),
            Mock.Of<IIngredientBaseRepository>(),
            Mock.Of<IRepository<Measurement>>());

        _controller = new KrogerController(
            _userSettingsRepoMock.Object,
            _shoppingListService,
            _userManagerMock.Object,
            _exportServiceMock.Object,
            _krogerServiceMock.Object);

        _session = new TestSession();
        _session.SetString("KrogerAccessToken", "valid-token");
        _session.SetString("KrogerAccessTokenExpiry",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());

        var httpContext = new DefaultHttpContext();
        httpContext.Session = _session;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, TestUserId)], "TestAuth"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown() => _controller.Dispose();

    [Test]
    public async Task Export_WhenItemHasEmptyAbbreviation_SetsKrogerErrorTempData()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "avocado" },
                Amount = 1f,
                Measurement = new Measurement { Name = "large", Abbreviation = "" }
            }]);

        await _controller.Export("97401", "store-123");

        Assert.That(_controller.TempData["KrogerError"], Is.Not.Null);
    }

    [Test]
    public async Task Export_WhenItemHasEmptyAbbreviation_RedirectsToShoppingIndex()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "avocado" },
                Amount = 1f,
                Measurement = new Measurement { Name = "large", Abbreviation = "" }
            }]);

        var result = await _controller.Export("97401", "store-123");

        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task Export_WhenItemHasEmptyAbbreviation_DoesNotCallExportService()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "avocado" },
                Amount = 1f,
                Measurement = new Measurement { Name = "large", Abbreviation = "" }
            }]);

        await _controller.Export("97401", "store-123");

        _exportServiceMock.Verify(
            s => s.RunExportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task Export_WhenItemHasNullAbbreviation_SetsKrogerErrorTempData()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "cucumber" },
                Amount = 1f,
                Measurement = new Measurement { Name = "whole", Abbreviation = null! }
            }]);

        await _controller.Export("97401", "store-123");

        Assert.That(_controller.TempData["KrogerError"], Is.Not.Null);
    }

    [Test]
    public async Task Export_WhenAllItemsHaveValidAbbreviations_CallsExportService()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "chicken" },
                Amount = 2f,
                Measurement = new Measurement { Name = "Cup(s)", Abbreviation = "cup" }
            }]);
        _exportServiceMock.Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(new KrogerExportResult { Outcome = KrogerExportOutcome.Success, ItemsAdded = 1, Skipped = [] });

        await _controller.Export("97401", "store-123");

        _exportServiceMock.Verify(
            s => s.RunExportAsync(TestUserId, "store-123", "valid-token"),
            Times.Once);
    }

    [Test]
    public async Task Export_WhenMultipleItemsHaveMissingMeasurements_ErrorMessageNamesAll()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([
                new ShoppingListItem
                {
                    UserId = TestUserId,
                    IngredientBase = new IngredientBase { Name = "avocado" },
                    Amount = 1f,
                    Measurement = new Measurement { Name = "large", Abbreviation = "" }
                },
                new ShoppingListItem
                {
                    UserId = TestUserId,
                    IngredientBase = new IngredientBase { Name = "cucumber" },
                    Amount = 1f,
                    Measurement = new Measurement { Name = "whole", Abbreviation = "" }
                }
            ]);

        await _controller.Export("97401", "store-123");

        var error = _controller.TempData["KrogerError"]?.ToString();
        Assert.That(error, Does.Contain("avocado"));
        Assert.That(error, Does.Contain("cucumber"));
    }

    private class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
