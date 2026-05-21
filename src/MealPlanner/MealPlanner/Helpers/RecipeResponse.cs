namespace MealPlanner.Helpers;

public class EdamamRecipeSearchResponse
{
    public IList<EdamamHit> Hits { get; set; }
}

public class EdamamHit
{
    public EdamamRecipe Recipe { get; set; }
}

public class EdamamRecipe
{
    public string Uri { get; set; }
    public string Label { get; set; }
    public string? Image { get; set; }
    public IList<EdamamIngredient>? Ingredients { get; set; }
    public IDictionary<string, EdamamNutrient>? TotalNutrients { get; set; }
    public IList<string>? DietLabels { get; set; }
    public IList<string>? HealthLabels { get; set; }
    public IList<string>? CuisineType { get; set; }
    public IList<string>? MealType { get; set; }
    public IList<string>? DishType { get; set; }
}

public class EdamamIngredient
{
    public float Quantity { get; set; }
    public string Measure { get; set; }
    public string Food { get; set; }
}

public class EdamamNutrient
{
    public string Label { get; set; }
    public float Quantity { get; set; }
}