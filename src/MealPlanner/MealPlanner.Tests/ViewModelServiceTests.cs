using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;

namespace MealPlanner.Tests;

[TestFixture]
public class ViewModelServiceTests
{
    [Test]
    public void RecipeFromRecipeVM_ReturnsRecipe()
    {
        // Arrange
        RecipeViewModel vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            Ingredients = ["i1_Base", "i2_Base"],
            IngredientAmounts = ["1", "1"],
            IngredientMeasurements = ["i1_Measure", "i2_Measure"],
            Calories = 1,
            Protein = 2,
            Carbs = 3,
            Fat = 4
        };

        // Act
        Recipe r = ViewModelService.RecipeFromRecipeVM(vm);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Name, Is.EqualTo("name"));
            Assert.That(r.Directions, Is.EqualTo("dir"));
            Assert.That(r.Calories, Is.EqualTo(1));
            Assert.That(r.Protein, Is.EqualTo(2));
            Assert.That(r.Carbs, Is.EqualTo(3));
            Assert.That(r.Fat, Is.EqualTo(4));
            Assert.That(r.Ingredients[0].Amount, Is.EqualTo(1));
            Assert.That(r.Ingredients[0].DisplayName, Is.EqualTo("i1_Base"));
            Assert.That(r.Ingredients[0].IngredientBase.Name, Is.EqualTo(IngredientNameNormalizer.NormalizeKey("i1_Base")));
            Assert.That(r.Ingredients[0].Measurement.Name, Is.EqualTo("i1_Measure"));
            Assert.That(r.Ingredients[1].Amount, Is.EqualTo(1));
            Assert.That(r.Ingredients[1].DisplayName, Is.EqualTo("i2_Base"));
            Assert.That(r.Ingredients[1].IngredientBase.Name, Is.EqualTo(IngredientNameNormalizer.NormalizeKey("i2_Base")));
            Assert.That(r.Ingredients[1].Measurement.Name, Is.EqualTo("i2_Measure"));
        }
    }

    [Test]
    public void RecipeFromRecipeVM_MapsTagsToRecipe()
    {
        // Arrange
        var vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            Tags = ["Breakfast", "Vegan"]
        };

        // Act
        Recipe r = ViewModelService.RecipeFromRecipeVM(vm);

        // Assert
        Assert.That(r.Tags.Select(t => t.Name), Is.EquivalentTo(new[] { "Breakfast", "Vegan" }));
    }

    [Test]
    public void RecipeFromRecipeVM_IgnoresBlankTags()
    {
        // Arrange
        var vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            Tags = ["Breakfast", "", "  "]
        };

        // Act
        Recipe r = ViewModelService.RecipeFromRecipeVM(vm);

        // Assert
        Assert.That(r.Tags.Count, Is.EqualTo(1));
    }

    [Test]
    public void RecipeFromRecipeVM_TrimsTagWhitespace()
    {
        // Arrange
        var vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            Tags = ["  Breakfast  "]
        };

        // Act
        Recipe r = ViewModelService.RecipeFromRecipeVM(vm);

        // Assert
        Assert.That(r.Tags[0].Name, Is.EqualTo("Breakfast"));
    }

    [Test]
    public void RecipeToRecipeVM_ReturnsViewModel()
    {
        // Arrange
        Ingredient i1 = new Ingredient
        {
            DisplayName = "i1_display",
            Amount = 1,
            IngredientBase = new IngredientBase { Name = "i1_base"},
            Measurement = new Measurement { Name = "i1_measure" }
        };
        Ingredient i2 = new Ingredient
        {
            DisplayName = "i2_display",
            Amount = 2,
            IngredientBase = new IngredientBase { Name = "i2_base"},
            Measurement = new Measurement { Name = "i2_measure" }
        };

        Recipe r = new Recipe
        {
            Name = "name",
            Directions = "dir",
            Ingredients = [i1, i2],
            Calories = 1,
            Protein = 2,
            Carbs = 3,
            Fat = 4
        };

        // Act
        RecipeViewModel vm = ViewModelService.RecipeToRecipeVM(r);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vm.Name, Is.EqualTo("name"));
            Assert.That(vm.Directions, Is.EqualTo("dir"));
            Assert.That(vm.Calories, Is.EqualTo(1));
            Assert.That(vm.Protein, Is.EqualTo(2));
            Assert.That(vm.Carbs, Is.EqualTo(3));
            Assert.That(vm.Fat, Is.EqualTo(4));
            Assert.That(vm.IngredientAmounts[0], Is.EqualTo("1"));
            Assert.That(vm.Ingredients[0], Is.EqualTo("i1_display"));
            Assert.That(vm.IngredientMeasurements[0], Is.EqualTo("i1_measure"));
            Assert.That(vm.IngredientAmounts[1], Is.EqualTo("2"));
            Assert.That(vm.Ingredients[1], Is.EqualTo("i2_display"));
            Assert.That(vm.IngredientMeasurements[1], Is.EqualTo("i2_measure"));
        }
    }

    [Test]
    public void RecipeToRecipeVM_MapsTagsToViewModel()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "name",
            Directions = "dir",
            Tags = [new Tag { Name = "Breakfast" }, new Tag { Name = "Vegan" }]
        };

        // Act
        RecipeViewModel vm = ViewModelService.RecipeToRecipeVM(recipe);

        // Assert
        Assert.That(vm.Tags, Is.EquivalentTo(new[] { "Breakfast", "Vegan" }));
    }

    [Test]
    public void EditRecipeVMToModel_UpdatesTags()
    {
        // Arrange
        var existing = new Recipe
        {
            Name = "name",
            Directions = "dir",
            Tags = [new Tag { Name = "Breakfast" }]
        };
        var vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            Tags = ["Dinner"]
        };

        // Act
        Recipe result = ViewModelService.EditRecipeVMToModel(existing, vm);

        // Assert
        Assert.That(result.Tags.Select(t => t.Name), Is.EquivalentTo(new[] { "Dinner" }));
    }

    [Test]
    public void IngredientFromPantryItemVM_ReturnsIngredientWithCorrectFields()
    {
        var vm = new PantryItemViewModel { Name = "Milk", Amount = 2f, Measurement = "cups" };

        var result = ViewModelService.IngredientFromPantryItemVM(vm);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Amount, Is.EqualTo(2f));
            Assert.That(result.DisplayName, Is.EqualTo("Milk"));
            Assert.That(result.IngredientBase.Name, Is.EqualTo(IngredientNameNormalizer.NormalizeKey("Milk")));
            Assert.That(result.Measurement.Name, Is.EqualTo("cups"));
        }
    }

    [Test]
    public void EditRecipeVMToModel_ClearsOldTags_WhenNoTagsSubmitted()
    {
        // Arrange
        var existing = new Recipe
        {
            Name = "name",
            Directions = "dir",
            Tags = [new Tag { Name = "Breakfast" }]
        };
        var vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            Tags = []
        };

        // Act
        Recipe result = ViewModelService.EditRecipeVMToModel(existing, vm);

        // Assert
        Assert.That(result.Tags, Is.Empty);
    }

    [Test]
    public void RecipeFromRecipeVM_MapsImageUrl()
    {
        var vm = new RecipeViewModel
        {
            Name = "name",
            Directions = "dir",
            ImageUrl = "/images/recipes/test.png"
        };

        Recipe r = ViewModelService.RecipeFromRecipeVM(vm);

        Assert.That(r.ImageUrl, Is.EqualTo("/images/recipes/test.png"));
    }

    [Test]
    public void RecipeFromRecipeVM_ImageUrl_IsNullWhenVmHasNoImage()
    {
        var vm = new RecipeViewModel { Name = "name", Directions = "dir" };

        Recipe r = ViewModelService.RecipeFromRecipeVM(vm);

        Assert.That(r.ImageUrl, Is.Null);
    }

    [Test]
    public void RecipeToRecipeVM_MapsImageUrl()
    {
        var recipe = new Recipe
        {
            Name = "name",
            Directions = "dir",
            ImageUrl = "/images/recipes/test.png"
        };

        RecipeViewModel vm = ViewModelService.RecipeToRecipeVM(recipe);

        Assert.That(vm.ImageUrl, Is.EqualTo("/images/recipes/test.png"));
    }

    [Test]
    public void EditRecipeVMToModel_MapsImageUrl()
    {
        var existing = new Recipe { Name = "name", Directions = "dir", ImageUrl = "/old.png" };
        var vm = new RecipeViewModel { Name = "name", Directions = "dir", ImageUrl = "/new.png" };

        Recipe result = ViewModelService.EditRecipeVMToModel(existing, vm);

        Assert.That(result.ImageUrl, Is.EqualTo("/new.png"));
    }

    [Test]
    public void EditRecipeVMToModel_ClearsImageUrl_WhenVmImageUrlIsNull()
    {
        var existing = new Recipe { Name = "name", Directions = "dir", ImageUrl = "/old.png" };
        var vm = new RecipeViewModel { Name = "name", Directions = "dir", ImageUrl = null };

        Recipe result = ViewModelService.EditRecipeVMToModel(existing, vm);

        Assert.That(result.ImageUrl, Is.Null);
    }
}
