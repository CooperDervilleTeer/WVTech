namespace MealPlanner.Services.Recommendation;

public interface IRecommendationStream
{
    Task<IReadOnlyList<ScoredRecipe>> GetRankedCandidatesAsync(RecommendationContext ctx);
}
