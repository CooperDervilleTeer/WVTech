using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using NUnit.Framework;

namespace Mealplanner.IntegrationTests;

[Binding]
public class SelectMealSteps
{
    IWebDriver _driver;
    string _baseUrl;
    readonly string _emailBase = "@fakeemail.com";

    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
    }

    [Given("{string} has a previously created meal named {string}")]
    public void GivenUserHasAPreviouslyCreatedMeal(string username, string mealTitle)
    {
        using var ctx = BDDSetup.CreateContext();
        var user = ctx.Users.First(u => u.NormalizedEmail == $"{username}{_emailBase}".ToUpper());

        var existing = ctx.Meals
            .Where(m => m.UserId == user.Id && m.Title == mealTitle)
            .ToList();
        if (existing.Count > 0)
        {
            ctx.RemoveRange(existing);
            ctx.SaveChanges();
        }

        var meal = new Meal
        {
            UserId = user.Id,
            Title = mealTitle,
            StartTime = DateTime.Today.AddDays(-30)
        };
        ctx.Add(meal);
        ctx.SaveChanges();
    }

    [Given("{string} has no previously created meals")]
    public void GivenUserHasNoMeals(string username)
    {
        using var ctx = BDDSetup.CreateContext();
        var user = ctx.Users.First(u => u.NormalizedEmail == $"{username}{_emailBase}".ToUpper());
        var mine = ctx.Meals.Where(m => m.UserId == user.Id).ToList();
        if (mine.Count > 0)
        {
            ctx.RemoveRange(mine);
            ctx.SaveChanges();
        }
    }

    [Given("{string} has a meal named {string} scheduled on {int} different past dates")]
    public void GivenUserHasMealOnMultiplePastDates(string username, string mealTitle, int count)
    {
        using var ctx = BDDSetup.CreateContext();
        var user = ctx.Users.First(u => u.NormalizedEmail == $"{username}{_emailBase}".ToUpper());

        var existing = ctx.Meals
            .Where(m => m.UserId == user.Id && m.Title == mealTitle)
            .ToList();
        if (existing.Count > 0)
        {
            ctx.RemoveRange(existing);
            ctx.SaveChanges();
        }

        for (int i = 1; i <= count; i++)
        {
            ctx.Add(new Meal
            {
                UserId = user.Id,
                Title = mealTitle,
                StartTime = DateTime.Today.AddDays(-i * 7)
            });
        }
        ctx.SaveChanges();
    }

    [Then("{string} sees the empty meal list message")]
    public void ThenUserSeesEmptyMessage(string username)
    {
        var container = _driver.FindElement(By.Id("selectMealList"));
        Assert.That(container.Text, Does.Contain("not created any meals"));
        var rows = _driver.FindElements(By.CssSelector(".selectMealRow"));
        Assert.That(rows.Count, Is.EqualTo(0));
    }

    [Then("{string} does not see a meal named {string} in the select meal list")]
    public void ThenUserDoesNotSeeMeal(string username, string mealTitle)
    {
        var names = _driver.FindElements(By.CssSelector(".selectMealName"))
            .Select(e => e.Text.Trim())
            .ToList();
        Assert.That(names, Does.Not.Contain(mealTitle));
    }

    [Then("{string} sees exactly {int} meal named {string} in the select meal list")]
    public void ThenUserSeesExactCountNamed(string username, int expectedCount, string mealTitle)
    {
        var matches = _driver.FindElements(By.CssSelector(".selectMealName"))
            .Count(e => e.Text.Trim() == mealTitle);
        Assert.That(matches, Is.EqualTo(expectedCount));
    }

    [Given("{string} is on the select meal page")]
    public void GivenUserIsOnSelectMealPage(string username)
    {
        string expectedPath = "/Meal/SelectMeal";
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}{expectedPath}");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState").ToString() == "complete");

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

            new WebDriverWait(_driver, TimeSpan.FromSeconds(5)).Until(d =>
            {
                try { return d.FindElement(By.Id("selectMealList")).Displayed; }
                catch (NoSuchElementException) { return false; }
            });
            return;
        }
    }

    [Then("{string} sees a meal named {string} in the select meal list")]
    public void ThenUserSeesMealInList(string username, string mealTitle)
    {
        var names = _driver.FindElements(By.CssSelector(".selectMealName"))
            .Select(e => e.Text.Trim())
            .ToList();
        Assert.That(names, Does.Contain(mealTitle));
    }

    [When("{string} clicks the remove button for the meal named {string}")]
    public void WhenUserClicksRemoveButtonForMealNamed(string username, string mealTitle)
    {
        var slot = _driver.FindElements(By.CssSelector(".sm-card-slot"))
            .First(s => s.FindElements(By.CssSelector(".selectMealName"))
                .Any(e => e.Text.Trim() == mealTitle));

        var removeBtn = slot.FindElement(By.CssSelector(".sm-delete-btn"));
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block:'center',behavior:'instant'});", removeBtn);
        try
        {
            removeBtn.Click();
        }
        catch (ElementClickInterceptedException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", removeBtn);
        }

        // Wait for navigation to start, then complete (readyState check alone fires on the current page)
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(3)).Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState").ToString() != "complete");
        }
        catch (WebDriverTimeoutException) { }

        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [When("{string} clicks the meal named {string}")]
    public void WhenUserClicksMealNamed(string username, string mealTitle)
    {
        var row = _driver.FindElements(By.CssSelector(".selectMealRow"))
            .First(r => r.FindElement(By.CssSelector(".selectMealName")).Text.Trim() == mealTitle);

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block:'center',behavior:'instant'});", row);
        try
        {
            row.Click();
        }
        catch (ElementClickInterceptedException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", row);
        }

        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("{string} is redirected to the home page")]
    public void ThenUserIsRedirectedToHomePage(string username)
    {
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            !d.Url.Contains("/Meal/SelectMeal", StringComparison.OrdinalIgnoreCase));
        var uri = new Uri(_driver.Url);
        Assert.That(uri.AbsolutePath.TrimEnd('/'), Is.EqualTo("").Or.EqualTo("/Home/Index").IgnoreCase);
    }

    [Then("{string} has a meal named {string} scheduled for today")]
    public void ThenUserHasMealScheduledForToday(string username, string mealTitle)
    {
        using var ctx = BDDSetup.CreateContext();
        var user = ctx.Users.First(u => u.NormalizedEmail == $"{username}{_emailBase}".ToUpper());

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var match = ctx.Meals.Any(m =>
            m.UserId == user.Id &&
            m.Title == mealTitle &&
            m.StartTime != null &&
            m.StartTime >= today &&
            m.StartTime < tomorrow);

        Assert.That(match, Is.True, $"No meal '{mealTitle}' scheduled for today for user {username}");
    }
}
