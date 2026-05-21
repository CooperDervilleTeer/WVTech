using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using MealPlanner.Helpers;
using MealPlanner.Models;
using MealPlanner.Models.DTO;

namespace MealPlanner.Services;

public class EdamamService:IExternalRecipeService
{
    HttpClient _httpClient;
    JsonSerializerOptions _responseDeserializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    string _appId;
    string _apiKey;

    public EdamamService(HttpClient httpClient, string appId, string apiKey)
    {
        _httpClient = httpClient;
        _appId = appId;
        _apiKey = apiKey;
    }
    
    public async Task<IEnumerable<RecipeDTO>> SearchExternalRecipesByName(string recipeName)
    {
        string endpoint = $"recipes/v2?type=any&q={recipeName}&app_id={_appId}&app_key={_apiKey}&field=uri&field=label&field=image";

        HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error accessing Edamam Recipe Search API: {response.StatusCode}");
        }
        string responseBody = await response.Content.ReadAsStringAsync();

        EdamamRecipeSearchResponse? edamamResponse = JsonSerializer.Deserialize<EdamamRecipeSearchResponse>
        (
            responseBody,
            _responseDeserializerOptions
        );
        return edamamResponse?.Hits.Select(e => new RecipeDTO { Name = e.Recipe.Label, ExternalUri = e.Recipe.Uri, ImageUrl = e.Recipe.Image }) ?? [];
    }

    public async Task<Recipe?> GetExternalRecipeByURI(string uri)
    {
        uri = WebUtility.UrlEncode(uri);
        string endpoint = $"recipes/v2/by-uri?uri={uri}&app_id={_appId}&app_key={_apiKey}";
        
        HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(response.RequestMessage);
            throw new Exception($"Error accessing Edamam Recipe Search API: {response.StatusCode}");
        }
        string responseBody = await response.Content.ReadAsStringAsync();
        
        EdamamRecipeSearchResponse? edamamResponse = JsonSerializer.Deserialize<EdamamRecipeSearchResponse>
        (
            responseBody,
            _responseDeserializerOptions
        );
        return edamamResponse?.Hits.Select(
            e => new Recipe
            {
                Name = e.Recipe.Label,
                ExternalUri = e.Recipe.Uri,
                Directions = "",
                Calories = (int?) e.Recipe.TotalNutrients?["ENERC_KCAL"]?.Quantity ?? 0,
                Protein = (int?) e.Recipe.TotalNutrients?["PROCNT"]?.Quantity ?? 0,
                Carbs = (int?) e.Recipe.TotalNutrients?["CHOCDF"]?.Quantity ?? 0,
                Fat = (int?) e.Recipe.TotalNutrients?["FAT"]?.Quantity ?? 0,
                ImageUrl = e.Recipe.Image,
                Ingredients = ParseIngredientsFromResponse(e.Recipe.Ingredients ?? []),
                ExternalCategorization = CollectCategorization(e.Recipe)
            }).FirstOrDefault();
    }

    // Concatenates Edamam's five categorization arrays into one flat list,
    // which EdamamTagClassifier.ResolveLocalTags then reverse-maps onto local
    // Tag entities. All five vocabularies share the canonical string space the
    // forward classifier already routes on, so the inverse direction doesn't
    // need to know which facet a string came from.
    private static List<string> CollectCategorization(EdamamRecipe r) =>
        (r.DietLabels ?? [])
            .Concat(r.HealthLabels ?? [])
            .Concat(r.CuisineType ?? [])
            .Concat(r.MealType ?? [])
            .Concat(r.DishType ?? [])
            .ToList();

    public async Task<IEnumerable<Recipe>> SearchByContextAsync(ExternalSearchQuery query)
    {
        if (!query.HasAnyCriteria) return [];

        var parts = new List<string>
        {
            "type=any",
            $"app_id={_appId}",
            $"app_key={_apiKey}"
        };

        if (!string.IsNullOrWhiteSpace(query.FreeText))
            parts.Add($"q={WebUtility.UrlEncode(query.FreeText)}");

        if (query.CaloriesMin.HasValue || query.CaloriesMax.HasValue)
            parts.Add($"calories={query.CaloriesMin ?? 0}-{query.CaloriesMax ?? 99999}");

        if (query.ProteinMin.HasValue || query.ProteinMax.HasValue)
            parts.Add($"nutrients%5BPROCNT%5D={query.ProteinMin ?? 0}-{query.ProteinMax ?? 99999}");

        if (query.CarbsMin.HasValue || query.CarbsMax.HasValue)
            parts.Add($"nutrients%5BCHOCDF%5D={query.CarbsMin ?? 0}-{query.CarbsMax ?? 99999}");

        if (query.FatMin.HasValue || query.FatMax.HasValue)
            parts.Add($"nutrients%5BFAT%5D={query.FatMin ?? 0}-{query.FatMax ?? 99999}");

        foreach (var h in query.HealthFilters)
            parts.Add($"health={WebUtility.UrlEncode(h)}");

        foreach (var d in query.Diets)
            parts.Add($"diet={WebUtility.UrlEncode(d)}");

        foreach (var c in query.CuisineTypes)
            parts.Add($"cuisineType={WebUtility.UrlEncode(c)}");

        foreach (var m in query.MealTypes)
            parts.Add($"mealType={WebUtility.UrlEncode(m)}");

        foreach (var dt in query.DishTypes)
            parts.Add($"dishType={WebUtility.UrlEncode(dt)}");

        string endpoint = $"recipes/v2?{string.Join("&", parts)}";

        HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error accessing Edamam Recipe Search API: {response.StatusCode}");

        string responseBody = await response.Content.ReadAsStringAsync();
        EdamamRecipeSearchResponse? edamamResponse = JsonSerializer.Deserialize<EdamamRecipeSearchResponse>(
            responseBody, _responseDeserializerOptions);

        return edamamResponse?.Hits.Select(e => new Recipe
        {
            Name = e.Recipe.Label,
            ExternalUri = e.Recipe.Uri,
            Directions = "",
            Calories = (int?) e.Recipe.TotalNutrients?["ENERC_KCAL"]?.Quantity ?? 0,
            Protein  = (int?) e.Recipe.TotalNutrients?["PROCNT"]?.Quantity ?? 0,
            Carbs    = (int?) e.Recipe.TotalNutrients?["CHOCDF"]?.Quantity ?? 0,
            Fat      = (int?) e.Recipe.TotalNutrients?["FAT"]?.Quantity ?? 0,
            ImageUrl = e.Recipe.Image,
            Tags = [],
            Ingredients = ParseIngredientsFromResponse(e.Recipe.Ingredients ?? []),
            ExternalCategorization = CollectCategorization(e.Recipe)
        }) ?? [];
    }

    public async Task<IEnumerable<Recipe>> GetExternalRecipesByURIs(IEnumerable<string> uris)
    {
        var uriList = uris.ToList();
        if (uriList.Count == 0)
            return [];

        var results = new List<Recipe>();
        foreach (var batch in uriList.Chunk(20))
        {
            string uriParams = string.Join("&", batch.Select(u => $"uri={WebUtility.UrlEncode(u)}"));
            string endpoint = $"recipes/v2/by-uri?{uriParams}&app_id={_appId}&app_key={_apiKey}";

            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error accessing Edamam Recipe Search API: {response.StatusCode}");

            string responseBody = await response.Content.ReadAsStringAsync();
            EdamamRecipeSearchResponse? edamamResponse = JsonSerializer.Deserialize<EdamamRecipeSearchResponse>(
                responseBody, _responseDeserializerOptions);

            if (edamamResponse is not null)
                results.AddRange(edamamResponse.Hits.Select(e => new Recipe
                {
                    Name = e.Recipe.Label,
                    ExternalUri = e.Recipe.Uri,
                    Directions = "",
                    Calories = (int?) e.Recipe.TotalNutrients?["ENERC_KCAL"]?.Quantity ?? 0,
                    Protein  = (int?) e.Recipe.TotalNutrients?["PROCNT"]?.Quantity ?? 0,
                    Carbs    = (int?) e.Recipe.TotalNutrients?["CHOCDF"]?.Quantity ?? 0,
                    Fat      = (int?) e.Recipe.TotalNutrients?["FAT"]?.Quantity ?? 0,
                    ImageUrl = e.Recipe.Image,
                    Ingredients = ParseIngredientsFromResponse(e.Recipe.Ingredients ?? []),
                    ExternalCategorization = CollectCategorization(e.Recipe)
                }));
        }
        return results;
    }

    private List<Ingredient> ParseIngredientsFromResponse(IList<EdamamIngredient> edamamIngredients)
    {
        return edamamIngredients.Select(i => new Ingredient
        {
            DisplayName = i.Food,
            Amount = i.Quantity,
            IngredientBase = new IngredientBase { Name = IngredientNameNormalizer.NormalizeKey(i.Food) },
            Measurement = new Measurement { Name = i.Measure }
        }).ToList();
    }
}