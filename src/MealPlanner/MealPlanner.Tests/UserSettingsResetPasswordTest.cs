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
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests
{
    [TestFixture]
    public class UserSettingsResetPasswordTests
    {
        private Mock<IUserSettingsService> _mockUserSettingsService;
        private UserSettingsController _controller;

        [SetUp]
        public void SetUp()
        {
            // Arrange: create mock service and controller
            _mockUserSettingsService = new Mock<IUserSettingsService>();
            var userManagerMock = new Mock<UserManager<User>>(
                Mock.Of<IUserStore<User>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            _controller = new UserSettingsController(null!, _mockUserSettingsService.Object, new Mock<ITagRepository>().Object, new Mock<IUserFoodPreferenceRepository>().Object, new Mock<IUserNutritionPreferenceRepository>().Object, new Mock<IUserDietaryRestrictionRepository>().Object, userManagerMock.Object);

            // Add a fake authenticated user for the controller
            var user = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "user@test.com") },
                "TestAuth"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        // ===================== GET RESET PASSWORD =====================

        [Test]
        public void ResetPassword_Get_ReturnsViewWithModel()
        {
            // Act
            var result = _controller.ResetPassword();

            // Assert
            var viewResult = result as ViewResult;
            Assert.That(viewResult, Is.Not.Null);
            Assert.That(viewResult.Model, Is.InstanceOf<UserSettingsResetPasswordViewModel>());
        }

        // ===================== POST RESET PASSWORD =====================

        [Test]
        public async Task ResetPassword_Post_WhenModelStateInvalid_ReturnsView()
        {
            // Arrange
            _controller.ModelState.AddModelError("Password", "Required");
            var model = new UserSettingsResetPasswordViewModel();

            // Act
            var result = await _controller.ResetPassword(model);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
        }

        [Test]
        public async Task ResetPassword_Post_WhenPasswordResetSucceeds_ReturnsEmptyFormWithSuccessMessage()
        {
            // Arrange
            var model = new UserSettingsResetPasswordViewModel
            {
                Password = "OldPassword123!",
                NewPassword = "NewPassword123!",
                ConfirmPassword = "NewPassword123!"
            };

            _mockUserSettingsService
                .Setup(s => s.ResetPasswordAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    model.Password,
                    model.NewPassword))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.ResetPassword(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.That(viewResult, Is.Not.Null);
            Assert.That(_controller.ViewBag.StatusMessage, Is.EqualTo("Your password has been reset successfully."));
            Assert.That(viewResult.Model, Is.InstanceOf<UserSettingsResetPasswordViewModel>());
        }

        [Test]
        public async Task ResetPassword_Post_WhenCurrentPasswordIncorrect_ReturnsViewWithError()
        {
            // Arrange
            var model = new UserSettingsResetPasswordViewModel
            {
                Password = "WrongPassword",
                NewPassword = "NewPassword123!",
                ConfirmPassword = "NewPassword123!"
            };

            var errors = new[]
            {
                new IdentityError { Description = "Incorrect current password" }
            };

            _mockUserSettingsService
                .Setup(s => s.ResetPasswordAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    model.Password,
                    model.NewPassword))
                .ReturnsAsync(IdentityResult.Failed(errors));

            // Act
            var result = await _controller.ResetPassword(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.That(viewResult, Is.Not.Null);
            Assert.That(_controller.ModelState.ContainsKey(""), Is.True);
        }
    }
}
