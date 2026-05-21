using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT72Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly ScenarioContext _scenarioContext;

    public WVT72Steps(ScenarioContext scenarioContext)
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _scenarioContext = scenarioContext;
    }

    private void EnsureOnEditMealPage()
    {
        if (_driver.Url.Contains("/EditMeal", StringComparison.OrdinalIgnoreCase)) return;
        var mealId = (int)_scenarioContext["MealId"];
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/EditMeal?id={mealId}");
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [When("'Jack' selects meal day {string}")]
    public void WhenJackSelectsMealDate(string day)
    {
        var dayElement = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d => d.FindElement(By.Name("SelectedDay")));
        var daySelect = new SelectElement(dayElement);
        daySelect.SelectByText(day);
    }

    [When("'Jack' enables weekly repeat")]
    public void WhenJackEnablesWeeklyRepeat()
    {
        var repeatCheckbox = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d => d.FindElement(By.Name("RepeatWeekly")));

        if (!repeatCheckbox.Selected)
            repeatCheckbox.Click();
    }

    [Then("the meal repeat rule is saved as weekly")]
    public void ThenTheMealRepeatRuleIsSavedAsWeekly()
    {
        EnsureOnEditMealPage();
        bool? isSelected = null;
        new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try
                {
                    isSelected = d.FindElement(By.Name("RepeatWeekly")).Selected;
                    return true;
                }
                catch (StaleElementReferenceException) { return false; }
                catch (NoSuchElementException) { return false; }
            });
        Assert.That(isSelected, Is.True);
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("'Jack' selects the meal day {string}")]
    public void WhenSelectsTheMealDay(string month)
    {
        var monthElement = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d => d.FindElement(By.Name("SelectedMonth")));
        var monthSelect = new SelectElement(monthElement);
        monthSelect.SelectByText(month);
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("the meal month field is saved as {string}")]
    public void ThenTheMealMonthFieldIsSavedAs(string expectedMonth)
    {
        EnsureOnEditMealPage();
        string? selectedText = null;
        new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try
                {
                    selectedText = new SelectElement(d.FindElement(By.Name("SelectedMonth"))).SelectedOption.Text;
                    return true;
                }
                catch (StaleElementReferenceException) { return false; }
                catch (NoSuchElementException) { return false; }
            });
        Assert.That(selectedText, Does.Contain(expectedMonth));
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("the meal day field is saved as {string}")]
    public void ThenTheMealDayFieldIsSavedAs(string expectedDay)
    {
        EnsureOnEditMealPage();
        string? selectedText = null;
        new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try
                {
                    selectedText = new SelectElement(d.FindElement(By.Name("SelectedDay"))).SelectedOption.Text;
                    return true;
                }
                catch (StaleElementReferenceException) { return false; }
                catch (NoSuchElementException) { return false; }
            });
        Assert.That(selectedText, Does.Contain(expectedDay));
    }
}