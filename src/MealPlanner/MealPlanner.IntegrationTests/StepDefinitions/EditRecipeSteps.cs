using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using NUnit.Framework;

namespace Mealplanner.IntegrationTests;

[Binding]
public class EditRecipeSteps
{
    IWebDriver _driver;
    string _baseUrl;
    readonly string _emailBase = "@fakeemail.com";
    int _recipeId;

    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
    }

    [Given("{string} has a recipe to edit")]
    public void GivenUserHasARecipeToEdit(string username)
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = ctx.Users
            .First(u => u.NormalizedEmail == $"{username}{_emailBase}".ToUpper()).Id;

        // clean up any leftover test recipes from previous runs
        var existing = ctx.Set<Recipe>()
            .FirstOrDefault(r => r.Name == "EditTestRecipe");
        if (existing != null)
        {
            ctx.Remove(existing);
            ctx.SaveChanges();
        }

        var recipe = new Recipe
        {
            Name = "EditTestRecipe",
            Directions = "Edit test directions",
            Calories = 20,
            Protein = 30,
            Fat = 40,
            Carbs = 50
        };

        ctx.Add(recipe);
        ctx.SaveChanges();
        _recipeId = recipe.Id;

        // link recipe to user so bob can edit it
        ctx.Add(new UserRecipe
        {
            UserId = userId,
            RecipeId = _recipeId,
            UserOwner = true,
            UserFavorite = false,
            UserVote = 0
        });
        ctx.SaveChanges();
    }

    [Given("{string} is on the edit recipe page")]
    public void GivenUserIsOnEditRecipePage(string username)
    {
        string expectedPath = $"/FoodEntries/EditRecipe/{_recipeId}";
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}{expectedPath}");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState").ToString() == "complete");

            // Pending login redirect can cause Navigate to land on "/" — retry.
            if (!_driver.Url.Contains(expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                if (_driver.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
                {
                    _driver.FindElement(By.Id("Email")).SendKeys($"{username}{_emailBase}");
                    _driver.FindElement(By.Id("Password")).SendKeys("1234!Abcd");
                    _driver.FindElement(By.ClassName("btn")).Click();
                    new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
                        !d.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase));
                }
                continue;
            }

            try
            {
                new WebDriverWait(_driver, TimeSpan.FromSeconds(5)).Until(driver =>
                {
                    try { return driver.FindElement(By.Id("step-input")).Displayed; }
                    catch (NoSuchElementException) { return false; }
                    catch (StaleElementReferenceException) { return false; }
                });
                return;
            }
            catch (WebDriverTimeoutException)
            {
                if (attempt == maxAttempts - 1) throw;
            }
        }
    }

    [Then("the recipe name field contains {string}")]
    public void ThenRecipeNameFieldContains(string expected)
    {
        var field = _driver.FindElement(By.Id("Name"));
        Assert.That(field.GetAttribute("value"), Is.EqualTo(expected));
    }

    [Then("the recipe directions field contains {string}")]
    public void ThenRecipeDirectionsFieldContains(string expected)
    {
        var firstStep = _driver.FindElement(By.CssSelector("#step-list .ar-step-text"));
        Assert.That(firstStep.Text, Is.EqualTo(expected));
    }

    [Then("the recipe calories field contains {string}")]
    public void ThenRecipeCaloriesFieldContains(string expected)
    {
        var field = _driver.FindElement(By.Id("Calories"));
        Assert.That(field.GetAttribute("value"), Is.EqualTo(expected));
    }

    [Then("the recipe protein field contains {string}")]
    public void ThenRecipeProteinFieldContains(string expected)
    {
        var field = _driver.FindElement(By.Id("Protein"));
        Assert.That(field.GetAttribute("value"), Is.EqualTo(expected));
    }

    [Then("the recipe fat field contains {string}")]
    public void ThenRecipeFatFieldContains(string expected)
    {
        var field = _driver.FindElement(By.Id("Fat"));
        Assert.That(field.GetAttribute("value"), Is.EqualTo(expected));
    }

    [Then("the recipe carbs field contains {string}")]
    public void ThenRecipeCarbsFieldContains(string expected)
    {
        var field = _driver.FindElement(By.Id("Carbs"));
        Assert.That(field.GetAttribute("value"), Is.EqualTo(expected));
    }

    [Given("{string} clears the recipe name")]
    public void GivenUserClearsRecipeName(string username)
    {
        var field = _driver.FindElement(By.Id("Name"));
        field.Clear();
    }

    [Given("{string} clears the recipe directions")]
    public void GivenUserClearsRecipeDirections(string username)
    {
        var removeBtns = _driver.FindElements(By.CssSelector("#step-list .ar-step-remove-btn")).ToList();
        foreach (var btn in removeBtns)
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
    }

    [Given("{string} clears the recipe calories")]
    public void GivenUserClearsRecipeCalories(string username)
    {
        var field = _driver.FindElement(By.Id("Calories"));
        field.Clear();
    }

    [Given("{string} clears the recipe protein")]
    public void GivenUserClearsRecipeProtein(string username)
    {
        var field = _driver.FindElement(By.Id("Protein"));
        field.Clear();
    }

    [Given("{string} clears the recipe fat")]
    public void GivenUserClearsRecipeFat(string username)
    {
        var field = _driver.FindElement(By.Id("Fat"));
        field.Clear();
    }

    [Given("{string} clears the recipe carbs")]
    public void GivenUserClearsRecipeCarbs(string username)
    {
        var field = _driver.FindElement(By.Id("Carbs"));
        field.Clear();
    }

    [Then("{string} remains on the edit recipe page")]
    public void ThenUserRemainsOnEditRecipePage(string username)
    {
        Assert.That(_driver.Url, Does.Contain("/FoodEntries/EditRecipe"));
    }

    [Then("{string} is redirected away from the edit recipe page")]
    public void ThenUserIsRedirectedAwayFromEditRecipePage(string username)
    {
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            !d.Url.Contains("/FoodEntries/EditRecipe"));
    }
}