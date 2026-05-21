using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using MealPlanner.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace MealPlanner.Tests
{
    [TestFixture]
    public class ThemeTests
    {
        private MealPlannerDBContext _context;
        private UserSettingsRepository _userProfileRepository;
        private UserSettingsController _controller;
        private Mock<IUserSettingsService> _mockUserSettingsService;

        [SetUp]
        public async Task Setup()
        {
            var options = new DbContextOptionsBuilder<MealPlannerDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

            _context = new MealPlannerDBContext(options);
            _userProfileRepository = new UserSettingsRepository(_context);
            _mockUserSettingsService = new Mock<IUserSettingsService>();

            var userManagerMock = new Mock<UserManager<User>>(
                Mock.Of<IUserStore<User>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            _controller = new UserSettingsController(_userProfileRepository, new Mock<IUserSettingsService>().Object, new Mock<ITagRepository>().Object, new Mock<IUserFoodPreferenceRepository>().Object, new Mock<IUserNutritionPreferenceRepository>().Object, new Mock<IUserDietaryRestrictionRepository>().Object, userManagerMock.Object);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            _context.UserProfiles.Add(new UserProfile
            {
                UserId = "test-user-id",
            });
            await _context.SaveChangesAsync();
        }

        [TearDown]
        public void Cleanup()
        {
            _controller?.Dispose();
            _context?.Dispose();
        }

        //this also tests to make sure that it starts out false
        [Test]
        public async Task SwitchToDark()
        {
            await _controller.ThemeChange();

            var profile = await _context.UserProfiles.FirstAsync(x => x.UserId == "test-user-id");
            Assert.That(profile.IsDarkTheme, Is.True);
        }

        [Test]
        public async Task SwitchBackToLight()
        {
            await _controller.ThemeChange();

            await _controller.ThemeChange();

            var profile = await _context.UserProfiles.FirstAsync(x => x.UserId == "test-user-id");
            Assert.That(profile.IsDarkTheme, Is.False);
        }

        [Test]
        public async Task ThemeChangeNotLoggedIn()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var result = await _controller.ThemeChange();

            Assert.That(result, Is.InstanceOf<ChallengeResult>());
        }
    }
}