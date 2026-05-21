using MealPlanner.Models;

namespace MealPlanner.Services;

/// <summary>
/// Routes recipe tag names onto Edamam recipes/v2 search facets — diet,
/// health, cuisineType, mealType, dishType. Matching is case- and
/// separator-insensitive ("Low Carb" matches "low-carb"), with a small alias
/// map for tag names that differ from Edamam's wording. A tag matching no
/// facet becomes a free-text term; a tag matching several is assigned by
/// precedence: diet, health, cuisine, meal, dish.
/// </summary>
public static class EdamamTagClassifier
{
    // Canonical Edamam facet values, exactly as the recipes/v2 spec defines them.
    private static readonly string[] _diets =
        ["balanced", "high-fiber", "high-protein", "low-carb", "low-fat", "low-sodium"];

    private static readonly string[] _health =
        ["alcohol-cocktail", "alcohol-free", "celery-free", "crustacean-free", "dairy-free",
         "DASH", "egg-free", "fish-free", "fodmap-free", "gluten-free", "immuno-supportive",
         "keto-friendly", "kidney-friendly", "kosher", "low-fat-abs", "low-potassium",
         "low-sugar", "lupine-free", "Mediterranean", "mollusk-free", "mustard-free",
         "no-oil-added", "paleo", "peanut-free", "pescatarian", "pork-free", "red-meat-free",
         "sesame-free", "shellfish-free", "soy-free", "sugar-conscious", "sulfite-free",
         "tree-nut-free", "vegan", "vegetarian", "wheat-free"];

    private static readonly string[] _cuisineTypes =
        ["American", "Asian", "British", "Caribbean", "Central Europe", "Chinese",
         "Eastern Europe", "French", "Greek", "Indian", "Italian", "Japanese", "Korean",
         "Kosher", "Mediterranean", "Mexican", "Middle Eastern", "Nordic", "South American",
         "South East Asian"];

    private static readonly string[] _mealTypes =
        ["Breakfast", "Lunch", "Dinner", "Snack", "Teatime"];

    private static readonly string[] _dishTypes =
        ["Biscuits and cookies", "Bread", "Cereals", "Condiments and sauces", "Desserts",
         "Drinks", "Main course", "Pancake", "Preps", "Preserve", "Salad", "Sandwiches",
         "Side dish", "Soup", "Starter", "Sweets"];

    // Tag names that differ from Edamam's wording: normalized alias -> canonical value.
    private static readonly Dictionary<string, string> _dishAliases = new()
    {
        ["dessert"] = "Desserts",
        ["cookie"] = "Biscuits and cookies",
        ["cookies"] = "Biscuits and cookies",
        ["appetizer"] = "Starter",
        ["entree"] = "Main course",
        ["main"] = "Main course",
        ["main-dish"] = "Main course",
        ["side"] = "Side dish",
        ["drink"] = "Drinks",
        ["sweet"] = "Sweets",
        ["sandwich"] = "Sandwiches",
        ["condiment"] = "Condiments and sauces",
        ["sauce"] = "Condiments and sauces",
    };

    private static readonly Dictionary<string, string> _healthAliases = new()
    {
        ["keto"] = "keto-friendly",
    };

    private static readonly Dictionary<string, string> _dietLookup = BuildLookup(_diets);
    private static readonly Dictionary<string, string> _healthLookup = BuildLookup(_health, _healthAliases);
    private static readonly Dictionary<string, string> _cuisineLookup = BuildLookup(_cuisineTypes);
    private static readonly Dictionary<string, string> _mealLookup = BuildLookup(_mealTypes);
    private static readonly Dictionary<string, string> _dishLookup = BuildLookup(_dishTypes, _dishAliases);

    // Inverse of Classify: given a list of canonical Edamam category strings
    // (a recipe's dishType / cuisineType / mealType / dietLabels / healthLabels
    // values), returns the distinct local Tag entities whose Name would
    // forward-classify to any of those values. Used by ExternalRecipeStream
    // to attach local Tags onto otherwise-tagless Edamam recipes so the
    // tag-based scorers can act on them. A local Tag whose name falls through
    // to free text in the forward direction (no facet match) can never be
    // resolved here.
    public static List<Tag> ResolveLocalTags(IEnumerable<string> edamamCategories, IEnumerable<Tag> localTags)
    {
        var targets = edamamCategories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (targets.Count == 0) return [];

        var matched = new List<Tag>();
        foreach (var tag in localTags)
        {
            var facets = Classify([tag.Name]);
            bool hit = facets.Diets.Any(v => targets.Contains(v))
                    || facets.HealthLabels.Any(v => targets.Contains(v))
                    || facets.CuisineTypes.Any(v => targets.Contains(v))
                    || facets.MealTypes.Any(v => targets.Contains(v))
                    || facets.DishTypes.Any(v => targets.Contains(v));
            if (hit && !matched.Contains(tag)) matched.Add(tag);
        }
        return matched;
    }

    public static EdamamFacetSelection Classify(IEnumerable<string> tagNames)
    {
        var diets = new List<string>();
        var health = new List<string>();
        var cuisines = new List<string>();
        var meals = new List<string>();
        var dishes = new List<string>();
        var freeText = new List<string>();

        foreach (var name in tagNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var key = Normalize(name);

            if (_dietLookup.TryGetValue(key, out var diet)) AddDistinct(diets, diet);
            else if (_healthLookup.TryGetValue(key, out var h)) AddDistinct(health, h);
            else if (_cuisineLookup.TryGetValue(key, out var c)) AddDistinct(cuisines, c);
            else if (_mealLookup.TryGetValue(key, out var m)) AddDistinct(meals, m);
            else if (_dishLookup.TryGetValue(key, out var d)) AddDistinct(dishes, d);
            else AddDistinct(freeText, name.Trim());
        }

        return new EdamamFacetSelection(diets, health, cuisines, meals, dishes, freeText);
    }

    // Lowercases and unifies word separators so tag wording variations
    // ("Low Carb", "low_carb") collapse onto a single canonical key.
    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');

    private static Dictionary<string, string> BuildLookup(
        string[] canonical, Dictionary<string, string>? aliases = null)
    {
        var lookup = new Dictionary<string, string>();
        foreach (var value in canonical) lookup[Normalize(value)] = value;
        if (aliases != null)
            foreach (var (alias, value) in aliases) lookup[Normalize(alias)] = value;
        return lookup;
    }

    private static void AddDistinct(List<string> list, string value)
    {
        if (!list.Contains(value)) list.Add(value);
    }
}
