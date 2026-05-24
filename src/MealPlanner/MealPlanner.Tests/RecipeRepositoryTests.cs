using System.Data.Common;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Tests;

[TestFixture]
public class RecipeRepositoryTests
{
    private DbConnection _connection;
    private DbContextOptions<MealPlannerDBContext> _contextOptions;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(_connection)
            .Options;
        
        using var context = new MealPlannerDBContext(_contextOptions);

        if (context.Database.EnsureCreated())
        {
            // Normally we'd add more test data here, but we will use the seed data definined in MealPlannerDBContext as well
            Ingredient ingredient = new Ingredient
            {
                IngredientBase = new IngredientBase { Name="test" },
                Measurement = new Measurement { Name="Test", Abbreviation="Test" },
                Amount = 0
            };
            context.Add( new Recipe
            {
                Id = 10,
                Name = "Test",
                Ingredients = [ingredient],
                Directions = ""
            });

            context.SaveChanges();
        }
    }

    MealPlannerDBContext CreateContext() => new MealPlannerDBContext(_contextOptions);

    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
    }
    
    [Test]
    public void GetRecipesByName_ReturnsARecipe()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("Oatmeal Cookies");

        // Assert
        Assert.That(results.First().Name, Is.EqualTo("Oatmeal Cookies"));
    }

    [Test]
    public async Task GetRecipesByExternalUrisAsync_ReturnsMatchingRecipesWithTags()
    {
        // Arrange — two externally-cached recipes
        using (var seed = CreateContext())
        {
            seed.Add(new Recipe
            {
                Id = 101, Name = "Cached A", Directions = "",
                ExternalUri = "http://edamam/a",
                Tags = [new Tag { Name = "ExtUriTestTag" }]
            });
            seed.Add(new Recipe
            {
                Id = 102, Name = "Cached B", Directions = "",
                ExternalUri = "http://edamam/b", Tags = []
            });
            seed.SaveChanges();
        }

        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act — one URI matches, one does not
        List<Recipe> results = await repo.GetRecipesByExternalUrisAsync(
            ["http://edamam/a", "http://edamam/missing"]);

        // Assert — only the matching recipe is returned, with its tags loaded
        Assert.That(results.Select(r => r.Id), Is.EquivalentTo(new[] { 101 }));
        Assert.That(results.Single().Tags.Select(t => t.Name), Does.Contain("ExtUriTestTag"));
    }

    [Test]
    public void GetRecipesByName_ReturnsEmptyListIfNoneFound()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("I don't exist");

        // Assert
        Assert.That(results, Is.Empty);
    }
    
    [Test]
    public void GetRecipesByName_MatchesBeginningOfName()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("Home");

        // Assert
        Assert.That(results.First().Name, Is.EqualTo("Homemade Mac 'n Cheese"));
    }
    
    [Test]
    public void GetRecipesByName_IsNotCaseSensitive()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("baked spaghetti casserole");

        // Assert
        Assert.That(results.First().Name, Is.EqualTo("Baked Spaghetti Casserole"));
    }
    
    [Test]
    public void GetRecipesByName_MatchesWordsInMiddleOfRecipeName()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("Spaghetti");

        // Assert
        foreach (Recipe r in results)
        {
            Console.WriteLine(r.Name);
        }
        
        Assert.That(results.Count(), Is.EqualTo(4), "There are 4 recipes with spaghetti in their name");
    }
    
    [Test]
    public void GetRecipesByName_MatchesBeginningOfWordsInMiddleOfRecipeName()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("Cass");

        // Assert
        Console.Write(results);
        Assert.That(results.Count(), Is.EqualTo(2), "There are 2 recipes with words that start with Cass");
    }
    
    [Test]
    public void GetRecipesByName_DoesntMatchMiddleOfWords()
    {   
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        List<Recipe> results = repo.GetRecipesByName("aghe");

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task ReadRecipeWithIngredientsAsync_ReturnsNullIfRecipeNotFound()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        var result = await repo.ReadRecipeWithIngredientsAsync(0);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ReadRecipeWithIngredientsAsync_ReturnsRecipeWithIngredients()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        var result = await repo.ReadRecipeWithIngredientsAsync(10);

        // Assert
        Assert.That(result?.Ingredients, Is.Not.Empty);
    }

    [Test]
    public async Task ReadRecipeWithIngredientsAsync_IncludesMeasurement()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        var result = await repo.ReadRecipeWithIngredientsAsync(10);

        // Assert
        Assert.That(result?.Ingredients[0].Measurement.Name, Is.EqualTo("Test"));
    }

    [Test]
    public async Task ReadRecipeWithIngredientsAsync_IncludesIngredientBase()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);

        // Act
        var result = await repo.ReadRecipeWithIngredientsAsync(10);

        // Assert
        Assert.That(result?.Ingredients[0].IngredientBase.Name, Is.EqualTo("test"));
    }

    [Test]
    public void CreateOrUpdate_CreatesNewRecipe()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Recipe toAdd = new Recipe
        {
            Name = "",
            Directions = ""
        };

        // Act
        int countBefore = context.Set<Recipe>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int countAfter = context.Set<Recipe>().Count();

        // Assert
        Assert.That(countAfter, Is.EqualTo(countBefore + 1));
    }

    [Test]
    public void CreateOrUpdate_UpdatesExistingRecipe()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Recipe toChange = context.Find<Recipe>(10);

        // Act
        toChange.Name = "Changed";
        repo.CreateOrUpdate(toChange);
        context.SaveChanges();
        Recipe result = context.Find<Recipe>(10);

        // Assert
        Assert.That(result.Name, Is.EqualTo("Changed"));
    }

    [Test]
    public void CreateOrUpdate_DoesntCreateNewIngredients_WhenUpdatingName()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Recipe toChange = context.Find<Recipe>(10);

        // Act
        int ingredientCountBefore = context.Set<Ingredient>().Count();
        toChange.Name = "Changed";
        repo.CreateOrUpdate(toChange);
        context.SaveChanges();
        int ingredientCountAfter = context.Set<Ingredient>().Count();

        // Assert
        Assert.That(ingredientCountAfter, Is.EqualTo(ingredientCountBefore));
    }

    [Test]
    public void CreateOrUpdate_CreatesNewIngredients()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient ingredient = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "" },
            Measurement = new Measurement { Name = "", Abbreviation = "" }
        };
        Recipe toAdd = new Recipe
        {
            Name = "",
            Directions = "",
            Ingredients = [ingredient]
        };

        // Act
        int ingredientCountBefore = context.Set<Ingredient>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int ingredientCountAfter = context.Set<Ingredient>().Count();

        // Assert
        Assert.That(ingredientCountAfter, Is.EqualTo(ingredientCountBefore + 1));
    }

    [Test]
    public void CreateOrUpdate_CreatesNewIngredients_WhenUpdatedWithNewIngredients()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient ingredient = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "" },
            Measurement = new Measurement { Name = "", Abbreviation = "" }
        };
        Recipe toChange = context.Find<Recipe>(10);

        // Act
        int ingredientCountBefore = context.Set<Ingredient>().Count();
        toChange.Ingredients.Add(ingredient);
        repo.CreateOrUpdate(toChange);
        context.SaveChanges();
        int ingredientCountAfter = context.Set<Ingredient>().Count();

        // Assert
        Assert.That(ingredientCountAfter, Is.EqualTo(ingredientCountBefore + 1));
    }

    [Test]
    public void CreateOrUpdate_CreatesNewIngredientBase()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient ingredient = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "Test2" },
            Measurement = new Measurement { Name = "", Abbreviation = "" }
        };
        Recipe toAdd = new Recipe
        {
            Name = "",
            Directions = "",
            Ingredients = [ingredient]
        };

        // Act
        int baseCountBefore = context.Set<IngredientBase>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int baseCountAfter = context.Set<IngredientBase>().Count();

        // Assert
        Assert.That(baseCountAfter, Is.EqualTo(baseCountBefore + 1));
    }

    [Test]
    public void CreateOrUpdate_UsesExistingIngredientBase()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient ingredient = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "Test" },
            Measurement = new Measurement { Name = "", Abbreviation = "" }
        };
        Recipe toAdd = new Recipe
        {
            Name = "",
            Directions = "",
            Ingredients = [ingredient]
        };

        // Act
        int baseCountBefore = context.Set<IngredientBase>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int baseCountAfter = context.Set<IngredientBase>().Count();

        // Assert
        Assert.That(baseCountAfter, Is.EqualTo(baseCountBefore));
    }

    [Test]
    public void CreateOrUpdate_CreatesNewMeasurement()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient ingredient = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "" },
            Measurement = new Measurement { Name = "Test2", Abbreviation = "Test2" }
        };
        Recipe toAdd = new Recipe
        {
            Name = "",
            Directions = "",
            Ingredients = [ingredient]
        };

        // Act
        int measurementCountBefore = context.Set<Measurement>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int measurementCountAfter = context.Set<Measurement>().Count();

        // Assert
        Assert.That(measurementCountAfter, Is.EqualTo(measurementCountBefore + 1));
    }

    [Test]
    public void CreateOrUpdate_UsesExistingMeasurement()
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient ingredient = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "" },
            Measurement = new Measurement { Name = "Test", Abbreviation = "Test" }
        };
        Recipe toAdd = new Recipe
        {
            Name = "",
            Directions = "",
            Ingredients = [ingredient]
        };

        // Act
        int measurementCountBefore = context.Set<Measurement>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int measurementCountAfter = context.Set<Measurement>().Count();

        // Assert
        Assert.That(measurementCountAfter, Is.EqualTo(measurementCountBefore));
    }

    [TestCase(new [] {"Same", "Same"}, new [] {"New 1", "New 2"}, new [] {1,2})]
    [TestCase(new [] {"New 1", "New 2"}, new [] {"Same", "Same"}, new [] {2,1})]
    public void CreateOrUpdate_DoesntDuplicateRow_IfMultipleOfSameUniqueEntry(string[] measurements, string[] ingredientBases, int[] expected)
    {
        // Arrange
        MealPlannerDBContext context = CreateContext();
        RecipeRepository repo = new RecipeRepository(context);
        Ingredient i1 = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = ingredientBases[0] },
            Measurement = new Measurement { Name = measurements[0], Abbreviation = measurements[0] }
        };
        Ingredient i2 = new Ingredient
        {
            Amount = 1,
            IngredientBase = new IngredientBase { Name = ingredientBases[1] },
            Measurement = new Measurement { Name = measurements[1], Abbreviation = measurements[1] }
        };
        Recipe toAdd = new Recipe 
        { 
            Name = "",
            Directions = "",
            Ingredients = [i1, i2]
        };

        // Act
        int measurementCountBefore = context.Set<Measurement>().Count();
        int baseCountBefore = context.Set<IngredientBase>().Count();
        repo.CreateOrUpdate(toAdd);
        context.SaveChanges();
        int measurementCountAfter = context.Set<Measurement>().Count();
        int baseCountAfter = context.Set<IngredientBase>().Count();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(measurementCountAfter, Is.EqualTo(measurementCountBefore + expected[0]));
            Assert.That(baseCountAfter, Is.EqualTo(baseCountBefore + expected[1]));
        }
        context.Dispose();
    }
}