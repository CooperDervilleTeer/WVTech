using System.ComponentModel.DataAnnotations;

namespace MealPlanner.Models;

public enum MealSize
{
    [Display(Name = "Small Snack")]
    SmallSnack,
    [Display(Name = "Small")]
    Small,
    [Display(Name = "Average")]
    Average,
    [Display(Name = "Large")]
    Large,
    [Display(Name = "Large Snack")]
    LargeSnack
}

public static class MealSizeExtensions
{
    public static int Calories(this MealSize size) => size switch
    {
        MealSize.SmallSnack => 200,
        MealSize.Small      => 400,
        MealSize.Average    => 800,
        MealSize.Large      => 1200,
        MealSize.LargeSnack => 600,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };

    public static double Weight(this MealSize size) => size switch
    {
        MealSize.SmallSnack => 0.25,
        MealSize.Small      => 0.5,
        MealSize.Average    => 1.0,
        MealSize.Large      => 1.5,
        MealSize.LargeSnack => 0.75,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };

    public static int MaxRecipes(this MealSize size) => size switch
    {
        MealSize.SmallSnack => 1,
        MealSize.Small      => 2,
        MealSize.Average    => 3,
        MealSize.LargeSnack => 2,
        MealSize.Large      => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };

    public static bool IsSnack(this MealSize size) =>
        size == MealSize.SmallSnack || size == MealSize.LargeSnack;

    /// <summary>
    /// The fraction of a 2000 cal/day reference diet this size represents.
    /// Used to compare a meal's macro ratios against size thresholds uniformly
    /// across calories, protein, carbs, and fat.
    /// </summary>
    public static double Ratio(this MealSize size) => size.Calories() / 2000.0;
}
