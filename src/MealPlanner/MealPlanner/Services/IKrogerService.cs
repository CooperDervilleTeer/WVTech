using MealPlanner.Models.DTO;

namespace MealPlanner.Services;

public interface IKrogerService
{
    string GetAuthorizationUrl(string state);
    Task<KrogerTokenResponse?> ExchangeCodeAsync(string code);
    Task<List<KrogerStoreInfo>> FindNearestStoresAsync(string zipCode, int radiusInMiles = 50, int limit = 5);
    Task<string?> GetClientCredentialsTokenAsync();
    Task<KrogerProductMatch?> SearchProductUpcAsync(string ingredientName, string storeId, float amount, string measurement, string token);
    Task<bool> ExportCartAsync(IEnumerable<KrogerCartItem> items, string userAccessToken);
}
