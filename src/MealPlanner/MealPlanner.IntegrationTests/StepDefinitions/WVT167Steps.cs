using MealPlanner.Models;
using Microsoft.AspNetCore.Identity;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT167Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly WebDriverWait _wait;

    private int _testRecipeId;
    private int _highCalRecipeId;
    private int _lowCalRecipeId;
    private int _testMealId;
    private string? _imageFilePath;

    private const string NewRecipeName = "WVT167NewRecipe";
    private const string ExistingRecipeName = "WVT167ExistingRecipe";
    private const string HighCalRecipeName = "WVT167HighCal";
    private const string LowCalRecipeName = "WVT167LowCal";

    // Minimal valid 1×1 PNG bytes
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
        0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    public WVT167Steps()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
    }

    private string GetGaryId(MealPlannerDBContext ctx) =>
        ctx.Users.First(u => u.Email == "Gary@fakeemail.com").Id;

    private string CreateTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wvt167-{Guid.NewGuid()}.png");
        File.WriteAllBytes(path, MinimalPng);
        return path;
    }

    private Recipe SeedRecipe(MealPlannerDBContext ctx, string name, string? imageUrl)
    {
        var existing = ctx.Recipes.FirstOrDefault(r => r.Name == name);
        if (existing != null)
        {
            existing.ImageUrl = imageUrl;
            ctx.SaveChanges();
            return existing;
        }

        var recipe = new Recipe
        {
            Name = name,
            Directions = "Test directions",
            Calories = 300,
            Protein = 10,
            Carbs = 30,
            Fat = 5,
            ImageUrl = imageUrl
        };
        ctx.Recipes.Add(recipe);
        ctx.SaveChanges();
        return recipe;
    }

    private void SeedUserRecipe(MealPlannerDBContext ctx, string userId, int recipeId)
    {
        var existing = ctx.Set<UserRecipe>().FirstOrDefault(ur => ur.UserId == userId && ur.RecipeId == recipeId);
        if (existing == null)
        {
            ctx.Set<UserRecipe>().Add(new UserRecipe
            {
                UserId = userId,
                RecipeId = recipeId,
                UserOwner = true,
                UserVote = UserVoteType.NoVote
            });
            ctx.SaveChanges();
        }
        else if (!existing.UserOwner)
        {
            existing.UserOwner = true;
            ctx.SaveChanges();
        }
    }

    [AfterScenario]
    public void CleanupTestImages()
    {
        // Scenario 7: file written directly — delete if the test failed before DeleteRecipe ran
        if (_imageFilePath != null && File.Exists(_imageFilePath))
            File.Delete(_imageFilePath);

        // Scenarios 1 & 2: files saved by the server — look up the ImageUrl from the DB and delete
        var webRoot = GetAutWebRootPath();
        var trackedNames = new[] { NewRecipeName, ExistingRecipeName, "WVT167DeleteImageRecipe" };
        using var ctx = BDDSetup.CreateContext();
        var recipes = ctx.Recipes
            .Where(r => trackedNames.Contains(r.Name) && r.ImageUrl != null && r.ImageUrl.StartsWith("/images/recipes/"))
            .ToList();

        foreach (var recipe in recipes)
        {
            var filePath = Path.Combine(webRoot,
                recipe.ImageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    // --- Scenario 1: Upload image when creating recipe ---

    [Given("'Gary' navigates to the create recipe page")]
    public void GivenGaryNavigatesToCreateRecipePage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/AddNewRecipe");
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [When("'Gary' fills in the recipe details and uploads an image file")]
    public void WhenGaryFillsInRecipeDetailsAndUploadsImage()
    {
        _driver.FindElement(By.Id("Name")).Clear();
        _driver.FindElement(By.Id("Name")).SendKeys(NewRecipeName);
        var removeBtns = _driver.FindElements(By.CssSelector("#step-list .ar-step-remove-btn")).ToList();
        foreach (var btn in removeBtns)
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        var stepInput = _driver.FindElement(By.Id("step-input"));
        stepInput.SendKeys("Test directions");
        stepInput.SendKeys(Keys.Enter);
        _driver.FindElement(By.Id("Calories")).Clear();
        _driver.FindElement(By.Id("Calories")).SendKeys("400");
        _driver.FindElement(By.Id("Protein")).Clear();
        _driver.FindElement(By.Id("Protein")).SendKeys("20");
        _driver.FindElement(By.Id("Carbs")).Clear();
        _driver.FindElement(By.Id("Carbs")).SendKeys("40");
        _driver.FindElement(By.Id("Fat")).Clear();
        _driver.FindElement(By.Id("Fat")).SendKeys("10");

        var tempImage = CreateTempImage();
        _driver.FindElement(By.Id("imageFile")).SendKeys(tempImage);
    }

    [When("'Gary' submits the new recipe")]
    public void WhenGarySubmitsTheNewRecipe()
    {
        var btn = _driver.FindElement(By.CssSelector("button[type=submit]"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("'Gary' sees the recipe page with the uploaded image displayed")]
    public void ThenGarySeesRecipePageWithUploadedImage()
    {
        using var ctx = BDDSetup.CreateContext();
        var recipe = ctx.Recipes.OrderByDescending(r => r.Id).First(r => r.Name == NewRecipeName);
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes/{recipe.Id}");
        _wait.Until(d => d.Url.Contains("Recipes"));

        var img = _wait.Until(d => d.FindElement(By.CssSelector(".recipe-image.recipe-has-image")));
        Assert.That(img, Is.Not.Null);
    }

    // --- Scenarios 2 & 3: Edit recipe image ---

    [Given("'Gary' has a WVT167 recipe with an image")]
    public void GivenGaryHasAWvt167RecipeWithAnImage()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetGaryId(ctx);
        var recipe = SeedRecipe(ctx, ExistingRecipeName, "/images/icons/meal.png");
        _testRecipeId = recipe.Id;
        SeedUserRecipe(ctx, userId, recipe.Id);
    }

    [Given("'Gary' navigates to the edit recipe page for that recipe")]
    public void GivenGaryNavigatesToEditRecipePage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/EditRecipe/{_testRecipeId}");
        _wait.Until(d => d.Url.Contains("EditRecipe"));
    }

    [When("'Gary' uploads a different image file")]
    public void WhenGaryUploadsDifferentImageFile()
    {
        var tempImage = CreateTempImage();
        var fileInput = _driver.FindElement(By.Id("imageFile"));
        fileInput.SendKeys(tempImage);
    }

    [When("'Gary' saves the recipe")]
    public void WhenGarySavesTheRecipe()
    {
        var btn = _driver.FindElement(By.CssSelector("button[type=submit]"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("'Gary' sees the recipe page with the new image displayed")]
    public void ThenGarySeesRecipePageWithNewImage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes/{_testRecipeId}");
        _wait.Until(d => d.Url.Contains("Recipes"));

        var img = _wait.Until(d => d.FindElement(By.CssSelector(".recipe-image.recipe-has-image")));
        Assert.That(img.GetAttribute("src"), Does.Contain("/images/recipes/"));
    }

    [When("'Gary' removes the recipe image")]
    public void WhenGaryRemovesRecipeImage()
    {
        var btn = _wait.Until(d => d.FindElement(By.Id("removeImageBtn")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
    }

    [Then("'Gary' sees the recipe page with the placeholder image displayed")]
    public void ThenGarySeesRecipePageWithPlaceholder()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes/{_testRecipeId}");
        _wait.Until(d => d.Url.Contains("Recipes"));

        var img = _wait.Until(d => d.FindElement(By.CssSelector(".recipe-image.recipe-no-image")));
        Assert.That(img, Is.Not.Null);
    }

    // --- Scenarios 4 & 5: Search result thumbnails ---

    [Given("'Gary' has a recipe named {string} with no image")]
    public void GivenGaryHasARecipeWithNoImage(string name)
    {
        using var ctx = BDDSetup.CreateContext();
        SeedRecipe(ctx, name, null);
    }

    [Given("'Gary' has a recipe named {string} with an image")]
    public void GivenGaryHasARecipeWithAnImage(string name)
    {
        using var ctx = BDDSetup.CreateContext();
        SeedRecipe(ctx, name, "/images/icons/meal.png");
    }

    // 'Gary' searches for {string} is handled by WVT101Steps.WhenUserSearchesFor

    [Then("'Gary' sees a placeholder image next to {string} in the search results")]
    public void ThenGarySeesPlaceholderImageInSearchResults(string recipeName)
    {
        var row = _wait.Until(d =>
            d.FindElements(By.CssSelector(".recipeSearchRow"))
             .FirstOrDefault(r => r.FindElement(By.CssSelector(".recipeName")).Text.Contains(recipeName)));
        Assert.That(row, Is.Not.Null, $"Could not find search result row for '{recipeName}'");

        var img = row.FindElement(By.CssSelector(".recipe-thumbnail.recipe-no-image"));
        Assert.That(img, Is.Not.Null);
    }

    [Then("'Gary' sees the recipe image thumbnail next to {string} in the search results")]
    public void ThenGarySeesRecipeImageThumbnailInSearchResults(string recipeName)
    {
        var row = _wait.Until(d =>
            d.FindElements(By.CssSelector(".recipeSearchRow"))
             .FirstOrDefault(r => r.FindElement(By.CssSelector(".recipeName")).Text.Contains(recipeName)));
        Assert.That(row, Is.Not.Null, $"Could not find search result row for '{recipeName}'");

        var img = row.FindElement(By.CssSelector(".recipe-thumbnail.recipe-has-image"));
        Assert.That(img, Is.Not.Null);
    }

    // --- Scenario 6: Home page collage ---

    [Given("'Gary' has a meal planned for today containing multiple recipes with images")]
    public void GivenGaryHasAMealWithMultipleRecipesWithImages()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetGaryId(ctx);

        var existing = ctx.Meals.Where(m => m.UserId == userId && m.Title == "WVT167CollageMeal").ToList();
        ctx.Meals.RemoveRange(existing);
        ctx.SaveChanges();

        var highCal = SeedRecipe(ctx, HighCalRecipeName, "/images/icons/meal.png");
        highCal.Calories = 500;
        ctx.SaveChanges();
        _highCalRecipeId = highCal.Id;

        var lowCal = SeedRecipe(ctx, LowCalRecipeName, "/images/icons/ingredient.png");
        lowCal.Calories = 200;
        ctx.SaveChanges();
        _lowCalRecipeId = lowCal.Id;

        var meal = new Meal
        {
            UserId = userId,
            Title = "WVT167CollageMeal",
            StartTime = DateTime.Today.AddHours(12)
        };
        meal.Recipes.Add(highCal);
        meal.Recipes.Add(lowCal);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [When("'Gary' visits the home page")]
    public void WhenGaryVisitsHomePage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/?date={DateTime.Today:yyyy-MM-dd}");
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("'Gary' sees multiple recipe images displayed on the meal card")]
    public void ThenGarySeesMultipleRecipeImages()
    {
        var mealCard = _wait.Until(d =>
            d.FindElements(By.CssSelector(".mealCardWrapper"))
             .FirstOrDefault(item => item.Text.Contains("WVT167CollageMeal")));
        Assert.That(mealCard, Is.Not.Null, "Could not find WVT167CollageMeal card on home page");

        var images = mealCard.FindElements(By.CssSelector(".recipe-collage-img"));
        Assert.That(images.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 recipe images in the collage");
    }

    [Then("the recipe images on the meal card appear in order from highest to lowest calorie count")]
    public void ThenRecipeImagesAreOrderedByCalories()
    {
        var mealCard = _wait.Until(d =>
            d.FindElements(By.CssSelector(".mealCardWrapper"))
             .FirstOrDefault(item => item.Text.Contains("WVT167CollageMeal")));
        Assert.That(mealCard, Is.Not.Null);

        var images = mealCard.FindElements(By.CssSelector(".recipe-collage-img"));
        Assert.That(images.Count, Is.GreaterThanOrEqualTo(2));

        var calories = images
            .Select(img => int.Parse(img.GetAttribute("data-calories") ?? "0"))
            .ToList();

        for (int i = 0; i < calories.Count - 1; i++)
        {
            Assert.That(calories[i], Is.GreaterThanOrEqualTo(calories[i + 1]),
                $"Image at position {i} ({calories[i]} cal) should come before position {i + 1} ({calories[i + 1]} cal)");
        }
    }

    // --- Scenario 7: Deleting a recipe removes its image file ---

    private static string GetAutWebRootPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MealPlanner", "wwwroot"));

    [Given("'Gary' has a WVT167 recipe with an uploaded image file on disk")]
    public void GivenGaryHasAWvt167RecipeWithAnUploadedImageFileOnDisk()
    {
        var webRoot = GetAutWebRootPath();
        var dir = Path.Combine(webRoot, "images", "recipes");
        Directory.CreateDirectory(dir);
        var fileName = $"wvt167-delete-test-{Guid.NewGuid()}.png";
        _imageFilePath = Path.Combine(dir, fileName);
        File.WriteAllBytes(_imageFilePath, MinimalPng);

        using var ctx = BDDSetup.CreateContext();
        var userId = GetGaryId(ctx);
        var recipe = SeedRecipe(ctx, "WVT167DeleteImageRecipe", $"/images/recipes/{fileName}");
        _testRecipeId = recipe.Id;
        SeedUserRecipe(ctx, userId, recipe.Id);
    }

    [When("'Gary' deletes that recipe")]
    public void WhenGaryDeletesThatRecipe()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes");
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");

        var btn = _wait.Until(d =>
            d.FindElement(By.CssSelector($"[data-recipe-id='{_testRecipeId}'] .delete-recipe-btn")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);

        var confirmBtn = _wait.Until(d => d.FindElement(By.CssSelector(".inline-confirm-yes")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", confirmBtn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", confirmBtn);

        // Wait until the JS fetch completes and removes the card from the DOM
        _wait.Until(d => d.FindElements(By.CssSelector($"[data-recipe-id='{_testRecipeId}']")).Count == 0);
    }

    [Then("the image file no longer exists on the server")]
    public void ThenTheImageFileNoLongerExistsOnTheServer()
    {
        Assert.That(_imageFilePath, Is.Not.Null);
        Assert.That(File.Exists(_imageFilePath), Is.False, $"Expected image file to be deleted: {_imageFilePath}");
    }
}
