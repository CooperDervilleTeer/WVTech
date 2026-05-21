using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// A recipe paired with the aggregate score a stream's scorers gave it for the
/// current <see cref="RecommendationContext"/>. Streams return these so the
/// service can merge candidates from every stream into one score-ranked pool,
/// rather than concatenating streams and losing cross-stream comparability.
/// </summary>
public sealed record ScoredRecipe(Recipe Recipe, float Score);
