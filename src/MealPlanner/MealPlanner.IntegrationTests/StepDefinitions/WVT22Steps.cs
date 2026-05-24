using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using System.Threading;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT22Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly WebDriverWait _wait;

    public WVT22Steps()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
    }

    [Given("'Dave' is on the shopping list page")]
    public void GivenDaveIsOnShoppingListPage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState")?.ToString() == "complete");
    }

    [Then("a zip code input field is visible")]
    public void ThenAZipCodeInputFieldIsVisible()
    {
        var input = _wait.Until(d =>
        {
            var el = d.FindElements(By.Id("ZipCode")).FirstOrDefault();
            return el != null && el.Displayed ? el : null;
        });
        Assert.That(input, Is.Not.Null, "Expected a visible zip code input with id='ZipCode'");
    }

    [When("'Dave' enters zip code {string} and clicks export to Kroger")]
    public void WhenDaveEntersZipCodeAndClicksExport(string zipCode)
    {
        var input = _wait.Until(d => d.FindElement(By.Id("ZipCode")));
        input.Clear();
        input.SendKeys(zipCode);

        var findBtn = _wait.Until(d => d.FindElement(By.Id("findKrogerStores")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", findBtn);
        findBtn.Click();

        _wait.Until(d =>
        {
            var section = d.FindElements(By.Id("krogerStoreSection")).FirstOrDefault();
            return section != null && section.Displayed;
        });

        Thread.Sleep(500);

        var exportBtn = _driver.FindElements(By.Id("exportToKroger"))
            .FirstOrDefault(e => e.Displayed);

        if (exportBtn != null)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", exportBtn);
            exportBtn.Click();

            _wait.Until(d => ((IJavaScriptExecutor)d)
                .ExecuteScript("return document.readyState")?.ToString() == "complete");
        }

        if (!_driver.Url.Contains("Shopping"))
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping");
            _wait.Until(d => ((IJavaScriptExecutor)d)
                .ExecuteScript("return document.readyState")?.ToString() == "complete");
        }
    }

    [Then("the zip code {string} is shown in the export section")]
    public void ThenTheZipCodeIsShownInExportSection(string zipCode)
    {
        var input = _wait.Until(d => d.FindElement(By.Id("ZipCode")));
        Assert.That(input.GetAttribute("value"), Is.EqualTo(zipCode),
            $"Expected zip code input to show '{zipCode}'");
    }

    [When("'Dave' navigates away and returns to the shopping list page")]
    public void WhenDaveNavigatesAwayAndReturnsToShoppingList()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/PlannerHome");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState")?.ToString() == "complete");

        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState")?.ToString() == "complete");
    }

    [Given("'Dave' has no previous Kroger exports")]
    public static void GivenDaveHasNoPreviousKrogerExports()
    {
        var user = SharedSteps.Users["Dave"];
        using var ctx = BDDSetup.CreateContext();
        var exports = ctx.KrogerExports.Where(e => e.UserId == user.Id).ToList();
        if (exports.Count > 0)
        {
            ctx.KrogerExports.RemoveRange(exports);
            ctx.SaveChanges();
        }
    }

    [Given("'Dave' has items on their shopping list")]
    public static void GivenDaveHasItemsOnShoppingList()
    {
        var user = SharedSteps.Users["Dave"];
        using var ctx = BDDSetup.CreateContext();
        if (!ctx.ShoppingListItems.Any(i => i.UserId == user.Id))
        {
            var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(b => b.Name == "chicken broth")
                ?? ctx.Set<IngredientBase>().Add(new IngredientBase { Name = "chicken broth" }).Entity;
            var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Cup(s)")
                ?? ctx.Set<Measurement>().Add(new Measurement { Name = "Cup(s)", Abbreviation = "Cup(s)" }).Entity;
            ctx.SaveChanges();
            ctx.ShoppingListItems.Add(new ShoppingListItem
            {
                UserId = user.Id,
                IngredientBase = ingredientBase,
                Amount = 2,
                Measurement = measurement
            });
            ctx.SaveChanges();
        }
    }

    [Then("the 'Previous Exports' button is visible")]
    public void ThenThePreviousExportsButtonIsVisible()
    {
        var btn = _wait.Until(d =>
        {
            var el = d.FindElements(By.Id("previousExportsBtn")).FirstOrDefault();
            return el != null && el.Displayed ? el : null;
        });
        Assert.That(btn, Is.Not.Null, "Expected a visible 'Previous Exports' button");
    }

    [When("'Dave' clicks the 'Previous Exports' button")]
    public void WhenDaveClicksPreviousExportsButton()
    {
        var btn = _wait.Until(d => d.FindElement(By.Id("previousExportsBtn")));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
        btn.Click();

        _wait.Until(d =>
        {
            var modal = d.FindElements(By.Id("exportHistoryModal")).FirstOrDefault();
            return modal != null && modal.Displayed;
        });
    }

    [Then("the export history modal is displayed")]
    public void ThenTheExportHistoryModalIsDisplayed()
    {
        var modal = _wait.Until(d =>
        {
            var el = d.FindElements(By.Id("exportHistoryModal")).FirstOrDefault();
            return el != null && el.Displayed ? el : null;
        });
        Assert.That(modal, Is.Not.Null, "Expected the export history modal to be visible");
    }

    [Then("the export history modal shows 'No previous exports'")]
    public void ThenTheExportHistoryModalShowsNoPreviousExports()
    {
        var body = _wait.Until(d =>
        {
            var el = d.FindElement(By.Id("exportHistoryBody"));
            return el.Text != "Loading..." ? el : null;
        });
        Assert.That(body.Text, Does.Contain("No previous exports"),
            "Expected empty state message in export history modal");
    }

    [Given("'Dave' has a previous Kroger export with {int} items")]
    public static void GivenDaveHasAPreviousKrogerExportWithItems(int itemCount)
    {
        var user = SharedSteps.Users["Dave"];
        using var ctx = BDDSetup.CreateContext();
        var export = new KrogerExport
        {
            UserId = user.Id,
            ExportedAt = DateTime.UtcNow.AddMinutes(-10),
            Items = [.. Enumerable.Range(1, itemCount).Select(i => new KrogerExportItem
            {
                Name = $"Item {i}",
                Amount = 1,
                Measurement = "Count",
                Upc = $"000000000{i:D4}",
                Quantity = 1
            })]
        };
        ctx.KrogerExports.Add(export);
        ctx.SaveChanges();
    }

    [Then("the export history modal shows an entry with {string}")]
    public void ThenTheExportHistoryModalShowsAnEntryWith(string text)
    {
        var body = _wait.Until(d =>
        {
            var el = d.FindElement(By.Id("exportHistoryBody"));
            return el.Text != "Loading..." ? el : null;
        });
        Assert.That(body.Text, Does.Contain(text),
            $"Expected export history entry containing '{text}'");
    }

    [When("'Dave' clicks on the first export entry")]
    public void WhenDaveClicksOnTheFirstExportEntry()
    {
        var firstEntry = _wait.Until(d =>
            d.FindElements(By.CssSelector(".export-history-entry")).FirstOrDefault());
        Assert.That(firstEntry, Is.Not.Null, "Expected at least one export history entry");
        firstEntry.Click();

        _wait.Until(d =>
            d.FindElements(By.CssSelector(".export-item-list")).Any(el => el.Displayed));
    }

    [Then("the items from that export are shown")]
    public void ThenTheItemsFromThatExportAreShown()
    {
        var itemList = _wait.Until(d =>
            d.FindElements(By.CssSelector(".export-item-list")).FirstOrDefault(el => el.Displayed));
        Assert.That(itemList, Is.Not.Null, "Expected export items to be visible after clicking entry");
        Assert.That(itemList.Text, Has.Length.GreaterThan(0), "Expected item list to contain text");
    }

    [Given("'Dave' has an empty shopping list")]
    public static void GivenDaveHasAnEmptyShoppingList()
    {
        var user = SharedSteps.Users["Dave"];
        using var ctx = BDDSetup.CreateContext();
        var items = ctx.ShoppingListItems.Where(i => i.UserId == user.Id).ToList();
        if (items.Count > 0)
        {
            ctx.ShoppingListItems.RemoveRange(items);
            ctx.SaveChanges();
        }
    }

    [When("'Dave' clicks the 'Add to Shopping List' button")]
    public void WhenDaveClicksAddToShoppingList()
    {
        var btn = _wait.Until(d =>
        {
            var el = d.FindElements(By.CssSelector(".add-to-list-btn")).FirstOrDefault(e => e.Displayed);
            return el != null && el.Enabled ? el : null;
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", btn);
        btn.Click();

        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState")?.ToString() == "complete");
    }

    [Then("the shopping list shows the items from the export")]
    public void ThenTheShoppingListShowsTheItemsFromExport()
    {
        var items = _wait.Until(d =>
        {
            var els = d.FindElements(By.CssSelector(".item-display"));
            return els.Count > 0 ? els : null;
        });
        Assert.That(items, Is.Not.Null, "Expected shopping list items to appear after adding from export history");
    }
}
