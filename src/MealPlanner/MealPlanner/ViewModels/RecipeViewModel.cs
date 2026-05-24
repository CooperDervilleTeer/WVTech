using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MealPlanner.Models;
using MealPlanner.Services;

namespace MealPlanner.ViewModels;


public class RecipeViewModel
{
    public RecipeViewModel()
    {
    }

    public RecipeViewModel(Recipe recipe)
    {
        // Convert Ingredients list to strings

        // Change to LINQ or list comprehension
        List<IngredientViewModel> ingredients = []; // new List<string>(recipe.Ingredients)

        foreach (Ingredient i in recipe.Ingredients)
        {
            ingredients.Add(new IngredientViewModel(i));
        }

        Name = recipe.Name;
        Id = recipe.Id;
        Ingredients = recipe.Ingredients.Select(i => i.DisplayName).ToList();
        IngredientAmounts = recipe.Ingredients.Select(i => FractionParser.FormatAmount(i.Amount)).ToList();
        IngredientMeasurements = recipe.Ingredients.Select(i => i.Measurement.Name).ToList();
        Directions = recipe.Directions;
        Calories = recipe.Calories;
        Protein = recipe.Protein;
        Carbs = recipe.Carbs;
        Fat = recipe.Fat;
        Tags = recipe.Tags.Select(t => t.Name).ToList();
        ImageUrl = recipe.ImageUrl;
    }

    [Required(ErrorMessage = "A recipe needs a name")]
    public string Name { get; set; }
    public int? Id { get; set; }
    public List<string> Ingredients { get; set; } = [];
    public List<string> IngredientAmounts { get; set; } = [];
    public List<string> IngredientMeasurements { get; set; } = [];

    [Required(ErrorMessage = "A recipe needs directions")]
    public string Directions { get; set; }

    [Range(0, 32767)]
    public int Calories { get; set; }

    [Range(0, 32767)]
    public int Protein { get; set; }

    [Range(0, 32767)]
    public int Carbs { get; set; }
    
    [Range(0, 32767)]
    public int Fat { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> AvailableTags { get; set; } = [];
    public List<Measurement> Measurements { get; set; } = [];
    public bool IsOwned { get; set;} = false;
    public UserVoteType UserVote { get; set; } = UserVoteType.NoVote;
    public float? VotePercentage { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public bool RemoveImage { get; set; } = false;
}