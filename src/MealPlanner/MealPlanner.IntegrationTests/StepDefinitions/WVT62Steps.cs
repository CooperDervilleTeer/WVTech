using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT62Steps
{
    private IWebDriver _driver = null!;
    private WebDriverWait _wait = null!;
    private string _baseUrl = null!;
    private MealPlannerDBContext _context = null!;

    private string _mostPopularTag = string.Empty;
    private string _secondMostPopularTag = string.Empty;

    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _wait = BDDSetup.Wait;
        _baseUrl = AUTHost.BaseUrl;
        _context = BDDSetup.Context;

        using var ctx = BDDSetup.CreateContext();
        var gary = ctx.Set<User>().FirstOrDefault(u => u.NormalizedEmail == "GARY@FAKEEMAIL.COM");
        if (gary != null)
        {
            var prefs = ctx.Set<UserFoodPreference>().Where(p => p.UserId == gary.Id).ToList();
            ctx.Set<UserFoodPreference>().RemoveRange(prefs);
            ctx.SaveChanges();
        }
    }

    // The settings page defaults to the profile section; WVT62 scenarios all need the food
    // section. After any step that lands on /UserSettings without ?section=food, redirect there.
    [AfterStep]
    public void EnsureFoodSectionActive()
    {
        if (_driver == null) return;
        if (_driver.Url.Contains("/UserSettings") && !_driver.Url.Contains("section=food"))
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/UserSettings?section=food");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(5)).Until(d =>
                ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
        }
    }

    [Given("Onebite has at least 2 tags")]
    public void GivenOnebiteHasAtLeast2Tags()
    {
        var italian = _context.Set<Tag>().FirstOrDefault(t => t.Name == "Italian")
            ?? new Tag { Name = "Italian" };
        var cheap = _context.Set<Tag>().FirstOrDefault(t => t.Name == "Cheap")
            ?? new Tag { Name = "Cheap" };

        if (italian.Id == 0) _context.Add(italian);
        if (cheap.Id == 0) _context.Add(cheap);
        _context.SaveChanges();

        // Attach recipes so tags appear in the popularity-ordered dropdown
        if (!_context.Set<Recipe>().Any(r => r.Tags.Any(t => t.Name == "Italian")))
        {
            _context.Set<Recipe>().AddRange(
                new Recipe { Name = "Italian Test 1", Directions = "d", Calories = 100, Tags = [italian] },
                new Recipe { Name = "Italian Test 2", Directions = "d", Calories = 100, Tags = [italian] }
            );
            _context.SaveChanges();
        }

        if (!_context.Set<Recipe>().Any(r => r.Tags.Any(t => t.Name == "Cheap")))
        {
            _context.Set<Recipe>().Add(
                new Recipe { Name = "Cheap Test 1", Directions = "d", Calories = 100, Tags = [cheap] }
            );
            _context.SaveChanges();
        }
    }

    [Then("he sees the option to set food preference")]
    public void ThenHeSeesTheOptionToSetFoodPreference()
    {
        var el = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("food-pref-select")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(el, Is.Not.Null, "Could not find food preference dropdown (#food-pref-select)");
        Assert.That(el!.Displayed, Is.True);
    }

    [When("he selects the most popular tag from the food preference dropdown")]
    public void WhenHeSelectsMostPopularTag()
    {
        var select = new SelectElement(
            _wait.Until(d => d.FindElement(By.Id("food-pref-select"))));

        var firstOption = select.Options.FirstOrDefault(o => !string.IsNullOrEmpty(o.GetAttribute("value")));
        Assert.That(firstOption, Is.Not.Null, "No options in food preference dropdown");

        _mostPopularTag = firstOption!.GetAttribute("value");
        select.SelectByValue(_mostPopularTag);
    }

    [When("he selects the next most popular tag from the food preference dropdown")]
    public void WhenHeSelectsNextMostPopularTag()
    {
        var select = new SelectElement(
            _wait.Until(d => d.FindElement(By.Id("food-pref-select"))));

        var secondOption = select.Options
            .Where(o => !string.IsNullOrEmpty(o.GetAttribute("value")))
            .Skip(1)
            .FirstOrDefault();
        Assert.That(secondOption, Is.Not.Null, "Less than 2 options in food preference dropdown");

        _secondMostPopularTag = secondOption!.GetAttribute("value");
        select.SelectByValue(_secondMostPopularTag);
    }

    [When("he clicks save preference")]
    public void WhenHeClicksSavePreference()
    {
        var btn = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("food-pref-save-btn")); }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        _wait.Until(d => d.Url.TrimEnd('/') == $"{_baseUrl}/UserSettings");
    }

    [Then("he sees the tag in a list of his food preferences")]
    public void ThenHeSeesTagInPreferences()
    {
        var items = _driver.FindElements(By.CssSelector(".food-pref-item"));
        Assert.That(
            items.Any(el => el.GetAttribute("data-tag-name") == _mostPopularTag),
            Is.True,
            $"Tag '{_mostPopularTag}' not found in saved food preferences");
    }

    [When("he reloads the page")]
    public void WhenHeReloadsThePage()
    {
        _driver.Navigate().Refresh();
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Given("the tag {string} is not in the database")]
    public void GivenTagIsNotInDatabase(string tagName)
    {
        using var ctx = BDDSetup.CreateContext();
        var tag = ctx.Set<Tag>().FirstOrDefault(t => t.Name == tagName);
        if (tag != null)
        {
            ctx.Set<Tag>().Remove(tag);
            ctx.SaveChanges();
        }
    }

    [When("he types {string} into the custom food preference input")]
    public void WhenHeTypesIntoCustomInput(string tagName)
    {
        _mostPopularTag = tagName;

        var input = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("food-pref-custom-input")); }
            catch (NoSuchElementException) { return null; }
        });
        input!.Clear();
        input.SendKeys(tagName);
    }

    [When("he clicks add tag")]
    public void WhenHeClicksAddTag()
    {
        var btn = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("food-pref-add-btn")); }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);

        _wait.Until(d => d.FindElements(By.CssSelector("#food-pref-pending-container .food-pref-pending-pill")).Count > 0);
    }

    [Then("he sees both tags in his list of preferences")]
    public void ThenHeSeesBothTagsInPreferences()
    {
        var items = _driver.FindElements(By.CssSelector(".food-pref-item"));
        Assert.That(items.Count, Is.GreaterThanOrEqualTo(2),
            "Expected at least 2 food preferences to be visible");
    }

    [Given("{string} has the food preference {string}")]
    public void GivenUserHasFoodPreference(string userName, string tagName)
    {
        using var ctx = BDDSetup.CreateContext();
        var user = ctx.Set<User>()
            .First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var tag = ctx.Set<Tag>().FirstOrDefault(t => t.Name == tagName);
        if (tag == null)
        {
            tag = new Tag { Name = tagName };
            ctx.Add(tag);
            ctx.SaveChanges();
        }

        var alreadyExists = ctx.Set<UserFoodPreference>()
            .Any(p => p.UserId == user.Id && p.TagId == tag.Id);

        if (!alreadyExists)
        {
            ctx.Set<UserFoodPreference>().Add(new UserFoodPreference { UserId = user.Id, TagId = tag.Id });
            ctx.SaveChanges();
        }
    }

    [When("he clicks on the remove food preference button for {string}")]
    public void WhenHeClicksRemoveForTag(string tagName)
    {
        var btn = _wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.CssSelector($".food-pref-remove-btn[data-tag-name='{tagName}']"));
            }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", btn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        _wait.Until(d => { try { _ = btn.TagName; return false; } catch (StaleElementReferenceException) { return true; } catch (UnknownErrorException) { return true; } });
        _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("he has no food preferences")]
    public void ThenHeHasNoFoodPreferences()
    {
        var items = _driver.FindElements(By.CssSelector(".food-pref-item"));
        Assert.That(items.Count, Is.EqualTo(0), "Expected no saved food preferences");
    }

    [When("he selects a tag that is not {string}")]
    public void WhenHeSelectsATagThatIsNot(string excludedTagName)
    {
        var select = new SelectElement(
            _wait.Until(d => d.FindElement(By.Id("food-pref-select"))));

        var option = select.Options
            .FirstOrDefault(o => !string.IsNullOrEmpty(o.GetAttribute("value"))
                              && o.GetAttribute("value") != excludedTagName);
        Assert.That(option, Is.Not.Null, $"No option available that is not '{excludedTagName}'");

        _mostPopularTag = option!.GetAttribute("value");
        select.SelectByValue(_mostPopularTag);
    }

    [Then("he sees only the tag {string} in his list of preferences")]
    public void ThenHeSeesOnlyTag(string tagName)
    {
        var items = _driver.FindElements(By.CssSelector(".food-pref-item"));
        Assert.That(items.Count, Is.EqualTo(1), $"Expected exactly 1 food preference, found {items.Count}");
        Assert.That(items[0].GetAttribute("data-tag-name"), Is.EqualTo(tagName),
            $"Expected tag '{tagName}' but found '{items[0].GetAttribute("data-tag-name")}'");
    }
}
