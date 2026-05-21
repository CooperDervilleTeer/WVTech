using System.Data.Common;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class MealRepositoryTests
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
        context.Database.EnsureCreated();

        context.Users.Add(new User { Id = "user-1", UserName = "user-1", NormalizedUserName = "USER-1", Email = "u@test.com", NormalizedEmail = "U@TEST.COM", SecurityStamp = "stamp" });
        context.SaveChanges();

        var recipe1 = new Recipe { Name = "R1", Directions = "" };
        var recipe2 = new Recipe { Name = "R2", Directions = "" };
        context.AddRange(recipe1, recipe2);
        context.SaveChanges();

        context.AddRange(
            new Meal { Title = "Meal A", UserId = "user-1", StartTime = DateTime.Today, Recipes = [recipe1] },
            new Meal { Title = "Meal B", UserId = "user-1", StartTime = DateTime.Today, Recipes = [recipe2] },
            new Meal { Title = "Meal C", UserId = "user-1", StartTime = DateTime.Today }
        );

        context.SaveChanges();
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    MealPlannerDBContext CreateContext() => new MealPlannerDBContext(_contextOptions);

    int IdOf(MealPlannerDBContext ctx, string title) =>
        ctx.Set<Meal>().First(m => m.Title == title).Id;

    [Test]
    public async Task GetMealsByIdsAsync_ReturnsOnlyRequestedIds()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var idA = IdOf(context, "Meal A");
        var idC = IdOf(context, "Meal C");

        var result = await repo.GetMealsByIdsAsync([idA, idC]);

        Assert.That(result.Select(m => m.Id), Is.EquivalentTo(new[] { idA, idC }));
    }

    [Test]
    public async Task GetMealsByIdsAsync_ExcludesMealsNotInList()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var idA = IdOf(context, "Meal A");
        var idB = IdOf(context, "Meal B");
        var idC = IdOf(context, "Meal C");

        var result = await repo.GetMealsByIdsAsync([idA]);

        Assert.That(result.Any(m => m.Id == idB), Is.False);
        Assert.That(result.Any(m => m.Id == idC), Is.False);
    }

    [Test]
    public async Task GetMealsByIdsAsync_IncludesRecipes()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var idA = IdOf(context, "Meal A");

        var result = await repo.GetMealsByIdsAsync([idA]);

        var meal = result.Single();
        Assert.That(meal.Recipes, Is.Not.Empty);
        Assert.That(meal.Recipes[0].Name, Is.EqualTo("R1"));
    }

    [Test]
    public async Task GetMealsByIdsAsync_WithEmptyList_ReturnsEmpty()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);

        var result = await repo.GetMealsByIdsAsync([]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetMealsByIdsAsync_WithNonExistentId_IgnoresIt()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var idA = IdOf(context, "Meal A");

        var result = await repo.GetMealsByIdsAsync([idA, 999999]);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(idA));
    }

    [Test]
    public async Task GetUserRecipeIdsForDateAsync_ReturnsRecipeIdsForUsersMealsOnDate()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var user = context.Users.Single();
        var expected = context.Recipes.Where(r => r.Name == "R1" || r.Name == "R2").Select(r => r.Id).ToList();

        var result = await repo.GetUserRecipeIdsForDateAsync(user, DateTime.Today);

        Assert.That(result, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task GetUserRecipeIdsForDateAsync_ExcludesMealsOnOtherDates()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var user = context.Users.Single();

        var result = await repo.GetUserRecipeIdsForDateAsync(user, DateTime.Today.AddDays(1));

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetUserRecipeIdsForDateAsync_ExcludesOtherUsersMeals()
    {
        using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = "user-2", UserName = "user-2", NormalizedUserName = "USER-2",
            Email = "u2@test.com", NormalizedEmail = "U2@TEST.COM", SecurityStamp = "stamp2"
        });
        var otherRecipe = new Recipe { Name = "OtherUserRecipe", Directions = "" };
        context.Add(otherRecipe);
        context.SaveChanges();
        context.Add(new Meal { Title = "Other user meal", UserId = "user-2", StartTime = DateTime.Today, Recipes = [otherRecipe] });
        context.SaveChanges();
        var repo = new MealRepository(context);
        var jack = context.Users.Single(u => u.Id == "user-1");

        var result = await repo.GetUserRecipeIdsForDateAsync(jack, DateTime.Today);

        Assert.That(result, Does.Not.Contain(otherRecipe.Id));
    }

    [Test]
    public async Task GetUserRecipeIdsForDateAsync_ExcludesGivenMealIdWhenProvided()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);
        var user = context.Users.Single();
        var mealAId = IdOf(context, "Meal A");
        var r2Id = context.Recipes.Single(r => r.Name == "R2").Id;

        var result = await repo.GetUserRecipeIdsForDateAsync(user, DateTime.Today, excludeMealId: mealAId);

        Assert.That(result, Does.Not.Contain(context.Recipes.Single(r => r.Name == "R1").Id),
            "Meal A's recipes must be omitted when its id is excluded");
        Assert.That(result, Does.Contain(r2Id),
            "Other meals' recipes remain in the result");
    }

    [Test]
    public async Task GetUserRecipeIdsForDateAsync_ReturnsDistinctIdsWhenRecipeAppearsInMultipleMeals()
    {
        using var context = CreateContext();
        var sharedRecipe = context.Recipes.Single(r => r.Name == "R1");
        context.Add(new Meal
        {
            Title = "Extra meal sharing R1",
            UserId = "user-1",
            StartTime = DateTime.Today,
            Recipes = [sharedRecipe]
        });
        context.SaveChanges();
        var repo = new MealRepository(context);
        var user = context.Users.Single();

        var result = await repo.GetUserRecipeIdsForDateAsync(user, DateTime.Today);

        Assert.That(result.Count(id => id == sharedRecipe.Id), Is.EqualTo(1),
            "a HashSet must collapse the duplicate appearances of the same recipe id");
    }

    [Test]
    public async Task GetUserRecipeIdsForDateAsync_IncludesWeeklyRepeatMealsMatchingDayOfWeek()
    {
        using var context = CreateContext();
        var weeklyRecipe = new Recipe { Name = "Sunday Roast", Directions = "" };
        context.Add(weeklyRecipe);
        context.SaveChanges();
        var anchor = DateTime.Today.AddDays(-21); // three weeks back, same day-of-week as today
        context.Add(new Meal
        {
            Title = "Weekly",
            UserId = "user-1",
            StartTime = anchor,
            RepeatRule = "Weekly",
            Recipes = [weeklyRecipe]
        });
        context.SaveChanges();
        var repo = new MealRepository(context);
        var user = context.Users.Single();

        var result = await repo.GetUserRecipeIdsForDateAsync(user, DateTime.Today);

        Assert.That(result, Does.Contain(weeklyRecipe.Id),
            "weekly meals matching the queried date's day-of-week must contribute their recipe ids");
    }

    [Test]
    public void CreateOrUpdate_NewExternalRecipe_CachesUriOnlyShell()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);

        var edamamRecipe = new Recipe
        {
            Name = "Edamam Curry",
            Directions = "from edamam",
            ExternalUri = "http://edamam/curry",
            Calories = 500,
            ImageUrl = "http://edamam/curry.jpg",
            Ingredients =
            [
                new Ingredient
                {
                    DisplayName = "Lentils",
                    IngredientBase = new IngredientBase { Name = "zzz-lentil" },
                    Measurement = new Measurement { Name = "zzz-cup" }
                }
            ]
        };
        var meal = new Meal
        {
            Title = "Curry Night", UserId = "user-1",
            StartTime = DateTime.Today, Recipes = [edamamRecipe]
        };

        repo.CreateOrUpdate(meal);
        context.SaveChanges();

        using var verify = CreateContext();
        var cached = verify.Set<Recipe>()
            .Include(r => r.Ingredients)
            .Single(r => r.ExternalUri == "http://edamam/curry");
        // Edamam's TOS permits caching only the recipe URI — no recipe data.
        Assert.That(cached.Ingredients, Is.Empty, "no ingredients may be persisted");
        Assert.That(cached.Name, Is.Empty, "no name may be persisted");
        Assert.That(cached.Calories, Is.EqualTo(0), "no nutrition data may be persisted");
        Assert.That(cached.ImageUrl, Is.Null, "no image may be persisted");

        var savedMeal = verify.Set<Meal>().Include(m => m.Recipes)
            .Single(m => m.Title == "Curry Night");
        Assert.That(savedMeal.Recipes.Select(r => r.ExternalUri), Does.Contain("http://edamam/curry"));
    }

    [Test]
    public void CreateOrUpdate_AlreadyCachedExternalRecipe_ReusesRowWithoutDuplicating()
    {
        using (var seed = CreateContext())
        {
            seed.Set<Recipe>().Add(new Recipe
            {
                Name = "Cached Tacos", Directions = "", ExternalUri = "http://edamam/tacos"
            });
            seed.SaveChanges();
        }

        using var context = CreateContext();
        var repo = new MealRepository(context);

        var edamamRecipe = new Recipe
        {
            Name = "Tacos (fresh from edamam)", Directions = "x",
            ExternalUri = "http://edamam/tacos"
        };
        var meal = new Meal
        {
            Title = "Taco Tuesday", UserId = "user-1",
            StartTime = DateTime.Today, Recipes = [edamamRecipe]
        };

        repo.CreateOrUpdate(meal);
        Assert.DoesNotThrow(() => context.SaveChanges(),
            "re-selecting an already-cached external recipe must not violate the ExternalUri unique index");

        using var verify = CreateContext();
        Assert.That(verify.Set<Recipe>().Count(r => r.ExternalUri == "http://edamam/tacos"),
            Is.EqualTo(1), "the cached recipe row is reused, not duplicated");
        var row = verify.Set<Recipe>().Single(r => r.ExternalUri == "http://edamam/tacos");
        Assert.That(row.Name, Is.EqualTo("Cached Tacos"), "the cached row's data is left untouched");
    }

    [Test]
    public async Task RemoveAllMealsWithSameTitleAsync_DeletesAllMatchingMeals()
    {
        using var seedCtx = CreateContext();
        seedCtx.Meals.Add(new Meal { Title = "Meal A", UserId = "user-1", StartTime = DateTime.Today.AddDays(1) });
        await seedCtx.SaveChangesAsync();

        using var context = CreateContext();
        var repo = new MealRepository(context);

        await repo.RemoveAllMealsWithSameTitleAsync("user-1", "Meal A");

        using var verify = CreateContext();
        Assert.That(verify.Meals.Any(m => m.Title == "Meal A" && m.UserId == "user-1"), Is.False);
    }

    [Test]
    public async Task RemoveAllMealsWithSameTitleAsync_DoesNotDeleteOtherTitles()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);

        await repo.RemoveAllMealsWithSameTitleAsync("user-1", "Meal A");

        using var verify = CreateContext();
        Assert.That(verify.Meals.Any(m => m.Title == "Meal B" && m.UserId == "user-1"), Is.True);
        Assert.That(verify.Meals.Any(m => m.Title == "Meal C" && m.UserId == "user-1"), Is.True);
    }

    [Test]
    public async Task RemoveAllMealsWithSameTitleAsync_DoesNotDeleteOtherUsersMatchingTitle()
    {
        using var seedCtx = CreateContext();
        seedCtx.Users.Add(new User { Id = "user-2", UserName = "user-2", NormalizedUserName = "USER-2", Email = "u2@test.com", NormalizedEmail = "U2@TEST.COM", SecurityStamp = "stamp2" });
        seedCtx.Meals.Add(new Meal { Title = "Meal A", UserId = "user-2", StartTime = DateTime.Today });
        await seedCtx.SaveChangesAsync();

        using var context = CreateContext();
        var repo = new MealRepository(context);
        
        await repo.RemoveAllMealsWithSameTitleAsync("user-1", "Meal A");

        using var verify = CreateContext();
        Assert.That(verify.Meals.Any(m => m.Title == "Meal A" && m.UserId == "user-2"), Is.True);
    }

    [Test]
    public async Task RemoveAllMealsWithSameTitleAsync_NoMatchingTitle_DoesNotThrow()
    {
        using var context = CreateContext();
        var repo = new MealRepository(context);

        Assert.DoesNotThrowAsync(() => repo.RemoveAllMealsWithSameTitleAsync("user-1", "Nonexistent Meal"));
    }
}
