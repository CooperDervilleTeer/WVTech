using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using NUnit.Framework;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT173Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly WebDriverWait _wait;

    public WVT173Steps()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
    }

    [Given("'Gary' navigates to the user settings page")]
    [When("'Gary' navigates to the user settings page")]
    public void GivenGaryNavigatesToUserSettingsPage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/UserSettings?section=food");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [When("'Gary' types {string} into the custom food preference input and presses Enter")]
    public void WhenGaryTypesIntoCustomInputAndPressesEnter(string value)
    {
        var input = _wait.Until(d => d.FindElement(By.Id("food-pref-custom-input")));
        input.Clear();
        input.SendKeys(value);
        input.SendKeys(Keys.Enter);
    }

    [Then("'Gary' sees {string} in the pending food preferences list")]
    public void ThenGarySeesInPendingList(string value)
    {
        var pill = _wait.Until(d =>
            d.FindElements(By.CssSelector("#food-pref-pending-container .food-pref-pending-pill"))
             .FirstOrDefault(e => e.Text.Trim() == value));
        Assert.That(pill, Is.Not.Null, $"Expected pending pill '{value}' not found");
    }

    [When("'Gary' presses Enter in an empty custom food preference input")]
    public void WhenGaryPressesEnterInEmptyCustomInput()
    {
        // Mark the page so we can detect a reload in the Then step
        ((IJavaScriptExecutor)_driver).ExecuteScript("window.__wvt173_noreload = true;");

        var input = _wait.Until(d => d.FindElement(By.Id("food-pref-custom-input")));
        input.Clear();
        input.SendKeys(Keys.Enter);
    }

    [Then("no new item appears in the pending food preferences list")]
    public void ThenNoNewItemInPendingList()
    {
        // If Enter submitted the form the page would reload, losing the JS marker
        var noReload = (bool?)((IJavaScriptExecutor)_driver)
            .ExecuteScript("return window.__wvt173_noreload === true;");
        Assert.That(noReload, Is.True, "Page appears to have reloaded — Enter should not submit the form");

        var pills = _driver.FindElements(By.CssSelector("#food-pref-pending-container .food-pref-pending-pill"));
        Assert.That(pills, Is.Empty, "No pending preference pills should be present");
    }

    [When("'Gary' clicks the Save Preferences button")]
    public void WhenGaryClicksSavePreferencesButton()
    {
        var btn = _wait.Until(d => d.FindElement(By.Id("food-pref-save-btn")));
        btn.Click();
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("'Gary' sees {string} in the saved food preferences list")]
    public void ThenGarySeesInSavedList(string value)
    {
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
        var found = (bool)((IJavaScriptExecutor)_driver).ExecuteScript(
            $"return Array.from(document.querySelectorAll('.food-pref-item')).some(e => e.textContent.trim() === arguments[0]);",
            value);
        Assert.That(found, Is.True, $"Expected saved preference '{value}' not found");
    }
}
