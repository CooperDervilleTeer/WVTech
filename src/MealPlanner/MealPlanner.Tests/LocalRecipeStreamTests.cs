using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services.Recommendation;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class LocalRecipeStreamTests
{
    private static RecommendationContext EmptyContext(
        HashSet<string>? restrictions = null,
        Dictionary<int, UserVoteType>? votes = null,
        Dictionary<int, float>? percentages = null,
        List<Recipe>? upvoted = null,
        HashSet<int>? userPreferredTagIds = null,
        int? calorieTarget = null,
        int? proteinTarget = null,
        int? carbTarget = null,
        int? fatTarget = null,
        HashSet<int>? mealPreferredTagIds = null,
        HashSet<string>? excludedRecipeKeys = null) =>
        new(
            new UserRecommendationContext(
                restrictions ?? [],
                votes ?? [],
                percentages ?? [],
                upvoted ?? [],
                userPreferredTagIds ?? [],
                [],
                [],
                []),
            new MealRecommendationContext(
                calorieTarget,
                proteinTarget,
                carbTarget,
                fatTarget,
                mealPreferredTagIds ?? [],
                excludedRecipeKeys ?? []));

    private static LocalRecipeStream BuildStream(
        List<Recipe> pool,
        IEnumerable<IRecipeScorer>? scorers = null,
        IEnumerable<IRecipeFilter>? filters = null)
    {
        var repoMock = new Mock<IRecipeRepository>();
        repoMock.Setup(r => r.GetAllWithTagsAndIngredientsAsync()).ReturnsAsync(pool);
        return new LocalRecipeStream(
            repoMock.Object,
            scorers ?? [
                new UpvotePriorityScorer(),
                new VotePercentageScorer(),
                new UserPreferredTagScorer(),
                new MealPreferredTagScorer()
            ],
            filters ?? [
                new DownVoteFilter(),
                new DietaryRestrictionFilter(),
                new PreferredTagFilter(),
                new ExcludedRecipeFilter()
            ]);
    }

    // The stream now returns ScoredRecipe; most tests only care about the
    // recipes and their order, so unwrap to a plain recipe list.
    private static async Task<List<Recipe>> RankedRecipes(
        LocalRecipeStream stream, RecommendationContext ctx) =>
        (await stream.GetRankedCandidatesAsync(ctx)).Select(s => s.Recipe).ToList();

    [Test]
    public async Task GetRankedCandidatesAsync_EmptyPool_ReturnsEmpty()
    {
        var stream = BuildStream([]);

        var result = await RankedRecipes(stream, EmptyContext());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetRankedCandidatesAsync_ReturnsAllUnfilteredRecipes()
    {
        var a = new Recipe { Id = 1, Tags = [] };
        var b = new Recipe { Id = 2, Tags = [] };
        var stream = BuildStream([a, b]);

        var result = await RankedRecipes(stream, EmptyContext());

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_PopulatesScoreFromScorers()
    {
        // The score travels out of the stream so the service can merge streams
        // by score — it must reflect the scorers, not be a placeholder zero.
        var recipe = new Recipe { Id = 1, Tags = [] };
        var scorer = new VotePercentageScorer();
        var ctx = EmptyContext(percentages: new Dictionary<int, float> { [1] = 0.7f });
        var stream = BuildStream([recipe], scorers: [scorer]);

        var result = (await stream.GetRankedCandidatesAsync(ctx)).ToList();

        Assert.That(result[0].Score, Is.EqualTo(scorer.Score(recipe, ctx)).Within(0.0001f));
        Assert.That(result[0].Score, Is.GreaterThan(0f), "scorer should produce a non-zero score here");
    }

    [Test]
    public async Task GetRankedCandidatesAsync_ExcludesDownvotedRecipes()
    {
        var downvoted = new Recipe { Id = 1, Tags = [] };
        var allowed   = new Recipe { Id = 2, Tags = [] };
        var ctx = EmptyContext(votes: new Dictionary<int, UserVoteType> { [1] = UserVoteType.DownVote });
        var stream = BuildStream([downvoted, allowed]);

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result, Does.Not.Contain(downvoted));
        Assert.That(result, Does.Contain(allowed));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_ExcludesDietaryRestrictionMismatch()
    {
        var vegan    = new Recipe { Id = 1, Tags = [new Tag { Id = 1, Name = "Vegan" }] };
        var nonVegan = new Recipe { Id = 2, Tags = [] };
        var ctx = EmptyContext(restrictions: ["Vegan"]);
        var stream = BuildStream([vegan, nonVegan]);

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result, Does.Contain(vegan));
        Assert.That(result, Does.Not.Contain(nonVegan));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_PutsUpvotedRecipeFirst()
    {
        var upvoted = new Recipe { Id = 2, Tags = [] };
        var normal  = new Recipe { Id = 1, Tags = [] };
        var ctx = EmptyContext(upvoted: [upvoted]);
        var stream = BuildStream([normal, upvoted]); // normal listed first in pool

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result[0].Id, Is.EqualTo(upvoted.Id));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_OrdersByVotePercentageAmongNonUpvoted()
    {
        var highVote = new Recipe { Id = 1, Tags = [] };
        var lowVote  = new Recipe { Id = 2, Tags = [] };
        var ctx = EmptyContext(percentages: new Dictionary<int, float> { [1] = 0.8f, [2] = 0.2f });
        var stream = BuildStream([lowVote, highVote]); // lowVote listed first in pool

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result[0].Id, Is.EqualTo(highVote.Id));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_PreferredTagFilter_RejectsNonMatchingWhenSlotHasTags()
    {
        var matching    = new Recipe { Id = 1, Tags = [new Tag { Id = 10, Name = "Breakfast" }] };
        var nonMatching = new Recipe { Id = 2, Tags = [new Tag { Id = 20, Name = "Dessert" }] };
        var ctx = EmptyContext(mealPreferredTagIds: [10]);
        var stream = BuildStream([matching, nonMatching]);

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result, Does.Contain(matching));
        Assert.That(result, Does.Not.Contain(nonMatching));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_OrdersByMealPreferredTagMatches()
    {
        var doubleMatch = new Recipe { Id = 1, Tags = [new Tag { Id = 10 }, new Tag { Id = 11 }] };
        var singleMatch = new Recipe { Id = 2, Tags = [new Tag { Id = 10 }] };
        var ctx = EmptyContext(mealPreferredTagIds: [10, 11]);
        var stream = BuildStream([singleMatch, doubleMatch]); // singleMatch listed first

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result[0].Id, Is.EqualTo(doubleMatch.Id));
    }

    [Test]
    public void GetRankedCandidatesAsync_EmptyScorersAndFilters_DoesNotThrow()
    {
        var repoMock = new Mock<IRecipeRepository>();
        repoMock.Setup(r => r.GetAllWithTagsAndIngredientsAsync()).ReturnsAsync([]);
        var stream = new LocalRecipeStream(repoMock.Object, [], []);

        Assert.DoesNotThrowAsync(() => stream.GetRankedCandidatesAsync(EmptyContext()));
    }

    [Test]
    public async Task GetRankedCandidatesAsync_ExcludesAlreadyPlannedRecipes()
    {
        var planned   = new Recipe { Id = 1, Tags = [] };
        var available = new Recipe { Id = 2, Tags = [] };
        var ctx = EmptyContext(excludedRecipeKeys: ["id:1"]);
        var stream = BuildStream([planned, available]);

        var result = await RankedRecipes(stream, ctx);

        Assert.That(result, Does.Not.Contain(planned));
        Assert.That(result, Does.Contain(available));
    }
}
