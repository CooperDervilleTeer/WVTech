using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT158Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly WebDriverWait _wait;

    private const string IngredientName = "WVT158Ingredient";
    private const float AutoAmount = 2f;
    private const float ManualAmount = 1f;

    public WVT158Steps()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
    }

    private string GetAliceId(MealPlannerDBContext ctx) =>
        ctx.Users.First(u => u.Email == "Alice@fakeemail.com").Id;

    private void NavigateToShoppingList()
    {
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListSynced");
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListDateFrom");
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListDateTo");
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping");
        _wait.Until(d => d.Url.Contains("/Shopping"));
    }

    private void SetDateRange(string dateFrom, string dateTo)
    {
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            $"document.querySelector('input[name=dateFrom]').value = '{dateFrom}';" +
            $"document.querySelector('input[name=dateTo]').value = '{dateTo}';" +
            "document.getElementById('dateRangeForm').submit();"
        );
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Given("'Alice' has a meal today with a WVT158 auto ingredient")]
    public void GivenAliceHasAMealTodayWithAWVT158AutoIngredient()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);

        var staleItems = ctx.Set<ShoppingListItem>()
            .Where(i => i.UserId == userId && i.IngredientBase.Name == IngredientName).ToList();
        ctx.Set<ShoppingListItem>().RemoveRange(staleItems);
        var staleMeals = ctx.Meals
            .Where(m => m.UserId == userId && m.Title == "WVT158 Meal").ToList();
        ctx.Meals.RemoveRange(staleMeals);
        ctx.SaveChanges();

        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == IngredientName);
        if (ingredientBase == null)
        {
            ingredientBase = new IngredientBase { Name = IngredientName };
            ctx.Set<IngredientBase>().Add(ingredientBase);
            ctx.SaveChanges();
        }

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count");
        if (measurement == null)
        {
            measurement = new Measurement { Name = "Count", Abbreviation = "Count" };
            ctx.Set<Measurement>().Add(measurement);
            ctx.SaveChanges();
        }

        var recipe = new Recipe
        {
            Name = "WVT158Recipe",
            Directions = "Test",
            Calories = 100, Protein = 5, Carbs = 10, Fat = 3,
            Ingredients = new List<Ingredient>
            {
                new Ingredient { DisplayName = IngredientName, IngredientBase = ingredientBase, Measurement = measurement, Amount = AutoAmount }
            }
        };
        ctx.Recipes.Add(recipe);

        var meal = new Meal
        {
            UserId = userId,
            Title = "WVT158 Meal",
            StartTime = DateTime.Today.AddHours(12)
        };
        meal.Recipes.Add(recipe);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
    }

    [Given("'Alice' views the shopping list so the auto ingredient is synced")]
    public void GivenAliceViewsTheShoppingListSoTheAutoIngredientIsSynced()
    {
        NavigateToShoppingList();
        _wait.Until(d => d.PageSource.Contains(IngredientName));
    }

    [Given("'Alice' manually adds the WVT158 ingredient to her shopping list")]
    public void GivenAliceManuallyAddsTheWVT158Ingredient()
    {
        _wait.Until(d => d.FindElement(By.CssSelector("input[name='amount']"))).SendKeys(ManualAmount.ToString());
        _driver.FindElement(By.CssSelector("select[name='measurement']")).FindElement(By.XPath(".//option[@value='Count']")).Click();
        _driver.FindElement(By.CssSelector("input[name='itemName']")).SendKeys(IngredientName);
        _driver.FindElement(By.CssSelector("button[type='submit'].sl-add-btn")).Click();

        // Wait for the POST navigation to start, then fully complete back on the shopping list
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(3))
                .Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() != "complete");
        }
        catch (WebDriverTimeoutException) { }
        _wait.Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete"
            && d.Url.Contains("/Shopping"));
    }

    [When("'Alice' changes the shopping list to a date range that excludes today")]
    public void WhenAliceChangesDateRangeToExcludeToday()
    {
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        SetDateRange(tomorrow, tomorrow);
    }

    [When("'Alice' changes the shopping list date range back to today")]
    public void WhenAliceChangesDateRangeBackToToday()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        SetDateRange(today, today);
    }

    [Then("the WVT158 ingredient amount has not been doubled on the shopping list")]
    public void ThenTheWVT158IngredientAmountHasNotBeenDoubled()
    {
        _wait.Until(d => d.Url.Contains("/Shopping"));
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);
        var items = ctx.Set<ShoppingListItem>()
            .Where(i => i.UserId == userId && i.IngredientBase.Name == IngredientName)
            .ToList();

        var manualItem = items.FirstOrDefault(i => !i.IsAutoAdded);
        Assert.That(manualItem, Is.Not.Null, "Manual item should still exist");
        Assert.That(manualItem!.Amount, Is.EqualTo(ManualAmount), "Manual item amount should not have been accumulated into");
    }

    [Then("the manually added WVT158 ingredient is still on the shopping list")]
    public void ThenTheManuallyAddedWVT158IngredientIsStillOnTheShoppingList()
    {
        _wait.Until(d => d.Url.Contains("/Shopping"));
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);
        var manualItem = ctx.Set<ShoppingListItem>()
            .FirstOrDefault(i => i.UserId == userId && i.IngredientBase.Name == IngredientName && !i.IsAutoAdded);

        Assert.That(manualItem, Is.Not.Null, "Manually added item should not have been deleted when date range changed");
    }
}
