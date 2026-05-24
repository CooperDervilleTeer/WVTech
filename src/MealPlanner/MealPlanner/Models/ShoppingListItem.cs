namespace MealPlanner.Models;

public class ShoppingListItem
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int IngredientBaseId { get; set; }
    public IngredientBase IngredientBase { get; set; } = null!;

    public int MeasurementId { get; set; }
    public Measurement Measurement { get; set; } = null!;

    public float Amount { get; set; }

    public string? DisplayAmount { get; set; }

    public bool IsAutoAdded { get; set; }
}
