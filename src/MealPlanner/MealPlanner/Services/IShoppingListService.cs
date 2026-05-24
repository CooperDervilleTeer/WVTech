using MealPlanner.Models;

namespace MealPlanner.Services;


public interface IShoppingListService
{
    IEnumerable<ShoppingListItem> GetItemsForUser(string userId);
    void AddItem(string userId, string itemName, float amount, string measurement, string? displayAmount = null);
    void AddItemsBatch(string userId, IEnumerable<(string name, float amount, string measurement)> items);
    void RemoveItem(int itemId, string userId);
    void ClearItems(string userId);
    void UpdateItemAmount(string userId, int ingredientBaseId, float newAmount, string? displayAmount = null);
    Task SyncFromDateRangeAsync(string userId, User user, DateTime dateFrom, DateTime dateTo);
    bool UpdateItemMeasurement(string userId, int itemId, int measurementId);
}
