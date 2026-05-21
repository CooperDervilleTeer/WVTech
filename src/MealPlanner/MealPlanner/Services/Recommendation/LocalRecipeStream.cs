using MealPlanner.DAL.Abstract;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Recommendation stream backed by the user's local recipe database. It scores
/// every recipe that passes the slot filters and returns them ranked. Weak
/// recipes are not pruned here — the service merges this stream's output with
/// the others by score, so a low-scoring local recipe simply sorts below a
/// stronger candidate (local or external) rather than being dropped outright.
/// </summary>
public sealed class LocalRecipeStream : IRecommendationStream
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IReadOnlyList<IRecipeScorer> _scorers;
    private readonly IReadOnlyList<IRecipeFilter> _filters;

    public LocalRecipeStream(
        IRecipeRepository recipeRepository,
        IEnumerable<IRecipeScorer> scorers,
        IEnumerable<IRecipeFilter> filters)
    {
        _recipeRepository = recipeRepository;
        _scorers = scorers.ToList();
        _filters = filters.ToList();
    }

    public async Task<IReadOnlyList<ScoredRecipe>> GetRankedCandidatesAsync(RecommendationContext ctx)
    {
        var recipes = await _recipeRepository.GetAllWithTagsAndIngredientsAsync();

        return recipes
            .Where(r => _filters.All(f => f.Allow(r, ctx)))
            .Select(r => new ScoredRecipe(r, _scorers.Sum(s => s.Score(r, ctx))))
            .OrderByDescending(x => x.Score)
            .ToList();
    }
}
