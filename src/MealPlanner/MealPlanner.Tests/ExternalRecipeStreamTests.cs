using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.Services.Recommendation;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class ExternalRecipeStreamTests
{
    private Mock<IExternalRecipeService> _externalServiceMock;
    private Mock<ITagRepository> _tagRepoMock;
    private Mock<IRecipeRepository> _recipeRepoMock;

    [SetUp]
    public void SetUp()
    {
        _externalServiceMock = new Mock<IExternalRecipeService>();
        _tagRepoMock = new Mock<ITagRepository>();
        _recipeRepoMock = new Mock<IRecipeRepository>();
        _tagRepoMock
            .Setup(r => r.GetTagsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<Tag>());
        _tagRepoMock
            .Setup(r => r.GetTagsByPopularityAsync())
            .ReturnsAsync(new List<Tag>());
        _recipeRepoMock
            .Setup(r => r.GetRecipesByExternalUrisAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync(Enumerable.Empty<Recipe>());
    }

    private static RecommendationContext BuildContext(
        HashSet<string>? restrictions = null,
        HashSet<int>? userTags = null,
        int? calorieTarget = null,
        int? proteinTarget = null,
        int? carbTarget = null,
        int? fatTarget = null,
        HashSet<int>? mealTags = null,
        List<Recipe>? upvoted = null,
        Dictionary<int, UserVoteType>? votes = null) =>
        new(
            new UserRecommendationContext(
                restrictions ?? [],
                votes ?? [],
                [],
                upvoted ?? [],
                userTags ?? [],
                [],
                [],
                []),
            new MealRecommendationContext(
                calorieTarget,
                proteinTarget,
                carbTarget,
                fatTarget,
                mealTags ?? [],
                []));

    private ExternalRecipeStream BuildStream(
        IEnumerable<IRecipeScorer>? scorers = null,
        IEnumerable<IRecipeFilter>? filters = null,
        bool withExternal = true) =>
        new(
            _tagRepoMock.Object,
            _recipeRepoMock.Object,
            scorers ?? [],
            filters ?? [],
            withExternal ? _externalServiceMock.Object : null);

    [Test]
    public async Task GetRankedCandidatesAsync_NullExternalService_ReturnsEmpty()
    {
        var stream = BuildStream(withExternal: false);

        var result = await stream.GetRankedCandidatesAsync(BuildContext());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_NoCriteria_ReturnsEmptyWithoutCallingApi()
    {
        var stream = BuildStream();

        var result = await stream.GetRankedCandidatesAsync(BuildContext());

        Assert.That(result, Is.Empty);
        _externalServiceMock.Verify(
            s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()),
            Times.Never);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_BuildsQueryFromCalorieTarget()
    {
        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(calorieTarget: 400));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.CaloriesMin, Is.EqualTo(1));
        Assert.That(captured.CaloriesMax, Is.EqualTo(400));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_BuildsQueryFromMacroTargets()
    {
        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(proteinTarget: 50, carbTarget: 60, fatTarget: 20));

        Assert.That(captured!.ProteinMax, Is.EqualTo(50));
        Assert.That(captured.CarbsMax, Is.EqualTo(60));
        Assert.That(captured.FatMax, Is.EqualTo(20));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_LowercasesRestrictionsAsHealthFilters()
    {
        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(restrictions: ["Vegan", "Gluten-Free"]));

        Assert.That(captured!.HealthFilters, Is.EquivalentTo(new[] { "vegan", "gluten-free" }));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_RoutesMatchingSlotTagsToFacets()
    {
        _tagRepoMock
            .Setup(r => r.GetTagsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<Tag>
            {
                new() { Id = 10, Name = "Italian" },
                new() { Id = 20, Name = "Breakfast" }
            });

        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(mealTags: [10, 20]));

        Assert.That(captured!.CuisineTypes, Does.Contain("Italian"));
        Assert.That(captured.MealTypes, Does.Contain("Breakfast"));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_RoutesUnmatchedSlotTagToFreeText()
    {
        _tagRepoMock
            .Setup(r => r.GetTagsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<Tag> { new() { Id = 30, Name = "Comfort Food" } });

        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(mealTags: [30]));

        Assert.That(captured!.FreeText, Does.Contain("Comfort Food"));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_DoesNotClassifyUserTagsIntoFacets()
    {
        // Standing user tags are an OR-style preference; only the meal slot's
        // tags drive the structured query. User taste still re-ranks results
        // through the scorers after the fetch.
        _tagRepoMock
            .Setup(r => r.GetTagsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<Tag> { new() { Id = 10, Name = "Italian" } });

        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(userTags: [10], calorieTarget: 500));

        Assert.That(captured!.CuisineTypes, Is.Empty);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_MergesClassifiedHealthTagsWithRestrictionFilters()
    {
        _tagRepoMock
            .Setup(r => r.GetTagsByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<Tag> { new() { Id = 40, Name = "Paleo" } });

        ExternalSearchQuery? captured = null;
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .Callback<ExternalSearchQuery>(q => captured = q)
            .ReturnsAsync([]);

        var stream = BuildStream();
        await stream.GetRankedCandidatesAsync(BuildContext(restrictions: ["Vegan"], mealTags: [40]));

        Assert.That(captured!.HealthFilters, Does.Contain("vegan"));
        Assert.That(captured.HealthFilters, Does.Contain("paleo"));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_AppliesFiltersToResults()
    {
        var downvoted = new Recipe { Id = 1, Name = "Bad", Tags = [] };
        var allowed = new Recipe { Id = 2, Name = "Good", Tags = [] };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([downvoted, allowed]);

        var ctx = BuildContext(
            votes: new Dictionary<int, UserVoteType> { [1] = UserVoteType.DownVote },
            calorieTarget: 500);

        var stream = BuildStream(filters: [new DownVoteFilter()]);
        var result = (await stream.GetRankedCandidatesAsync(ctx)).Select(s => s.Recipe).ToList();

        Assert.That(result, Does.Not.Contain(downvoted));
        Assert.That(result, Does.Contain(allowed));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_AppliesScorersToOrderResults()
    {
        var upvoted = new Recipe { Id = 2, Tags = [] };
        var normal  = new Recipe { Id = 1, Tags = [] };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([normal, upvoted]); // normal listed first

        var ctx = BuildContext(upvoted: [upvoted], calorieTarget: 500);
        var stream = BuildStream(scorers: [new UpvotePriorityScorer()]);

        var result = (await stream.GetRankedCandidatesAsync(ctx)).Select(s => s.Recipe).ToList();

        Assert.That(result[0].Id, Is.EqualTo(upvoted.Id), "Upvoted external recipe should rank first");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_ApiThrows_ReturnsEmpty()
    {
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ThrowsAsync(new Exception("Edamam down"));

        var stream = BuildStream();
        var result = await stream.GetRankedCandidatesAsync(BuildContext(calorieTarget: 500));

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_StampsLocalRecipeIdOntoCachedExternalRecipe()
    {
        // An Edamam result already cached locally (matched by ExternalUri) keeps
        // its own data but is stamped with the local row's Id, so the vote and
        // variety scorers — which key on recipe Id — can act on it.
        var fromEdamam = new Recipe
        {
            Id = 0, ExternalUri = "u1", Name = "Soup", Tags = [],
            Ingredients = [new Ingredient { DisplayName = "Carrot" }]
        };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);
        var localRow = new Recipe { Id = 7, ExternalUri = "u1", Name = "Soup (cached)", Tags = [] };
        _recipeRepoMock
            .Setup(r => r.GetRecipesByExternalUrisAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([localRow]);

        var stream = BuildStream();
        var result = (await stream.GetRankedCandidatesAsync(BuildContext(calorieTarget: 500)))
            .Select(s => s.Recipe).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(7), "local row's Id is stamped on");
        Assert.That(result[0].Name, Is.EqualTo("Soup"), "the Edamam recipe is kept, not replaced");
        Assert.That(result[0].Ingredients.Select(i => i.DisplayName), Does.Contain("Carrot"),
            "the Edamam recipe's own ingredients are preserved");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_LocallyCachedDownvotedRecipeIsFilteredOut()
    {
        // Matching a cached recipe to its local row lets the downvote filter
        // act on it — the raw Edamam result has Id 0 and would slip past.
        var fromEdamam = new Recipe { Id = 0, ExternalUri = "u1", Name = "Soup", Tags = [] };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);
        var localRow = new Recipe { Id = 7, ExternalUri = "u1", Name = "Soup", Tags = [] };
        _recipeRepoMock
            .Setup(r => r.GetRecipesByExternalUrisAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([localRow]);

        var ctx = BuildContext(
            votes: new Dictionary<int, UserVoteType> { [7] = UserVoteType.DownVote },
            calorieTarget: 500);
        var stream = BuildStream(filters: [new DownVoteFilter()]);

        var result = await stream.GetRankedCandidatesAsync(ctx);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_UnmatchedExternalRecipeIsKept()
    {
        // A genuinely new Edamam result with no local row is kept as-is.
        var fromEdamam = new Recipe { Id = 0, ExternalUri = "u-new", Name = "Novel Dish", Tags = [] };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);
        _recipeRepoMock
            .Setup(r => r.GetRecipesByExternalUrisAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);

        var stream = BuildStream();
        var result = (await stream.GetRankedCandidatesAsync(BuildContext(calorieTarget: 500)))
            .Select(s => s.Recipe).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ExternalUri, Is.EqualTo("u-new"));
    }

    // --- Inverse tag classification (attach local Tags from Edamam categorization) ---

    [Test]
    public async Task GetRankedCandidatesAsync_AttachesInverseClassifiedLocalTagsToExternalRecipes()
    {
        // External recipe carries Edamam categorization "Italian" — the stream
        // resolves it back to the local "Italian" Tag and attaches it so the
        // tag-based scorers can act on this recipe.
        var italian = new Tag { Id = 5, Name = "Italian" };
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([italian]);
        var fromEdamam = new Recipe
        {
            Id = 0, ExternalUri = "u1", Name = "Pasta", Tags = [],
            ExternalCategorization = ["Italian"]
        };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);

        var stream = BuildStream();
        var result = (await stream.GetRankedCandidatesAsync(BuildContext(calorieTarget: 500)))
            .Select(s => s.Recipe).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Tags, Does.Contain(italian),
            "the resolved local Tag must be attached to the external recipe");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_UserPreferredTagScorerScoresInverseClassifiedExternalRecipe()
    {
        var italian = new Tag { Id = 5, Name = "Italian" };
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([italian]);
        var fromEdamam = new Recipe
        {
            Id = 0, ExternalUri = "u1", Name = "Pasta", Tags = [],
            ExternalCategorization = ["Italian"]
        };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);

        var ctx = BuildContext(userTags: [5], calorieTarget: 500);
        var stream = BuildStream(scorers: [new UserPreferredTagScorer()]);

        var result = await stream.GetRankedCandidatesAsync(ctx);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Score, Is.GreaterThan(0f),
            "an Edamam recipe whose categorization matches a user-preferred tag must now score above zero");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_MealPreferredTagScorerScoresInverseClassifiedExternalRecipe()
    {
        var breakfast = new Tag { Id = 7, Name = "Breakfast" };
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([breakfast]);
        var fromEdamam = new Recipe
        {
            Id = 0, ExternalUri = "u1", Name = "Pancakes", Tags = [],
            ExternalCategorization = ["Breakfast"]
        };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);

        var ctx = BuildContext(mealTags: [7], calorieTarget: 500);
        var stream = BuildStream(scorers: [new MealPreferredTagScorer()]);

        var result = await stream.GetRankedCandidatesAsync(ctx);

        Assert.That(result[0].Score, Is.GreaterThan(0f),
            "an Edamam recipe whose categorization matches a slot's preferred tag must now score above zero");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_TagSimilarityScorerScoresInverseClassifiedExternalRecipe()
    {
        var italian = new Tag { Id = 5, Name = "Italian" };
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([italian]);
        var upvotedLocal = new Recipe { Id = 100, Tags = [italian] };
        var fromEdamam = new Recipe
        {
            Id = 0, ExternalUri = "u1", Name = "Pasta", Tags = [],
            ExternalCategorization = ["Italian"]
        };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);

        var ctx = BuildContext(upvoted: [upvotedLocal], calorieTarget: 500);
        var stream = BuildStream(scorers: [new TagSimilarityScorer()]);

        var result = await stream.GetRankedCandidatesAsync(ctx);

        Assert.That(result[0].Score, Is.GreaterThan(0f),
            "an Edamam recipe sharing a tag with the user's upvoted recipes must now score above zero");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_NoExternalCategorization_LeavesTagsEmpty()
    {
        // A recipe whose categorization arrays were missing on the Edamam side
        // (or whose strings classify to nothing locally) carries no inferred
        // tags — the stream still returns it, with Tags empty.
        _tagRepoMock.Setup(r => r.GetTagsByPopularityAsync()).ReturnsAsync([new Tag { Id = 1, Name = "Italian" }]);
        var fromEdamam = new Recipe
        {
            Id = 0, ExternalUri = "u1", Name = "Plain", Tags = [],
            ExternalCategorization = []
        };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([fromEdamam]);

        var stream = BuildStream();
        var result = (await stream.GetRankedCandidatesAsync(BuildContext(calorieTarget: 500)))
            .Select(s => s.Recipe).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Tags, Is.Empty);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_IsExemptFromPreferredTagFilter()
    {
        // External recipes carry no tags, so the meal-slot tag filter would
        // drop every one of them. The Phase 10 facet query already applied the
        // slot's tag intent, so the external stream skips PreferredTagFilter.
        var external = new Recipe { Id = 0, ExternalUri = "u1", Name = "Tagless", Tags = [] };
        _externalServiceMock
            .Setup(s => s.SearchByContextAsync(It.IsAny<ExternalSearchQuery>()))
            .ReturnsAsync([external]);

        var ctx = BuildContext(mealTags: [10], calorieTarget: 500); // slot specifies a tag
        var stream = BuildStream(filters: [new PreferredTagFilter()]);

        var result = await stream.GetRankedCandidatesAsync(ctx);

        Assert.That(result, Has.Count.EqualTo(1),
            "PreferredTagFilter must not drop tagless external recipes");
    }
}
