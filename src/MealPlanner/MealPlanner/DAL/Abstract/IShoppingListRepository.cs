using MealPlanner.Models;

namespace MealPlanner.DAL.Abstract;

public interface IShoppingListRepository
{
    void Add(ShoppingListItem item);
    void AddBatch(IEnumerable<ShoppingListItem> items);

    void Remove(int itemId, string userId);

    void RemoveAllByIngredientBase(string userId, int ingredientBaseId);

    void RemoveAutoAddedByUserId(string userId);

    IEnumerable<ShoppingListItem> GetByUserId(string userId);

    void ClearAllItems(string userId);

    void UpdateAmountByIngredientBase(string userId, int ingredientBaseId, float newAmount, string? displayAmount = null);

    HashSet<int> GetDismissedIngredientBaseIds(string userId);
    void DismissIngredientBase(string userId, int ingredientBaseId);
    void DismissBatch(string userId, IEnumerable<int> ingredientBaseIds);
    void UnDismiss(string userId, int ingredientBaseId);

    bool UpdateMeasurementById(string userId, int itemId, int measurementId);
}
