using System.Data.Common;
using MealPlanner.DAL.Abstract;
using MealPlanner.DAL.Concrete;
using MealPlanner.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class UserFoodPreferenceRepositoryTests
{
    private DbConnection _connection = null!;
    private DbContextOptions<MealPlannerDBContext> _contextOptions;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<MealPlannerDBContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new MealPlannerDBContext(_contextOptions);
        ctx.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    MealPlannerDBContext CreateContext() => new(_contextOptions);

    private User SeedUser(MealPlannerDBContext ctx, string id = "user-1")
    {
        var user = new User { Id = id, UserName = $"{id}@test.com", NormalizedUserName = $"{id}@TEST.COM", Email = $"{id}@test.com", NormalizedEmail = $"{id}@TEST.COM", SecurityStamp = "s" };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user;
    }

    private Tag SeedTag(MealPlannerDBContext ctx, string name)
    {
        var tag = new Tag { Name = name };
        ctx.Tags.Add(tag);
        ctx.SaveChanges();
        return tag;
    }

    private (UserFoodPreferenceRepository repo, MealPlannerDBContext ctx) CreateRepo(Mock<ITagRepository>? tagRepoMock = null)
    {
        tagRepoMock ??= new Mock<ITagRepository>();
        var ctx = CreateContext();
        return (new UserFoodPreferenceRepository(ctx, tagRepoMock.Object), ctx);
    }

    // ── GetFoodPreferenceNamesAsync ──────────────────────────────────────────

    [Test]
    public async Task GetFoodPreferenceNamesAsync_ReturnsTagNamesForUser()
    {
        using var ctx = CreateContext();
        var user = SeedUser(ctx);
        var tag = SeedTag(ctx, "Italian");
        ctx.UserFoodPreferences.Add(new UserFoodPreference { UserId = user.Id, TagId = tag.Id });
        ctx.SaveChanges();

        var (repo, _) = CreateRepo();
        var names = await repo.GetFoodPreferenceNamesAsync("user-1");

        Assert.That(names, Contains.Item("Italian"));
    }

    [Test]
    public async Task GetFoodPreferenceNamesAsync_ReturnsEmptyListWhenNoPreferences()
    {
        using var ctx = CreateContext();
        SeedUser(ctx);

        var (repo, _) = CreateRepo();
        var names = await repo.GetFoodPreferenceNamesAsync("user-1");

        Assert.That(names, Is.Empty);
    }

    // ── GetFoodPreferenceTagIdsAsync ─────────────────────────────────────────

    [Test]
    public async Task GetFoodPreferenceTagIdsAsync_ReturnsTagIdsForUser()
    {
        using var ctx = CreateContext();
        var user = SeedUser(ctx);
        var italian = SeedTag(ctx, "Italian");
        var cheap = SeedTag(ctx, "Cheap");
        ctx.UserFoodPreferences.AddRange(
            new UserFoodPreference { UserId = user.Id, TagId = italian.Id },
            new UserFoodPreference { UserId = user.Id, TagId = cheap.Id });
        ctx.SaveChanges();

        var (repo, _) = CreateRepo();
        var ids = await repo.GetFoodPreferenceTagIdsAsync("user-1");

        Assert.That(ids, Is.EquivalentTo(new[] { italian.Id, cheap.Id }));
    }

    [Test]
    public async Task GetFoodPreferenceTagIdsAsync_ReturnsEmptySetWhenNoPreferences()
    {
        using var ctx = CreateContext();
        SeedUser(ctx);

        var (repo, _) = CreateRepo();
        var ids = await repo.GetFoodPreferenceTagIdsAsync("user-1");

        Assert.That(ids, Is.Empty);
    }

    [Test]
    public async Task GetFoodPreferenceTagIdsAsync_OnlyReturnsTagsForRequestedUser()
    {
        using var ctx = CreateContext();
        var u1 = SeedUser(ctx, "user-1");
        var u2 = SeedUser(ctx, "user-2");
        var tag = SeedTag(ctx, "Italian");
        ctx.UserFoodPreferences.AddRange(
            new UserFoodPreference { UserId = u1.Id, TagId = tag.Id },
            new UserFoodPreference { UserId = u2.Id, TagId = tag.Id });
        ctx.SaveChanges();

        var (repo, _) = CreateRepo();
        var ids = await repo.GetFoodPreferenceTagIdsAsync("user-1");

        Assert.That(ids, Has.Count.EqualTo(1));
        Assert.That(ids, Contains.Item(tag.Id));
    }

    // ── AddFoodPreferencesAsync ──────────────────────────────────────────────

    [Test]
    public async Task AddFoodPreferencesAsync_AddsExistingTagToUser()
    {
        using var seedCtx = CreateContext();
        SeedUser(seedCtx);
        var tag = SeedTag(seedCtx, "Italian");

        var tagMock = new Mock<ITagRepository>();
        tagMock.Setup(r => r.FindByNameAsync("Italian")).ReturnsAsync(tag);

        var (repo, repoCtx) = CreateRepo(tagMock);
        await repo.AddFoodPreferencesAsync("user-1", ["Italian"]);
        repoCtx.SaveChanges();

        var saved = CreateContext().UserFoodPreferences.Include(p => p.Tag).Where(p => p.UserId == "user-1").ToList();
        Assert.That(saved.Any(p => p.Tag.Name == "Italian"), Is.True);
    }

    [Test]
    public async Task AddFoodPreferencesAsync_CreatesTagIfNotExists()
    {
        using var seedCtx = CreateContext();
        SeedUser(seedCtx);

        var tagMock = new Mock<ITagRepository>();
        tagMock.Setup(r => r.FindByNameAsync("New Tag")).ReturnsAsync((Tag?)null);
        tagMock.Setup(r => r.CreateOrUpdate(It.IsAny<Tag>())).Returns<Tag>(t => t);

        var (repo, _) = CreateRepo(tagMock);
        await repo.AddFoodPreferencesAsync("user-1", ["New Tag"]);

        tagMock.Verify(r => r.CreateOrUpdate(It.Is<Tag>(t => t.Name == "New Tag")), Times.Once);
    }

    [Test]
    public async Task AddFoodPreferencesAsync_DoesNotDuplicateExistingPreference()
    {
        using var seedCtx = CreateContext();
        var user = SeedUser(seedCtx);
        var tag = SeedTag(seedCtx, "Italian");
        seedCtx.UserFoodPreferences.Add(new UserFoodPreference { UserId = user.Id, TagId = tag.Id });
        seedCtx.SaveChanges();

        var tagMock = new Mock<ITagRepository>();
        tagMock.Setup(r => r.FindByNameAsync("Italian")).ReturnsAsync(tag);

        var (repo, repoCtx) = CreateRepo(tagMock);
        await repo.AddFoodPreferencesAsync("user-1", ["Italian"]);
        repoCtx.SaveChanges();

        var count = CreateContext().UserFoodPreferences.Count(p => p.UserId == "user-1" && p.TagId == tag.Id);
        Assert.That(count, Is.EqualTo(1));
    }

    // ── RemoveFoodPreferenceAsync ────────────────────────────────────────────

    [Test]
    public async Task RemoveFoodPreferenceAsync_RemovesNamedTag()
    {
        using var seedCtx = CreateContext();
        var user = SeedUser(seedCtx);
        var tag = SeedTag(seedCtx, "Italian");
        seedCtx.UserFoodPreferences.Add(new UserFoodPreference { UserId = user.Id, TagId = tag.Id });
        seedCtx.SaveChanges();

        var tagMock = new Mock<ITagRepository>();
        tagMock.Setup(r => r.FindByNameAsync("Italian")).ReturnsAsync(tag);

        var (repo, repoCtx) = CreateRepo(tagMock);
        await repo.RemoveFoodPreferenceAsync("user-1", "Italian");
        repoCtx.SaveChanges();

        var exists = CreateContext().UserFoodPreferences.Any(p => p.UserId == "user-1" && p.TagId == tag.Id);
        Assert.That(exists, Is.False);
    }

    [Test]
    public async Task RemoveFoodPreferenceAsync_LeavesOtherTagsIntact()
    {
        using var seedCtx = CreateContext();
        var user = SeedUser(seedCtx);
        var italian = SeedTag(seedCtx, "Italian");
        var cheap = SeedTag(seedCtx, "Cheap");
        seedCtx.UserFoodPreferences.AddRange(
            new UserFoodPreference { UserId = user.Id, TagId = italian.Id },
            new UserFoodPreference { UserId = user.Id, TagId = cheap.Id });
        seedCtx.SaveChanges();

        var tagMock = new Mock<ITagRepository>();
        tagMock.Setup(r => r.FindByNameAsync("Italian")).ReturnsAsync(italian);

        var (repo, repoCtx) = CreateRepo(tagMock);
        await repo.RemoveFoodPreferenceAsync("user-1", "Italian");
        repoCtx.SaveChanges();

        Assert.That(CreateContext().UserFoodPreferences.Any(p => p.UserId == "user-1" && p.TagId == cheap.Id), Is.True);
        Assert.That(CreateContext().UserFoodPreferences.Any(p => p.UserId == "user-1" && p.TagId == italian.Id), Is.False);
    }
}
