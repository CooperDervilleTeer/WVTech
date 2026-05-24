using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealPlanner.Tests
{
    [TestFixture]
    public class AddRecipeTests
    {
        private MealPlannerDBContext _context;
        private RecipeRepository _recipeRepository;
        private FoodEntriesController _controller;

        //sets up an in memory database to play with and sets the variables above to a new instance
        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<MealPlannerDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

            _context = new MealPlannerDBContext(options);
            _recipeRepository = new RecipeRepository(_context);
            var userRecipeRepo = new Mock<IUserRecipeRepository>();
            var tagRepo = new Mock<ITagRepository>();
            tagRepo.Setup(r => r.GetTagNamesAsync()).ReturnsAsync([]);
            var registrationService = new Mock<IRegistrationService>();
            var externalRecipeService = new Mock<IExternalRecipeService>();
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
            _controller = new FoodEntriesController(_recipeRepository, tagRepo.Object, userRecipeRepo.Object, _context, registrationService.Object, Mock.Of<IMeasurementRepository>(), mockEnv.Object, blobContainer: null, externalRecipeService.Object);
        }

        //handels the cleaning up after every test
        [TearDown]
        public void Cleanup()
        {
            _controller?.Dispose();
            _context?.Dispose();
        }

        [Test]
        public async Task AddsRecipeToRepository()
        {
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

            var recipes = _recipeRepository.ReadAll().ToList();
            Assert.That(recipes.Count, Is.EqualTo(1));

            var recipe = recipes.First();
            Assert.That(recipe.Name, Is.EqualTo("Test Recipe"));
            Assert.That(recipe.Directions, Is.EqualTo("Mix ingredients and bake 20 mins"));
            Assert.That(recipe.Ingredients[0].IngredientBase.Name, Is.EqualTo("sugar"));
            Assert.That(recipe.Ingredients[1].IngredientBase.Name, Is.EqualTo("flour"));
            Assert.That(recipe.Calories, Is.EqualTo(0));
            Assert.That(recipe.Protein, Is.EqualTo(1));
            Assert.That(recipe.Carbs, Is.EqualTo(2));
            Assert.That(recipe.Fat, Is.EqualTo(3));
        }

        [Test]
        public async Task InvalidModelState()
        {
            var vm1 = new RecipeViewModel
            {
                Name = "Test Recipe",
                Ingredients = ["sugar", "flour"],
                IngredientAmounts = ["1", "2"],
                IngredientMeasurements = ["cup", "cups"],
                Directions = "Mix ingredients and bake 20 mins"
            };

            _controller.ModelState.AddModelError("Name", "Required");
            await _controller.RecipeAdded(vm1);

            var recipes = _recipeRepository.ReadAll().ToList();
            Assert.That(recipes.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task AddingMultipleRecipes()
        {
            var vm1 = new RecipeViewModel
            {
                Name = "1Name",
                Ingredients = ["1Entry1", "1Entry2"],
                IngredientAmounts = ["0", "0"],
                IngredientMeasurements = ["", ""],
                Directions = "1Directions",
                Calories = 0,
                Protein = 1,
                Carbs = 2,
                Fat = 3,
            };

            var vm2 = new RecipeViewModel
            {
                Name = "2Name",
                Ingredients = ["2Entry1", "2Entry2"],
                IngredientAmounts = ["0", "0"],
                IngredientMeasurements = ["", ""],
                Directions = "2Directions",
                Calories = 20,
                Protein = 21,
                Carbs = 22,
                Fat = 23,
            };

            await _controller.RecipeAdded(vm1);
            await _controller.RecipeAdded(vm2);

            var recipes = _recipeRepository.ReadAll().ToList();
            Assert.That(recipes.Count, Is.EqualTo(2));

            var recipe = recipes.First();
            Assert.That(recipe.Name, Is.EqualTo("1Name"));
            Assert.That(recipe.Directions, Is.EqualTo("1Directions"));
            Assert.That(recipe.Ingredients[0].DisplayName, Is.EqualTo("1Entry1"));
            Assert.That(recipe.Ingredients[1].DisplayName, Is.EqualTo("1Entry2"));
            Assert.That(recipe.Calories, Is.EqualTo(0));
            Assert.That(recipe.Protein, Is.EqualTo(1));
            Assert.That(recipe.Carbs, Is.EqualTo(2));
            Assert.That(recipe.Fat, Is.EqualTo(3));

            var recipe2 = recipes.Last();
            Assert.That(recipe2.Name, Is.EqualTo("2Name"));
            Assert.That(recipe2.Directions, Is.EqualTo("2Directions"));
            Assert.That(recipe2.Ingredients[0].DisplayName, Is.EqualTo("2Entry1"));
            Assert.That(recipe2.Ingredients[1].DisplayName, Is.EqualTo("2Entry2"));
            Assert.That(recipe2.Calories, Is.EqualTo(20));
            Assert.That(recipe2.Protein, Is.EqualTo(21));
            Assert.That(recipe2.Carbs, Is.EqualTo(22));
            Assert.That(recipe2.Fat, Is.EqualTo(23));
        }
    }
}
