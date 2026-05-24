using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace MealPlanner.Tests
{
    [TestFixture]
    public class EditRecipeTests
    {
        private MealPlannerDBContext _context;
        private RecipeRepository _recipeRepository;
        private FoodEntriesController _controller;
        private Mock<IUserRecipeRepository> _userRecipeRepo;
        private Mock<IRegistrationService> _registrationService;

        [SetUp]
        public async Task Setup()
        {
            var options = new DbContextOptionsBuilder<MealPlannerDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

            _context = new MealPlannerDBContext(options);
            _recipeRepository = new RecipeRepository(_context);
            _userRecipeRepo = new Mock<IUserRecipeRepository>();
            var tagRepo = new Mock<ITagRepository>();
            tagRepo.Setup(r => r.GetTagNamesAsync()).ReturnsAsync([]);
            _registrationService = new Mock<IRegistrationService>();
            var externalRecipeService = new Mock<IExternalRecipeService>();
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
            _controller = new FoodEntriesController(_recipeRepository, tagRepo.Object, _userRecipeRepo.Object, _context, _registrationService.Object, Mock.Of<IMeasurementRepository>(), mockEnv.Object, blobContainer: null, externalRecipeService.Object);

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "test-user-id") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var vm = new RecipeViewModel
            {
                Name = "Test Recipe",
                Ingredients = ["sugar", "flour"],
                IngredientAmounts = ["1", "2"],
                IngredientMeasurements = ["cup", "cups"],
                Directions = "Mix ingredients and bake 20 mins",
                Calories = 0,
                Protein = 1,
                Carbs = 2,
                Fat = 3,
            };

            await _controller.RecipeAdded(vm);
        }

        [TearDown]
        public void Cleanup()
        {
            _controller?.Dispose();
            _context?.Dispose();
        }

        [Test]
        public async Task EditTest()
        {
            var vm = new RecipeViewModel
            {
                Name = "Edited Test Recipe",
                Ingredients = ["sugar", "candy"],
                IngredientAmounts = ["2", "2"],
                IngredientMeasurements = ["ounces", "cups"],
                Directions = "Edited Mix ingredients and bake 20 mins",
                Calories = 1,
                Protein = 2,
                Carbs = 3,
                Fat = 4,
            };

            await _controller.RecipeEditFinished(vm, 1);

            var recipes = _recipeRepository.ReadAll().ToList();
            Assert.That(recipes.Count, Is.EqualTo(1));

            var recipe = recipes.First();
            Assert.That(recipe.Name, Is.EqualTo("Edited Test Recipe"));
            Assert.That(recipe.Directions, Is.EqualTo("Edited Mix ingredients and bake 20 mins"));
            Assert.That(recipe.Ingredients.Count, Is.EqualTo(2));
            Assert.That(recipe.Ingredients[0].IngredientBase.Name, Is.EqualTo("sugar"));
            Assert.That(recipe.Ingredients[1].IngredientBase.Name, Is.EqualTo("candy"));
            Assert.That(recipe.Ingredients[0].ToString(), Is.EqualTo("2 ounces of sugar"));
            Assert.That(recipe.Ingredients[1].ToString(), Is.EqualTo("2 cups of candy"));
            Assert.That(recipe.Calories, Is.EqualTo(1));
            Assert.That(recipe.Protein, Is.EqualTo(2));
            Assert.That(recipe.Carbs, Is.EqualTo(3));
            Assert.That(recipe.Fat, Is.EqualTo(4));
        }

        [Test]
        public async Task InvalidModelState()
        {
            var vm = new RecipeViewModel
            {
                Name = "Edited Test Recipe",
                Ingredients = ["sugar", "flour"],
                IngredientAmounts = ["1", "2"],
                IngredientMeasurements = ["cup", "cups"],
                Directions = "Mix ingredients and bake 20 mins",
                Calories = 1,
                Protein = 2,
                Carbs = 3,
                Fat = 4,
            };

            _controller.ModelState.AddModelError("Name", "Required");
            await _controller.RecipeEditFinished(vm, 1);

            var recipes = _recipeRepository.ReadAll().ToList();
            Assert.That(recipes.Count, Is.EqualTo(1));

            var recipe = recipes.First();
            Assert.That(recipe.Name, Is.EqualTo("Test Recipe"));
        }

        [Test]
        public async Task RedirectsIfUserDoesNotOwnRecipe()
        {
            var mockUser = new User { Id = "test-user-id" };
            _registrationService.Setup(r => r.FindUserByClaimAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(mockUser);
            _userRecipeRepo.Setup(r => r.GetUserOwnedRecipesByUserIdAsync("test-user-id"))
                .ReturnsAsync(new List<Recipe>());

            var result = await _controller.EditRecipe(1);

            var redirect = result as RedirectToActionResult;
            Assert.That(redirect, Is.Not.Null);
            Assert.That(redirect.ActionName, Is.EqualTo("Recipes"));
        }

        [Test]
        public async Task LetsUserInIfUserOwnsRecipe()
        {
            var mockUser = new User { Id = "test-user-id" };
            var ownedRecipe = new Recipe { Id = 1 };

            _registrationService.Setup(r => r.FindUserByClaimAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(mockUser);
            _userRecipeRepo.Setup(r => r.GetUserOwnedRecipesByUserIdAsync("test-user-id"))
                .ReturnsAsync(new List<Recipe> { ownedRecipe });

            var result = await _controller.EditRecipe(1);

            Assert.That(result, Is.InstanceOf<ViewResult>());
        }

        [Test]
        public async Task UserDoesOwnShowButton()
        {
            var mockUser = new User { Id = "test-user-id" };
            var ownedRecipe = new Recipe { Id = 1 };

            _registrationService.Setup(r => r.FindUserByClaimAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(mockUser);
            _userRecipeRepo.Setup(r => r.GetUserOwnedRecipesByUserIdAsync("test-user-id"))
                .ReturnsAsync(new List<Recipe> { ownedRecipe });

            var result = await _controller.Recipes(1) as ViewResult;

            Assert.That(result, Is.Not.Null);
            var vm = result.Model as RecipeViewModel;
            Assert.That(vm, Is.Not.Null);
            Assert.That(vm.IsOwned, Is.True);
        }

        [Test]
        public async Task UserDoesntOwnNoButton()
        {
            var mockUser = new User { Id = "test-user-id" };

            _registrationService.Setup(r => r.FindUserByClaimAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(mockUser);
            _userRecipeRepo.Setup(r => r.GetUserOwnedRecipesByUserIdAsync("test-user-id"))
                .ReturnsAsync(new List<Recipe>());

            var result = await _controller.Recipes(1) as ViewResult;

            Assert.That(result, Is.Not.Null);
            var vm = result.Model as RecipeViewModel;
            Assert.That(vm, Is.Not.Null);
            Assert.That(vm.IsOwned, Is.False);
        }
    }
}