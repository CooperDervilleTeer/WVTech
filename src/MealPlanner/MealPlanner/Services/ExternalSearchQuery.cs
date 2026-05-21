namespace MealPlanner.Services;

/// <summary>
/// Parameters for a composed external recipe search. Translated into provider-
/// specific query strings (e.g. Edamam's recipes/v2 endpoint) by the
/// <see cref="IExternalRecipeService"/> implementation. All bound and filter
/// fields are optional; the service treats null bounds as "no constraint".
/// </summary>
public sealed record ExternalSearchQuery(
    string? FreeText,
    int? CaloriesMin,
    int? CaloriesMax,
    int? ProteinMin,
    int? ProteinMax,
    int? CarbsMin,
    int? CarbsMax,
    int? FatMin,
    int? FatMax,
    IReadOnlyCollection<string> HealthFilters)
{
    /// <summary>Edamam <c>diet</c> labels, e.g. high-protein, low-carb.</summary>
    public IReadOnlyCollection<string> Diets { get; init; } = [];

    /// <summary>Edamam <c>cuisineType</c> values, e.g. Italian, Mexican.</summary>
    public IReadOnlyCollection<string> CuisineTypes { get; init; } = [];

    /// <summary>Edamam <c>mealType</c> values, e.g. Breakfast, Dinner.</summary>
    public IReadOnlyCollection<string> MealTypes { get; init; } = [];

    /// <summary>Edamam <c>dishType</c> values, e.g. Salad, Soup, Desserts.</summary>
    public IReadOnlyCollection<string> DishTypes { get; init; } = [];

    /// <summary>
    /// True when the query carries at least one bound, free-text term, health
    /// filter, or structured facet. Edamam's API rejects searches with no
    /// criteria; callers can short-circuit when this returns false.
    /// </summary>
    public bool HasAnyCriteria =>
        !string.IsNullOrWhiteSpace(FreeText)
        || CaloriesMin.HasValue || CaloriesMax.HasValue
        || ProteinMin.HasValue || ProteinMax.HasValue
        || CarbsMin.HasValue || CarbsMax.HasValue
        || FatMin.HasValue || FatMax.HasValue
        || HealthFilters.Count > 0
        || Diets.Count > 0
        || CuisineTypes.Count > 0
        || MealTypes.Count > 0
        || DishTypes.Count > 0;
}
