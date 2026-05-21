using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using NUnit.Framework;
using Microsoft.IdentityModel.Tokens;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT99Steps
{
    IWebDriver _driver;
    string _baseUrl;
    MealPlannerDBContext _context;
    int _recipeId;
    string _excludedTag = string.Empty;
    string _selectedTag = string.Empty;

    [BeforeScenario]
    public void SetUp()
    {
        _context = BDDSetup.Context;
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
    }

    [Given("{string} selects the predefined tag {string}")]
    [When("{string} selects the predefined tag {string}")]
    public void GivenUserSelectsPredefinedTag(string username, string tag)
    {
        var select = new SelectElement(_driver.FindElement(By.Id("tag-select")));
        select.SelectByValue(tag);
    }

    [Given("{string} selects a predefined tag from the tag dropdown")]
    public void GivenUserSelectsAnyPredefinedTag(string username)
    {
        var select = new SelectElement(_driver.FindElement(By.Id("tag-select")));
        // Skip the placeholder option (index 0) and pick the first real tag (most popular)
        var firstOption = select.Options.FirstOrDefault(o => !string.IsNullOrEmpty(o.GetAttribute("value")));
        Assert.That(firstOption, Is.Not.Null, "No predefined tags available in the dropdown.");
        _selectedTag = firstOption!.GetAttribute("value");
        select.SelectByValue(_selectedTag);
    }

    [Then("the recipe {string} has the selected tag in the database")]
    public void ThenRecipeHasSelectedTagInDatabase(string recipeName)
    {
        Assert.That(_selectedTag, Is.Not.Empty, "No tag was selected in a prior step.");
        ThenRecipeHasTagInDatabase(recipeName, _selectedTag);
    }

    [Given("{string} adds the custom tag {string}")]
    public void GivenUserAddsCustomTag(string username, string tag)
    {
        var input = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try { var e = d.FindElement(By.Id("custom-tag-input")); return e.Displayed ? e : null; }
                catch (NoSuchElementException) { return null; }
            })!;
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", input);
        input.Clear();
        input.SendKeys(tag);
        var btn = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try { var e = d.FindElement(By.Id("add-custom-tag-btn")); return e.Displayed ? e : null; }
                catch (NoSuchElementException) { return null; }
            })!;
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", btn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);

        // Wait for the tag pill to appear in the DOM confirming the JS event fired
        new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d => d.FindElements(By.CssSelector("#tags-container .tag-wrapper")).Count > 0);
    }

    [Given("{string} has a recipe named {string} with the tag {string}")]
    public void GivenUserHasRecipeWithTag(string username, string recipeName, string tagName)
    {
        var ctx = BDDSetup.Context;
        var userId = SharedSteps.Users[username].Id;

        // Clean up any leftover recipe from previous runs
        var existing = ctx.Set<Recipe>()
            .Include(r => r.Tags)
            .FirstOrDefault(r => r.Name == recipeName);
        if (existing != null)
        {
            ctx.Remove(existing);
            ctx.SaveChanges();
        }

        // Find or create the tag
        var tag = ctx.Set<Tag>().FirstOrDefault(t => t.Name == tagName)
                  ?? new Tag { Name = tagName };

        var recipe = new Recipe
        {
            Name = recipeName,
            Directions = "Test directions",
            Calories = 200,
            Protein = 10,
            Fat = 5,
            Carbs = 30,
            Tags = [tag]
        };

        ctx.Add(recipe);
        ctx.SaveChanges();
        _recipeId = recipe.Id;

        ctx.Add(new UserRecipe
        {
            UserId = userId,
            RecipeId = _recipeId,
            UserOwner = true,
            UserFavorite = false,
            UserVote = UserVoteType.NoVote
        });
        ctx.SaveChanges();
    }

    [When("{string} views the recipe detail page for {string}")]
    public void WhenUserViewsRecipeDetailPage(string username, string recipeName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes/{_recipeId}");
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("the tag {string} is visible on the page")]
    public void ThenTagIsVisibleOnPage(string tag)
    {
        Assert.That(_driver.PageSource, Does.Contain(tag));
    }

    [Then("the recipe {string} has the tag {string} in the database")]
    public void ThenRecipeHasTagInDatabase(string recipeName, string tagName)
    {
        var ctx = BDDSetup.Context;
        var recipe = ctx.Set<Recipe>()
            .Include(r => r.Tags)
            .FirstOrDefault(r => r.Name == recipeName);

        Assert.That(recipe, Is.Not.Null, $"Recipe '{recipeName}' not found in database.");
        Assert.That(recipe!.Tags.Any(t => t.Name == tagName), Is.True,
            $"Recipe '{recipeName}' does not have tag '{tagName}'.");
    }

    [Given("{string} is on the edit recipe page for {string}")]
    public void GivenUserIsOnEditRecipePageFor(string username, string recipeName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/EditRecipe/{_recipeId}");
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [When("{string} submits the edit recipe form")]
    public void WhenUserSubmitsEditRecipeForm(string username)
    {
        var btn = _driver.FindElement(By.CssSelector("button.ar-submit-btn[type='submit']"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", btn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("there is only {int} tag with the name {string} in the database")]
    public void ThenThereIsOnlyNTagWithNameInDatabase(int expectedCount, string tagName)
    {
        var ctx = BDDSetup.Context;
        var actual = ctx.Set<Tag>()
            .Count(t => t.Name.ToLower() == tagName.ToLower());
        Assert.That(actual, Is.EqualTo(expectedCount),
            $"Expected {expectedCount} tag(s) matching '{tagName}' (case-insensitive) but found {actual}.");
    }

    [Given("there are 11 tags with varying usage counts")]
    public void GivenThereAre11TagsWithVaryingUsageCounts()
    {
        var ctx = BDDSetup.Context;

        // Remove any existing tags to start clean
        ctx.Set<Tag>().RemoveRange(ctx.Set<Tag>());
        ctx.SaveChanges();

        // Create 10 tags each used on 2 recipes (these should appear in top 10)
        for (int i = 1; i <= 10; i++)
        {
            var tag = new Tag { Name = $"PopularTag{i}" };
            ctx.AddRange(
                new Recipe { Name = $"PopularRecipe{i}A", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [tag] },
                new Recipe { Name = $"PopularRecipe{i}B", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [tag] }
            );
        }

        // Create 1 tag used on only 1 recipe (should be excluded when top 10 are present)
        _excludedTag = "RareTag";
        var rareTag = new Tag { Name = _excludedTag };
        ctx.Add(new Recipe { Name = "RareRecipe", Directions = "d", Calories = 100, Protein = 5, Fat = 2, Carbs = 10, Tags = [rareTag] });

        ctx.SaveChanges();
    }

    [Then("the tag dropdown shows exactly {int} options")]
    public void ThenTagDropdownShowsExactlyNOptions(int expectedCount)
    {
        var select = new SelectElement(_driver.FindElement(By.Id("tag-select")));
        // Subtract 1 for the placeholder "Select a tag..." option
        Assert.That(select.Options.Count - 1, Is.EqualTo(expectedCount));
    }

    [Then("the least used tag is not in the dropdown")]
    public void ThenLeastUsedTagIsNotInDropdown()
    {
        var select = _driver.FindElement(By.Id("tag-select"));
        var options = select.FindElements(By.TagName("option"));
        Assert.That(options.Any(o => o.GetAttribute("value") == _excludedTag), Is.False,
            $"Expected '{_excludedTag}' to be absent from the dropdown but it was present.");
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("there is a tag {string}")]
    public void GivenThereIsATag(string tagName)
    {
        if(!_context.Tags.Where(t => t.Name == tagName).IsNullOrEmpty()) return;
        _context.Add(new Tag() { Name=tagName });
        _context.SaveChanges();
    }
}
