using System.Globalization;
using System.Text.RegularExpressions;
using MealPlanner.Models;
using MealPlanner.Services;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT177Steps
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private readonly string _baseUrl;

    public WVT177Steps()
    {
        _driver = BDDSetup.Driver;
        _wait = BDDSetup.Wait;
        _baseUrl = AUTHost.BaseUrl;
    }

    [When("{string} adds an ingredient row")]
    public void WhenUserAddsAnIngredientRow(string username)
    {
        _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("ingUnit")).Displayed; }
            catch (NoSuchElementException) { return false; }
        });
    }

    [Then("the ingredient measurement dropdown contains {string}")]
    public void ThenIngredientMeasurementDropdownContains(string measurement)
    {
        var select = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("ingUnit")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(select, Is.Not.Null, "Ingredient measurement dropdown (#ingUnit) not found");
        var options = select!.FindElements(By.TagName("option"));
        Assert.That(
            options.Any(o => (o.GetAttribute("value") ?? "").Equals(measurement, StringComparison.OrdinalIgnoreCase)),
            Is.True,
            $"'{measurement}' not found. Available: {string.Join(", ", options.Select(o => o.GetAttribute("value")))}");
    }

    [When("{string} adds an ingredient with name {string} amount {string} and measurement {string}")]
    public void WhenUserAddsIngredientWithAmountAndMeasurement(string username, string name, string amount, string measurement)
    {
        var ingQty = _driver.FindElement(By.Id("ingQty"));
        ingQty.Clear();
        ingQty.SendKeys(amount);

        new SelectElement(_driver.FindElement(By.Id("ingUnit"))).SelectByValue(measurement);

        var ingName = _driver.FindElement(By.Id("ingName"));
        ingName.Clear();
        ingName.SendKeys(name);

        int rowsBefore = _driver.FindElements(By.CssSelector("#ingredientList .ar-ing-row")).Count;
        _driver.FindElement(By.Id("buttonAppend")).Click();
        _wait.Until(d => d.FindElements(By.CssSelector("#ingredientList .ar-ing-row")).Count > rowsBefore);
    }

    [When("{string} submits the recipe form")]
    public void WhenSubmitsTheRecipeForm(string username)
    {
        var submitBtn = _driver.FindElement(By.CssSelector(".ar-submit-btn"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", submitBtn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", submitBtn);
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("the recipe {string} stores {string} with amount {string}")]
    public void ThenRecipeStoresIngredientWithAmount(string recipeName, string ingredientName, string expectedAmountStr)
    {
        float expectedAmount = float.Parse(expectedAmountStr, CultureInfo.InvariantCulture);
        using var ctx = BDDSetup.CreateContext();
        var recipe = ctx.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefault(r => r.Name == recipeName);
        Assert.That(recipe, Is.Not.Null, $"Recipe '{recipeName}' not found in database");

        var normalizedName = IngredientNameNormalizer.NormalizeKey(ingredientName);
        var ingredient = recipe!.Ingredients.FirstOrDefault(i =>
            i.IngredientBase.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
        Assert.That(ingredient, Is.Not.Null, $"Ingredient '{ingredientName}' not found in recipe '{recipeName}'");
        Assert.That(ingredient!.Amount, Is.EqualTo(expectedAmount).Within(0.001f));
    }

    [Given("{string} navigates to the shopping list")]
    [When("{string} navigates to the shopping list")]
    public void WhenUserNavigatesToShoppingList(string username)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("the shopping list measurement dropdown contains {string}")]
    public void ThenShoppingListMeasurementDropdownContains(string measurement)
    {
        var select = _wait.Until(d =>
        {
            try { return d.FindElement(By.CssSelector("select[name='measurement']")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(select, Is.Not.Null, "Shopping list measurement dropdown not found");
        var options = select!.FindElements(By.TagName("option"));
        Assert.That(
            options.Any(o => (o.GetAttribute("value") ?? "").Equals(measurement, StringComparison.OrdinalIgnoreCase)),
            Is.True,
            $"'{measurement}' not found in shopping list dropdown. Available: {string.Join(", ", options.Select(o => o.GetAttribute("value")))}");
    }

    [Given("{string} has {string} with amount {string} and measurement {string} on the shopping list")]
    public void GivenUserHasItemOnShoppingList(string userName, string ingredientName, string amountStr, string measurement)
    {
        float amount = float.Parse(amountStr, CultureInfo.InvariantCulture);
        using var ctx = BDDSetup.CreateContext();

        var user = ctx.Set<User>().FirstOrDefault(u => u.Email == $"{userName}@fakeemail.com");
        Assert.That(user, Is.Not.Null, $"User '{userName}' not found");

        var normalizedName = IngredientNameNormalizer.NormalizeKey(ingredientName);
        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(b => b.Name == normalizedName)
            ?? ctx.Set<IngredientBase>().Add(new IngredientBase { Name = normalizedName }).Entity;

        var measurementEntity = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == measurement)
            ?? ctx.Set<Measurement>().Add(new Measurement { Name = measurement, Abbreviation = measurement }).Entity;

        ctx.SaveChanges();

        var existing = ctx.ShoppingListItems
            .Where(i => i.UserId == user!.Id && i.IngredientBaseId == ingredientBase.Id)
            .ToList();
        ctx.ShoppingListItems.RemoveRange(existing);
        ctx.SaveChanges();

        ctx.ShoppingListItems.Add(new ShoppingListItem
        {
            UserId = user!.Id,
            IngredientBase = ingredientBase,
            Measurement = measurementEntity,
            Amount = amount,
            IsAutoAdded = false
        });
        ctx.SaveChanges();
    }

    [Then("{string} displays with amount {string} on the shopping list")]
    public void ThenIngredientDisplaysWithAmountOnShoppingList(string ingredientName, string expectedAmount)
    {
        var matchingItem = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".item-display"))
                    .FirstOrDefault(i => i.Text.Contains(ingredientName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });
        Assert.That(matchingItem, Is.Not.Null, $"Item '{ingredientName}' not found on shopping list");
        var qtyInput = matchingItem!.FindElement(By.XPath("../preceding-sibling::div[1]//input[contains(@class,'qty-input')]"));
        Assert.That(qtyInput.GetAttribute("value"), Does.Contain(expectedAmount),
            $"Expected amount '{expectedAmount}' not found in qty input for '{ingredientName}'");
    }

    [Then("an invalid amount error is displayed")]
    public void ThenAnInvalidAmountErrorIsDisplayed()
    {
        _wait.Until(d =>
        {
            try { return d.FindElement(By.CssSelector(".validation-summary-errors")).Displayed; }
            catch (NoSuchElementException) { return false; }
            catch (StaleElementReferenceException) { return false; }
        });
    }

    [When("{string} updates the measurement of {string} to {string}")]
    public void WhenUserUpdatesMeasurementOf(string username, string ingredientName, string newMeasurement)
    {
        var span = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".item-display[data-name]"))
                    .FirstOrDefault(s => s.GetAttribute("data-name")
                        .Contains(ingredientName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });
        Assert.That(span, Is.Not.Null, $"Shopping list item '{ingredientName}' not found");

        var qtyDiv = span!.FindElement(By.XPath("../preceding-sibling::div[1]"));
        var input = qtyDiv.FindElement(By.CssSelector(".qty-input"));

        var currentValue = input.GetAttribute("value") ?? "";
        var numMatch = Regex.Match(currentValue, @"^(\d+(?:\.\d+)?(?:\s+\d+/\d+|/\d+)?)\s*");
        var currentNumberStr = numMatch.Success ? numMatch.Groups[1].Value.Trim() : "0";
        var newCombinedValue = $"{currentNumberStr} {newMeasurement}";

        var currentOriginalMeasurement = input.GetAttribute("data-original-measurement");
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].focus(); arguments[0].value = arguments[1]; arguments[0].blur();",
            input, newCombinedValue);

        if (!string.Equals(currentOriginalMeasurement, newMeasurement, StringComparison.OrdinalIgnoreCase))
        {
            _wait.Until(d =>
            {
                try { return string.Equals(input.GetAttribute("data-original-measurement"), newMeasurement, StringComparison.OrdinalIgnoreCase); }
                catch (StaleElementReferenceException) { return false; }
            });
        }
    }

    [Then("the measurement of {string} on the shopping list shows {string}")]
    public void ThenMeasurementOfItemOnShoppingListShows(string ingredientName, string expectedMeasurement)
    {
        var span = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".item-display[data-name]"))
                    .FirstOrDefault(s => s.GetAttribute("data-name")
                        .Contains(ingredientName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });
        Assert.That(span, Is.Not.Null, $"Shopping list item '{ingredientName}' not found");

        var qtyDiv = span!.FindElement(By.XPath("../preceding-sibling::div[1]"));
        var input = qtyDiv.FindElement(By.CssSelector(".qty-input"));
        Assert.That(input.GetAttribute("data-original-measurement"), Is.EqualTo(expectedMeasurement).IgnoreCase);
    }
}
