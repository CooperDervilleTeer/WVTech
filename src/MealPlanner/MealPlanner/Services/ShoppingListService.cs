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
        var ingredients = meals.SelectMany(m => m.Recipes).SelectMany(r => r.Ingredients).ToList();
        SyncFromMeals(userId, ingredients);
    }

    private void SyncFromMeals(string userId, IEnumerable<Ingredient> ingredients)
    {
        _shoppingListRepository.RemoveAutoAddedByUserId(userId);

        var manualItems = _shoppingListRepository.GetByUserId(userId).ToList();

        var grouped = ingredients
            .GroupBy(i => (i.IngredientBase.Id, i.Measurement.Id))
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
            var alreadyCovered = manualItems.Any(m =>
                m.IngredientBaseId == item.IngredientBase.Id &&
                m.MeasurementId == item.Measurement.Id);
            if (!alreadyCovered)
                _shoppingListRepository.Add(item);
        }
    }

    public void AddItem(string userId, string itemName, float amount, string measurement)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("Item name cannot be empty.");

        var ingredientBase = _ingredientBaseRepo.FindOrCreateByName(itemName);

        var measurementName = string.IsNullOrWhiteSpace(measurement) ? "Unit" : measurement.Trim();
        var measurementEntity = _measurementRepo.FindOrCreate(
            m => m.Name.ToLower() == measurementName.ToLower(),
            () => new Measurement { Name = measurementName });

        var item = new ShoppingListItem
        {
            UserId = userId,
            IngredientBase = ingredientBase,
            Measurement = measurementEntity,
            Amount = amount,
            IsAutoAdded = false
        };

        _shoppingListRepository.Add(item);
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

    public void UpdateItemAmount(string userId, int ingredientBaseId, float newAmount)
    {
        if (newAmount < 0)
            throw new ArgumentException("Amount cannot be negative.");

        _shoppingListRepository.UpdateAmountByIngredientBase(userId, ingredientBaseId, newAmount);
    }

    public void ClearItems(string userId)
    {
        _shoppingListRepository.ClearAllItems(userId);
    }

    public IEnumerable<ShoppingListItem> GetItemsForUser(string userId)
    {
        return _shoppingListRepository.GetByUserId(userId)
            .OrderBy(i => i.IngredientBase.Name)
            .ToList();
    }
}
