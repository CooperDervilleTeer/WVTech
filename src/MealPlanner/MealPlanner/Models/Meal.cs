using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MealPlanner.Models;

[Table("Meal")]
public class Meal
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? RepeatRule { get; set; }

    // Comma-separated DayOfWeek integers (e.g. "1,3" = Monday+Wednesday).
    // Null means fall back to StartTime.DayOfWeek for backward compatibility.
    public string? RepeatDays { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsGenerated { get; set; }

    public List<Recipe> Recipes { get; set; } = [];

    public List<Tag> GetMealTags() =>
        Recipes.SelectMany(r => r.Tags).DistinctBy(t => t.Id).ToList();

    public void UpdateFromEdit(Meal editedMeal, IEnumerable<Recipe> selectedRecipes)
    {
        StartTime = editedMeal.StartTime;
        EndTime = editedMeal.EndTime;
        RepeatRule = editedMeal.RepeatRule;

        Recipes.Clear();
        if (selectedRecipes != null)
        {
            Recipes.AddRange(selectedRecipes);
        }
    }
}