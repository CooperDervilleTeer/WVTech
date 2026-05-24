using MealPlanner.DAL.Abstract;
using MealPlanner.Models;

namespace MealPlanner.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly IShoppingListRepository _shoppingListRepository;
    private readonly IMealRepository _mealRepository;  
    private readonly IIngredientBaseRepository _ingredientBaseRepo;
    private readonly IRepository<Measurement> _measurementRepo;

    public ShoppingListService(IShoppingListRepository shoppingListRepository, IMealRepository mealRepository, IIngredientBaseRepository ingredientBaseRepo, IRepository<Measurement> measurementRepo)
    {
        _shoppingListRepository = shoppingListRepository;
        _mealRepository = mealRepository;
        _ingredientBaseRepo = ingredientBaseRepo;
        _measurementRepo = measurementRepo;   
    }

    public async Task SyncFromDateRangeAsync(string userId, User user, DateTime dateFrom, DateTime dateTo)
    {
        var meals = await _mealRepository.GetUserMealsByDateRangeWithIngredientsAsync(user, dateFrom, dateTo);
        var ingredients = meals
            .SelectMany(m => m.Recipes.DistinctBy(r => r.Id).SelectMany(r => r.Ingredients))
            .ToList();
        SyncFromMeals(userId, ingredients);
    }

    private void SyncFromMeals(string userId, IEnumerable<Ingredient> ingredients)
    {
        _shoppingListRepository.RemoveAutoAddedByUserId(userId);

        var manualItems = _shoppingListRepository.GetByUserId(userId).ToList();
        var dismissed = _shoppingListRepository.GetDismissedIngredientBaseIds(userId);

        var grouped = ingredients
            .GroupBy(i => (IngredientNameNormalizer.NormalizeKey(i.IngredientBase.Name), i.Measurement.Id))
            .Select(g => new ShoppingListItem
            {
                UserId = userId,
                IngredientBase = g.First().IngredientBase,
                Measurement = g.First().Measurement,
                Amount = g.Sum(i => i.Amount),
                IsAutoAdded = true
            });

        foreach (var item in grouped)
        {
            if (dismissed.Contains(item.IngredientBase.Id))
                continue;

            var normalizedName = IngredientNameNormalizer.NormalizeKey(item.IngredientBase.Name);
            var alreadyCovered = manualItems.Any(m =>
                string.Equals(
                    IngredientNameNormalizer.NormalizeKey(m.IngredientBase.Name),
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase) &&
                m.MeasurementId == item.Measurement.Id);
            if (!alreadyCovered)
                _shoppingListRepository.Add(item);
        }
    }

    public void AddItem(string userId, string itemName, float amount, string measurement, string? displayAmount = null)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("Item name cannot be empty.");

        var ingredientBase = _ingredientBaseRepo.FindOrCreateByName(itemName);

        var trimmed = measurement.Trim();
        var measurementEntity = _measurementRepo.ReadAll()
            .FirstOrDefault(m => m.Name.ToLower() == trimmed.ToLower()
                              || m.Abbreviation.ToLower() == trimmed.ToLower());
        if (measurementEntity == null)
            throw new ArgumentException($"Unknown measurement '{trimmed}'.");

        _shoppingListRepository.UnDismiss(userId, ingredientBase.Id);

        var item = new ShoppingListItem
        {
            UserId = userId,
            IngredientBase = ingredientBase,
            Measurement = measurementEntity,
            Amount = amount,
            DisplayAmount = displayAmount,
            IsAutoAdded = false
        };

        _shoppingListRepository.Add(item);
    }

    public void AddItemsBatch(string userId, IEnumerable<(string name, float amount, string measurement)> items)
    {
        var allMeasurements = _measurementRepo.ReadAll();
        var toAdd = new List<ShoppingListItem>();

        foreach (var (name, amount, measurement) in items)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            var trimmed = measurement.Trim();
            var measurementEntity = allMeasurements
                .FirstOrDefault(m => string.Equals(m.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(m.Abbreviation, trimmed, StringComparison.OrdinalIgnoreCase));
            if (measurementEntity == null) continue;

            var ingredientBase = _ingredientBaseRepo.FindOrCreateByName(name);

            toAdd.Add(new ShoppingListItem
            {
                UserId = userId,
                IngredientBase = ingredientBase,
                Measurement = measurementEntity,
                Amount = amount,
                IsAutoAdded = false
            });
        }

        _shoppingListRepository.AddBatch(toAdd);
    }

    public void RemoveItem(int itemId, string userId)
    {
        if (itemId <= 0)
            throw new ArgumentException("Invalid item id.");

        _shoppingListRepository.Remove(itemId, userId);
    }

    public void RemoveItemsByIngredientBase(string userId, int ingredientBaseId)
    {
        _shoppingListRepository.RemoveAllByIngredientBase(userId, ingredientBaseId);
    }

    public void UpdateItemAmount(string userId, int ingredientBaseId, float newAmount, string? displayAmount = null)
    {
        if (newAmount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        _shoppingListRepository.UpdateAmountByIngredientBase(userId, ingredientBaseId, newAmount, displayAmount);
    }

    public void ClearItems(string userId)
    {
        _shoppingListRepository.ClearAllItems(userId);
    }

    public bool UpdateItemMeasurement(string userId, int itemId, int measurementId)
    {
        return _shoppingListRepository.UpdateMeasurementById(userId, itemId, measurementId);
    }

    public IEnumerable<ShoppingListItem> GetItemsForUser(string userId)
    {
        return _shoppingListRepository.GetByUserId(userId);
    }
}
