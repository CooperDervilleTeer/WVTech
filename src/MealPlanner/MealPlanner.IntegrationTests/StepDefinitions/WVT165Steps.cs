using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using NUnit.Framework;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT165Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly WebDriverWait _wait;

    private int _testMealId;
    private int _testRecipeId;
    private string _testRecipeName = string.Empty;

    private const string RecipeName = "WVT165TestRecipe";

    public WVT165Steps()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
    }

    private string GetFrankId(MealPlannerDBContext ctx) =>
        ctx.Users.First(u => u.Email == "Frank@fakeemail.com").Id;

    private Recipe CreateTestRecipe(MealPlannerDBContext ctx)
    {
        var existing = ctx.Recipes.FirstOrDefault(r => r.Name == RecipeName);
        if (existing != null) return existing;

        var recipe = new Recipe
        {
            Name = RecipeName,
            Directions = "Test",
            Calories = 100,
            Protein = 5,
            Carbs = 10,
            Fat = 3
        };
        ctx.Recipes.Add(recipe);
        ctx.SaveChanges();
        return recipe;
    }

    [Given("'Frank' has a meal with a recipe")]
    public void GivenFrankHasAMealWithARecipe()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetFrankId(ctx);

        var existing = ctx.Meals.Where(m => m.UserId == userId && m.Title == "WVT165Meal").ToList();
        ctx.Meals.RemoveRange(existing);
        ctx.SaveChanges();

        var recipe = CreateTestRecipe(ctx);
        _testRecipeName = recipe.Name;
        _testRecipeId = recipe.Id;

        var meal = new Meal
        {
            UserId = userId,
            Title = "WVT165Meal",
            StartTime = DateTime.Today.AddHours(12)
        };
        meal.Recipes.Add(recipe);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [Given("'Frank' has a meal")]
    public void GivenFrankHasAMeal()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetFrankId(ctx);

        var existing = ctx.Meals.Where(m => m.UserId == userId && m.Title == "WVT165Meal").ToList();
        ctx.Meals.RemoveRange(existing);
        ctx.SaveChanges();

        var meal = new Meal
        {
            UserId = userId,
            Title = "WVT165Meal",
            StartTime = DateTime.Today.AddHours(12)
        };
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [Given("'Frank' has a meal scheduled for today")]
    public void GivenFrankHasAMealScheduledForToday()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetFrankId(ctx);

        var existing = ctx.Meals.Where(m => m.UserId == userId && m.Title == "WVT165HomeMeal").ToList();
        ctx.Meals.RemoveRange(existing);
        ctx.SaveChanges();

        var meal = new Meal
        {
            UserId = userId,
            Title = "WVT165HomeMeal",
            StartTime = DateTime.Today.AddHours(12)
        };
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [Given("'Frank' has an owned recipe in the recipe library")]
    public void GivenFrankHasAnOwnedRecipeInLibrary()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetFrankId(ctx);
        var recipe = CreateTestRecipe(ctx);
        _testRecipeName = recipe.Name;
        _testRecipeId = recipe.Id;

        var existing = ctx.Set<UserRecipe>().FirstOrDefault(ur => ur.UserId == userId && ur.RecipeId == recipe.Id);
        if (existing == null)
        {
            ctx.Set<UserRecipe>().Add(new UserRecipe
            {
                UserId = userId,
                RecipeId = recipe.Id,
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

    [Given("'Frank' is on the view meal page")]
    public void GivenFrankIsOnViewMealPage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/ViewMeal?id={_testMealId}");
        _wait.Until(d => d.Url.Contains("ViewMeal"));
    }

    [Given("'Frank' is on the edit meal page")]
    public void GivenFrankIsOnEditMealPage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/EditMeal?id={_testMealId}");
        _wait.Until(d => d.Url.Contains("EditMeal"));
    }

    [Given("'Frank' is on the create meal page with a recipe added to the list")]
    public void GivenFrankIsOnCreateMealPageWithRecipeAdded()
    {
        using var ctx = BDDSetup.CreateContext();
        var recipe = CreateTestRecipe(ctx);
        _testRecipeName = recipe.Name;

        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");

        var searchBox = _wait.Until(d => d.FindElement(By.Id("searchText")));

        // Set value and fire via jQuery's trigger so the registered .on("input") handler reliably fires
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "$(arguments[0]).val(arguments[1]).trigger('input');",
            searchBox, RecipeName);

        _wait.Until(d => d.FindElements(By.CssSelector(".recipeSearchRow")).Count > 0);

        var result = _driver.FindElements(By.CssSelector(".recipeSearchRow"))
            .FirstOrDefault(r => r.FindElement(By.CssSelector(".recipeName")).Text.Contains(RecipeName));

        Assert.That(result, Is.Not.Null, $"Recipe '{RecipeName}' not found in search results");
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", result);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", result);

        // Multi-select: clicking a row toggles selection; commit via the "Add selected" button
        var addBtn = _wait.Until(d =>
        {
            var btn = d.FindElement(By.Id("addSelectedRecipesBtn"));
            return btn.Displayed ? btn : null;
        });
        addBtn.Click();

        _wait.Until(d => d.FindElements(By.CssSelector("#createMealList .delete-recipe-btn")).Count > 0);
    }

    [Given("'Frank' is on the recipe library page")]
    public void GivenFrankIsOnRecipeLibraryPage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes");
        _wait.Until(d => d.Url.Contains("Recipes"));
    }

    [Given("'Frank' is on the planner home page")]
    public void GivenFrankIsOnHomePage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/PlannerHome?date={DateTime.Today:yyyy-MM-dd}");
        _wait.Until(d => d.Url.Contains("PlannerHome"));
    }

    [When("'Frank' clicks the remove recipe button")]
    [When("'Frank' clicks the delete recipe button")]
    public void WhenFrankClicksDeleteRecipeButton()
    {
        var btn = _wait.Until(d => d.FindElement(By.CssSelector(".delete-recipe-btn")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
        btn.Click();
    }

    [When("'Frank' clicks the delete meal button")]
    public void WhenFrankClicksDeleteMealButton()
    {
        var btn = _wait.Until(d => d.FindElement(By.CssSelector(".btn-delete-meal")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
        btn.Click();
    }

    [Then("an in-app confirmation is shown instead of a browser dialog")]
    public void ThenInAppConfirmationIsShown()
    {
        // Verify no browser alert was triggered
        try
        {
            _driver.SwitchTo().Alert();
            Assert.Fail("A browser alert dialog appeared — expected an in-app confirmation instead.");
        }
        catch (NoAlertPresentException) { }

        // Verify the in-app confirmation element is visible
        var confirm = _wait.Until(d =>
        {
            var el = d.FindElements(By.CssSelector(".inline-confirm")).FirstOrDefault();
            return el != null && el.Displayed ? el : null;
        });
        Assert.That(confirm, Is.Not.Null, "Expected .inline-confirm element to be visible");
    }

    [When("'Frank' clicks cancel on the in-app confirmation")]
    public void WhenFrankClicksCancelOnConfirmation()
    {
        var cancelBtn = _wait.Until(d => {
            try
            {
                var el = d.FindElement(By.CssSelector(".inline-confirm-no"));
                return el.Displayed && el.Enabled ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", cancelBtn);
        try { cancelBtn.Click(); }
        catch (ElementClickInterceptedException) { ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", cancelBtn); }
    }

    [When("'Frank' clicks confirm on the in-app confirmation")]
    public void WhenFrankClicksConfirmOnConfirmation()
    {
        var confirmBtn = _wait.Until(d => {
            try
            {
                var el = d.FindElement(By.CssSelector(".inline-confirm-yes"));
                return el.Displayed && el.Enabled ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", confirmBtn);
        try { confirmBtn.Click(); }
        catch (ElementClickInterceptedException) { ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", confirmBtn); }
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("the recipe is still visible on the page")]
    public void ThenRecipeIsStillVisible()
    {
        // Confirmation should have dismissed
        _wait.Until(d => !d.FindElements(By.CssSelector(".inline-confirm"))
            .Any(e => e.Displayed));
        Assert.That(
            _driver.FindElements(By.CssSelector(".delete-recipe-btn")).Count,
            Is.GreaterThan(0),
            "Expected at least one recipe row to remain");
    }

    [Then("the recipe is no longer visible on the page")]
    public void ThenRecipeIsNoLongerVisible()
    {
        _wait.Until(d => !d.FindElements(By.CssSelector(".delete-recipe-btn")).Any());
        Assert.That(
            _driver.FindElements(By.CssSelector(".delete-recipe-btn")),
            Is.Empty,
            "Expected all recipe rows to be removed");
    }

    [Then("'Frank' is still on the edit meal page")]
    public void ThenFrankIsStillOnEditMealPage()
    {
        // Confirmation should dismiss and URL should not have changed
        _wait.Until(d => !d.FindElements(By.CssSelector(".inline-confirm"))
            .Any(e => e.Displayed));
        Assert.That(_driver.Url, Does.Contain("EditMeal"));
    }

    [Then("'Frank' is redirected away from the edit meal page")]
    public void ThenFrankIsRedirectedAwayFromEditMealPage()
    {
        _wait.Until(d => !d.Url.Contains("EditMeal"));
        Assert.That(_driver.Url, Does.Not.Contain("EditMeal"));
    }

    [Then("the meal card is still visible on the home page")]
    public void ThenMealCardIsStillVisibleOnHomePage()
    {
        _wait.Until(d => !d.FindElements(By.CssSelector(".inline-confirm"))
            .Any(e => e.Displayed));
        Assert.That(
            _driver.FindElements(By.CssSelector(".list-group-item")).Count,
            Is.GreaterThan(0));
    }

    [Then("the meal card is no longer visible on the home page")]
    public void ThenMealCardIsNoLongerVisibleOnHomePage()
    {
        // form.submit() is async — wait for navigation to start, then complete
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(3))
                .Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() != "complete");
        }
        catch (WebDriverTimeoutException) { }

        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");

        // Other test meals for today may remain — assert only this specific meal is gone
        Assert.That(
            _driver.FindElements(By.CssSelector(".list-group-item"))
                .Any(item => item.Text.Contains("WVT165HomeMeal")),
            Is.False,
            "Expected WVT165HomeMeal to be removed from the planner home page");
    }
}
