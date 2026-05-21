using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Per-user recommendation inputs assembled once per request. Carries the user's
/// dietary restrictions, vote history, upvoted recipes, standing food-tag
/// preferences, the normalized names of ingredients in their pantry, the
/// day-offsets at which each recipe appears on nearby planned meals, and a
/// corpus-level smoothed-IDF weight per tag so the preferred-tag scorers can
/// weight matches on rare tags more heavily than matches on common ones.
/// Streams and scorers read from this for personalisation.
/// </summary>
public sealed record UserRecommendationContext(
    HashSet<string> RestrictionNames,
    Dictionary<int, UserVoteType> UserVotes,
    Dictionary<int, float> VotePercentages,
    List<Recipe> Upvoted,
    HashSet<int> PreferredTagIds,
    HashSet<string> PantryIngredientNames,
    Dictionary<int, List<int>> RecentRecipeDayOffsets,
    Dictionary<int, float> TagRarityWeights);
