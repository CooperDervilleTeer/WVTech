namespace MealPlanner.Models;

public class DismissedShoppingItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int IngredientBaseId { get; set; }
    public IngredientBase IngredientBase { get; set; } = null!;
}
