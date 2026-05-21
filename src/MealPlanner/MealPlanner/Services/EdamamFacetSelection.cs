namespace MealPlanner.Services;

/// <summary>
/// The outcome of routing a set of recipe tag names onto Edamam recipe-search
/// facets. Each facet list holds canonical Edamam values; <see cref="FreeTextTerms"/>
/// holds tag names that matched no facet and fall back to the free-text query.
/// </summary>
public sealed record EdamamFacetSelection(
    IReadOnlyList<string> Diets,
    IReadOnlyList<string> HealthLabels,
    IReadOnlyList<string> CuisineTypes,
    IReadOnlyList<string> MealTypes,
    IReadOnlyList<string> DishTypes,
    IReadOnlyList<string> FreeTextTerms);
