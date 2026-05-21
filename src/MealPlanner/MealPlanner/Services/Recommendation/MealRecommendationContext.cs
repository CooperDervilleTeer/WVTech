using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

/// <summary>
/// Per-meal-slot inputs. Carries the slot's calorie and macro targets, any tag
/// preferences explicitly requested for this slot, and the keys of recipes the
/// caller has already committed — placed in an earlier slot of the same day
/// plan, or excluded by the regenerate flow. Built fresh for each meal in a
/// day plan and passed alongside <see cref="UserRecommendationContext"/>.
/// </summary>
public sealed record MealRecommendationContext(
    int? CalorieTarget,
    int? ProteinTarget,
    int? CarbTarget,
    int? FatTarget,
    HashSet<int> PreferredTagIds,
    HashSet<string> ExcludedRecipeKeys);
