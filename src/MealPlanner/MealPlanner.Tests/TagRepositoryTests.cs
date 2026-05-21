using System.Data.Common;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Tests;

[TestFixture]
public class TagRepositoryTests
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
    }

    MealPlannerDBContext CreateContext() => new MealPlannerDBContext(_contextOptions);

    [TearDown]
    public void TearDown() => _connection.Dispose();

    [Test]
    public async Task GetTagNamesAsync_ReturnsTopTagsByUsage()
    {
        // Arrange
        using var context = CreateContext();
        var popular = new Tag { Name = "Popular" };
        var rare = new Tag { Name = "Rare" };
        context.Tags.AddRange(popular, rare);
        context.Recipes.Add(new Recipe { Name = "R1", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [popular] });
        context.Recipes.Add(new Recipe { Name = "R2", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [popular] });
        context.Recipes.Add(new Recipe { Name = "R3", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [rare] });
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        // Act
        var names = await repo.GetTagNamesAsync();

        // Assert — Popular (2 uses) should come before Rare (1 use)
        Assert.That(names.IndexOf("Popular"), Is.LessThan(names.IndexOf("Rare")));
    }

    [Test]
    public async Task GetTagNamesAsync_ReturnsAtMostTen()
    {
        // Arrange
        using var context = CreateContext();
        for (int i = 1; i <= 11; i++)
        {
            var tag = new Tag { Name = $"Tag{i}" };
            context.Recipes.Add(new Recipe { Name = $"Recipe{i}", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [tag] });
        }
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        // Act
        var names = await repo.GetTagNamesAsync();

        // Assert
        Assert.That(names.Count, Is.EqualTo(10));
    }

    [Test]
    public async Task GetTagNamesAsync_ExcludesLeastUsedTagWhenOver10()
    {
        // Arrange
        using var context = CreateContext();
        // 10 tags each on 2 recipes
        for (int i = 1; i <= 10; i++)
        {
            var tag = new Tag { Name = $"PopularTag{i}" };
            context.Recipes.Add(new Recipe { Name = $"PopRecipe{i}A", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [tag] });
            context.Recipes.Add(new Recipe { Name = $"PopRecipe{i}B", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [tag] });
        }
        // 1 tag on only 1 recipe — should be excluded
        var rareTag = new Tag { Name = "RareTag" };
        context.Recipes.Add(new Recipe { Name = "RareRecipe", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [rareTag] });
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        // Act
        var names = await repo.GetTagNamesAsync();

        // Assert
        Assert.That(names, Does.Not.Contain("RareTag"));
    }

    [Test]
    public async Task GetTagNamesAsync_ReturnsEmptyList_WhenNoTagsExist()
    {
        // Arrange
        var repo = new TagRepository(CreateContext());

        // Act
        var names = await repo.GetTagNamesAsync();

        // Assert
        Assert.That(names, Is.Empty);
    }

    // --- GetTagRarityWeightsAsync ---

    [Test]
    public async Task GetTagRarityWeightsAsync_RareTagOutweighsCommonTag()
    {
        // The whole point of rarity weighting: a tag carried by few recipes
        // (e.g. "Italian" cuisine) should weight more than one carried by many
        // (e.g. "Breakfast" meal type).
        using var context = CreateContext();
        var common = new Tag { Name = "Breakfast" };
        var rare = new Tag { Name = "Italian" };
        context.Tags.AddRange(common, rare);
        for (int i = 0; i < 5; i++)
            context.Recipes.Add(new Recipe { Name = $"Common{i}", Directions = "", Tags = [common] });
        context.Recipes.Add(new Recipe { Name = "Rare", Directions = "", Tags = [rare] });
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        var weights = await repo.GetTagRarityWeightsAsync();

        Assert.That(weights[rare.Id], Is.GreaterThan(weights[common.Id]),
            "the tag carried by fewer recipes must weight strictly more");
    }

    [Test]
    public async Task GetTagRarityWeightsAsync_WeightsAreNonNegative()
    {
        // The smoothed-IDF formula must not produce negative weights, even for
        // a tag that appears on every single recipe.
        using var context = CreateContext();
        var ubiquitous = new Tag { Name = "Everywhere" };
        context.Tags.Add(ubiquitous);
        for (int i = 0; i < 3; i++)
            context.Recipes.Add(new Recipe { Name = $"R{i}", Directions = "", Tags = [ubiquitous] });
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        var weights = await repo.GetTagRarityWeightsAsync();

        Assert.That(weights[ubiquitous.Id], Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public async Task GetTagRarityWeightsAsync_IncludesUnusedTags()
    {
        // A tag with zero recipes still belongs in the map — otherwise the
        // scorer would treat it as missing and fall back to its default weight.
        using var context = CreateContext();
        var orphan = new Tag { Name = "OrphanTag" };
        context.Tags.Add(orphan);
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        var weights = await repo.GetTagRarityWeightsAsync();

        Assert.That(weights.ContainsKey(orphan.Id), Is.True);
    }

    [Test]
    public async Task GetTagRarityWeightsAsync_NoTags_ReturnsEmptyDictionary()
    {
        var repo = new TagRepository(CreateContext());

        var weights = await repo.GetTagRarityWeightsAsync();

        Assert.That(weights, Is.Empty);
    }

    [Test]
    public async Task GetTagRarityWeightsAsync_EquallyFrequentTags_GetEqualWeights()
    {
        // Sanity check: two tags carried by the same number of recipes must
        // receive the same weight.
        using var context = CreateContext();
        var a = new Tag { Name = "A" };
        var b = new Tag { Name = "B" };
        context.Tags.AddRange(a, b);
        for (int i = 0; i < 3; i++)
        {
            context.Recipes.Add(new Recipe { Name = $"Ra{i}", Directions = "", Tags = [a] });
            context.Recipes.Add(new Recipe { Name = $"Rb{i}", Directions = "", Tags = [b] });
        }
        context.SaveChanges();
        var repo = new TagRepository(CreateContext());

        var weights = await repo.GetTagRarityWeightsAsync();

        Assert.That(weights[a.Id], Is.EqualTo(weights[b.Id]).Within(0.0001f));
    }
}
