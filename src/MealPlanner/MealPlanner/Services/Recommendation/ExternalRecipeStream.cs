using MealPlanner.DAL.Abstract;
using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Recommendation stream backed by an external recipe provider (Edamam).
/// Composes a search query from the current <see cref="RecommendationContext"/>
/// — meal calorie/macro targets, the user's dietary restrictions, and any
/// preferred tag names — then applies the same scorers/filters as the local
/// stream so external candidates can be merged into the recommendation pool.
/// Returns an empty result when no external service is registered, when the
/// composed query has no criteria, or when the API call fails.
/// </summary>
public sealed class ExternalRecipeStream : IRecommendationStream
{
    private readonly IExternalRecipeService? _externalRecipeService;
    private readonly ITagRepository _tagRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly IReadOnlyList<IRecipeScorer> _scorers;
    private readonly IReadOnlyList<IRecipeFilter> _filters;

    public ExternalRecipeStream(
        ITagRepository tagRepository,
        IRecipeRepository recipeRepository,
        IEnumerable<IRecipeScorer> scorers,
        IEnumerable<IRecipeFilter> filters,
        IExternalRecipeService? externalRecipeService = null)
    {
        _tagRepository = tagRepository;
        _recipeRepository = recipeRepository;
        // Tag scorers are NO LONGER exempt — AttachInferredTagsAsync below
        // reverse-classifies Edamam's recipe categorization (dietLabels /
        // healthLabels / cuisineType / mealType / dishType) into local Tag
        // entities, so external recipes can earn the same tag-based score
        // boosts as local ones.
        //
        // PreferredTagFilter IS still exempt: Edamam's categorization is
        // incomplete (some recipes come back without dishType at all) and the
        // Phase 10 facet query already steered the search toward slot intent.
        // We boost via the scorers rather than hard-rejecting.
        _scorers = scorers.ToList();
        _filters = filters
            .Where(f => f is not PreferredTagFilter)
            .ToList();
        _externalRecipeService = externalRecipeService;
    }

    public async Task<IReadOnlyList<ScoredRecipe>> GetRankedCandidatesAsync(RecommendationContext ctx)
    {
        if (_externalRecipeService == null) return [];

        var query = await BuildQueryAsync(ctx);
        if (!query.HasAnyCriteria) return [];

        IEnumerable<Recipe> recipes;
        try
        {
            recipes = await _externalRecipeService.SearchByContextAsync(query);
        }
        catch
        {
            return [];
        }

        var reconciled = await ReconcileWithLocalAsync(recipes);
        await AttachInferredTagsAsync(reconciled);

        return reconciled
            .Where(r => _filters.All(f => f.Allow(r, ctx)))
            .Select(r => new ScoredRecipe(r, _scorers.Sum(s => s.Score(r, ctx))))
            .OrderByDescending(x => x.Score)
            .ToList();
    }

    /// <summary>
    /// Inverse-classifies each recipe's Edamam categorization strings into
    /// local Tag entities and attaches them to <see cref="Recipe.Tags"/>, so
    /// the tag-based scorers can act on otherwise-tagless external recipes.
    /// Tag entities are reused from the local DB (not synthesised), so their
    /// Ids match those the scorers compare against. No-ops when no recipe
    /// carries categorization or when the local Tag table is empty.
    /// </summary>
    private async Task AttachInferredTagsAsync(List<Recipe> recipes)
    {
        if (!recipes.Any(r => r.ExternalCategorization.Count > 0)) return;
        var localTags = await _tagRepository.GetTagsByPopularityAsync();
        if (localTags.Count == 0) return;

        foreach (var recipe in recipes)
        {
            if (recipe.ExternalCategorization.Count == 0) continue;
            var resolved = EdamamTagClassifier.ResolveLocalTags(recipe.ExternalCategorization, localTags);
            foreach (var tag in resolved)
                if (!recipe.Tags.Contains(tag)) recipe.Tags.Add(tag);
        }
    }

    /// <summary>
    /// Edamam results arrive with no database Id, so the downvote filter and the
    /// upvote/variety scorers — which key on recipe Id — cannot act on them.
    /// When a result is already cached locally (a Recipe row exists with the
    /// same ExternalUri), stamp that local row's Id onto the Edamam recipe,
    /// leaving the recipe's own data (ingredients, macros) intact. The vote and
    /// variety logic can then treat it like any local recipe, and the service's
    /// merge can de-duplicate it against the local stream's copy of the same
    /// recipe (both then key on id:, not uri:).
    /// </summary>
    private async Task<List<Recipe>> ReconcileWithLocalAsync(IEnumerable<Recipe> externalRecipes)
    {
        var recipes = externalRecipes.ToList();
        var uris = recipes
            .Select(r => r.ExternalUri)
            .Where(u => !string.IsNullOrEmpty(u))
            .Select(u => u!)
            .Distinct()
            .ToList();
        if (uris.Count == 0) return recipes;

        var localIdByUri = (await _recipeRepository.GetRecipesByExternalUrisAsync(uris))
            .Where(r => r.ExternalUri != null)
            .GroupBy(r => r.ExternalUri!)
            .ToDictionary(g => g.Key, g => g.First().Id);

        foreach (var recipe in recipes)
        {
            if (recipe.ExternalUri != null
                && localIdByUri.TryGetValue(recipe.ExternalUri, out var localId))
            {
                recipe.Id = localId;
            }
        }
        return recipes;
    }

    private async Task<ExternalSearchQuery> BuildQueryAsync(RecommendationContext ctx)
    {
        // Only the meal slot's tags drive the structured query. The standing
        // user tag list is an OR-style preference, so ANDing it across Edamam's
        // facets would mis-model it; user taste still re-ranks results through
        // the scorers below.
        var slotTagIds = ctx.Meal.PreferredTagIds.ToList();
        IEnumerable<string> slotTagNames = slotTagIds.Count > 0
            ? (await _tagRepository.GetTagsByIdsAsync(slotTagIds)).Select(t => t.Name)
            : [];
        var facets = EdamamTagClassifier.Classify(slotTagNames);

        var freeText = facets.FreeTextTerms.Count > 0
            ? string.Join(" ", facets.FreeTextTerms)
            : null;

        var healthFilters = ctx.User.RestrictionNames
            .Select(n => n.ToLowerInvariant())
            .Concat(facets.HealthLabels)
            .Distinct()
            .ToList();

        return new ExternalSearchQuery(
            freeText,
            CaloriesMin: ctx.Meal.CalorieTarget.HasValue ? 1 : (int?)null,
            CaloriesMax: ctx.Meal.CalorieTarget,
            ProteinMin: ctx.Meal.ProteinTarget.HasValue ? 0 : (int?)null,
            ProteinMax: ctx.Meal.ProteinTarget,
            CarbsMin: ctx.Meal.CarbTarget.HasValue ? 0 : (int?)null,
            CarbsMax: ctx.Meal.CarbTarget,
            FatMin: ctx.Meal.FatTarget.HasValue ? 0 : (int?)null,
            FatMax: ctx.Meal.FatTarget,
            healthFilters)
        {
            Diets = facets.Diets,
            CuisineTypes = facets.CuisineTypes,
            MealTypes = facets.MealTypes,
            DishTypes = facets.DishTypes,
        };
    }
}
