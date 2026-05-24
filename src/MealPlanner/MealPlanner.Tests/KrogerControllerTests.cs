using System.Security.Claims;
using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Models.DTO;
using MealPlanner.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class KrogerControllerTests
{
    private Mock<UserManager<User>> _userManagerMock;
    private Mock<IKrogerExportService> _exportServiceMock;
    private Mock<IKrogerService> _krogerServiceMock;
    private Mock<IUserSettingsRepository> _userSettingsRepoMock;
    private Mock<IShoppingListRepository> _shoppingListRepoMock;
    private ShoppingListService _shoppingListService;
    private KrogerController _controller;
    private TestSession _session;

    private const string TestUserId = "user-1";

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
    }

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
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem { UserId = TestUserId, IngredientBase = new IngredientBase { Name = "chicken broth" }, Amount = 2, Measurement = new Measurement { Name = "Cup(s)", Abbreviation = "cup" } }]);

        _shoppingListService = new ShoppingListService(_shoppingListRepoMock.Object, Mock.Of<IMealRepository>(), Mock.Of<IIngredientBaseRepository>(), Mock.Of<IRepository<Measurement>>());

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

    [Test]
    public async Task Export_SetsTempDataSuccess_WhenServiceReturnsSuccess()
    {
        _exportServiceMock.Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(new KrogerExportResult { Outcome = KrogerExportOutcome.Success, ItemsAdded = 1, Skipped = [] });

        await _controller.Export("97401", "store-123");

        Assert.That(_controller.TempData["KrogerSuccess"], Is.Not.Null);
    }

    [Test]
    public async Task Export_SetsTempDataError_WhenServiceReturnsNoMatchesFound()
    {
        _exportServiceMock.Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(KrogerExportResult.Of(KrogerExportOutcome.NoMatchesFound));

        await _controller.Export("97401", "store-123");

        Assert.That(_controller.TempData["KrogerError"], Is.Not.Null);
    }

    [Test]
    public async Task Export_SetsTempDataError_WhenServiceReturnsSearchTokenFailed()
    {
        _exportServiceMock.Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(KrogerExportResult.Of(KrogerExportOutcome.SearchTokenFailed));

        await _controller.Export("97401", "store-123");

        Assert.That(_controller.TempData["KrogerError"], Is.Not.Null);
    }

    [Test]
    public async Task Export_RemovesSessionToken_WhenServiceReturnsExportFailed()
    {
        _exportServiceMock.Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(KrogerExportResult.Of(KrogerExportOutcome.ExportFailed));

        await _controller.Export("97401", "store-123");

        Assert.That(_session.GetString("KrogerAccessToken"), Is.Null);
    }

    [Test]
    public async Task Export_RedirectsToConnect_WhenTokenExpired()
    {
        _session.SetString("KrogerAccessTokenExpiry",
            DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString());

        var result = await _controller.Export("97401", "store-123");

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
        Assert.That(((RedirectToActionResult)result).ActionName, Is.EqualTo("Connect"));
        _exportServiceMock.Verify(s => s.RunExportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
