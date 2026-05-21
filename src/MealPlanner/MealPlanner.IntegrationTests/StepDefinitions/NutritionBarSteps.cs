using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class NutritionBarSteps
{
    IWebDriver _driver;
    string _baseUrl;
    DateTime? _mealDate;
    readonly Dictionary<(string user, string recipe), int> _recipeIds = [];

    [Given("{string} has no meals")]
    public void GivenUserHasNoMeals(string username)
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = SharedSteps.Users[username].Id;

        var mealIds = ctx.Set<Meal>().Where(m => m.UserId == userId).Select(m => m.Id).ToList();
        if (mealIds.Count == 0) return;

        ctx.Set<MealCompletion>().RemoveRange(
            ctx.Set<MealCompletion>().Where(mc => mealIds.Contains(mc.MealId)));
        ctx.Set<Meal>().RemoveRange(
            ctx.Set<Meal>().Where(m => mealIds.Contains(m.Id)));
        ctx.SaveChanges();
    }

    [Given("{string} has a recipe {string} with {int} calories, {int} protein, {int} fat, {int} carbs")]
    public void GivenUserHasRecipeWithMacros(
        string username, string recipeName, int calories, int protein, int fat, int carbs)
    {
        using var ctx = BDDSetup.CreateContext();
        var recipe = new Recipe
        {
            Name = recipeName,
            Directions = "Test directions",
            Calories = calories,
            Protein = protein,
            Fat = fat,
            Carbs = carbs
        };
        ctx.Set<Recipe>().Add(recipe);
        ctx.SaveChanges();
        _recipeIds[(username, recipeName)] = recipe.Id;
    }

    [Given("{string} has a completed meal {string} today with recipe {string}")]
    public void GivenUserHasCompletedMealToday(string username, string mealTitle, string recipeName)
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = SharedSteps.Users[username].Id;
        var recipeId = _recipeIds[(username, recipeName)];
        var recipe = ctx.Set<Recipe>().Single(r => r.Id == recipeId);
        var today = DateTime.Today;

        var meal = new Meal
        {
            UserId = userId,
            Title = mealTitle,
            StartTime = today,
            Recipes = { recipe }
        };
        ctx.Set<Meal>().Add(meal);
        ctx.SaveChanges();

        ctx.Set<MealCompletion>().Add(new MealCompletion
        {
            MealId = meal.Id,
            CompletionDate = today
        });
        ctx.SaveChanges();
    }

    [Given("{string} has nutrition targets of {int} calories, {int} protein, {int} fat, {int} carbs")]
    public void GivenUserHasNutritionTargets(
        string username, int calories, int protein, int fat, int carbs)
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = SharedSteps.Users[username].Id;
        var existing = ctx.Set<UserNutritionPreference>().FirstOrDefault(p => p.UserId == userId);
        if (existing == null)
        {
            ctx.Set<UserNutritionPreference>().Add(new UserNutritionPreference
            {
                UserId = userId,
                CalorieTarget = calories,
                ProteinTarget = protein,
                FatTarget = fat,
                CarbTarget = carbs
            });
        }
        else
        {
            existing.CalorieTarget = calories;
            existing.ProteinTarget = protein;
            existing.FatTarget = fat;
            existing.CarbTarget = carbs;
        }
        ctx.SaveChanges();
    }

    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
    }

    [Given("{string} is on the create recipe page")]
    public void GivenUserIsOnCreateRecipePage(string username)
    {
        EnsureOnCreateRecipePage(username);
    }

    private void EnsureOnCreateRecipePage(string username)
    {
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/AddNewRecipe");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState").ToString() == "complete");

            // Pending login redirect can cause Navigate to land on "/" — retry.
            if (!_driver.Url.Contains("/FoodEntries/AddNewRecipe", StringComparison.OrdinalIgnoreCase))
            {
                if (_driver.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
                {
                    _driver.FindElement(By.Id("Email")).SendKeys($"{username}@fakeemail.com");
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

    [Given("{string} fills in the recipe name as {string}")]
    public void GivenUserFillsInRecipeName(string username, string name)
    {
        _driver.FindElement(By.Id("Name")).SendKeys(name);
    }

    [Given("{string} fills in the recipe directions as {string}")]
    public void GivenUserFillsInRecipeDirections(string username, string directions)
    {
        var input = _driver.FindElement(By.Id("step-input"));
        input.SendKeys(directions);
        input.SendKeys(Keys.Enter);
    }

    [Given("{string} fills in the recipe calories as {string}")]
    public void GivenUserFillsInRecipeCalories(string username, string calories)
    {
        _driver.FindElement(By.Id("Calories")).SendKeys(calories);
    }

    [Given("{string} fills in the recipe protein as {string}")]
    public void GivenUserFillsInRecipeProtein(string username, string protein)
    {
        _driver.FindElement(By.Id("Protein")).SendKeys(protein);
    }

    [Given("{string} fills in the recipe fat as {string}")]
    public void GivenUserFillsInRecipeFat(string username, string fat)
    {
        _driver.FindElement(By.Id("Fat")).SendKeys(fat);
    }

    [Given("{string} fills in the recipe carbs as {string}")]
    public void GivenUserFillsInRecipeCarbs(string username, string carbs)
    {
        _driver.FindElement(By.Id("Carbs")).SendKeys(carbs);
    }

    [Given("{string} submits the recipe form")]
    public void GivenUserSubmitsRecipeForm(string username)
    {
        var btn = _driver.FindElement(By.CssSelector("button.ar-submit-btn[type='submit']"));
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block:'center',behavior:'instant'});", btn);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Given("{string} is on the create meal page for nutrition")]
    public void GivenUserIsOnCreateMealPage(string username)
    {
        // Clear any meals bob has for today from previous test scenarios so
        // GivenUserMarksTheMealAsCompleted always finds exactly the one checkbox we care about.
        var ctx = BDDSetup.Context;
        ctx.ChangeTracker.Clear();
        var user = ctx.Set<User>().FirstOrDefault(u => u.NormalizedEmail == $"{username}@fakeemail.com".ToUpper());
        if (user != null)
        {
            var today = DateTime.Today;
            var existing = ctx.Set<Meal>()
                .Where(m => m.UserId == user.Id && m.StartTime != null && m.StartTime.Value.Date == today)
                .ToList();
            if (existing.Count > 0)
            {
                ctx.Set<Meal>().RemoveRange(existing);
                ctx.SaveChanges();
            }
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")?.ToString() == "complete");
            if (_driver.Url.Contains("/Meal/NewMeal", StringComparison.OrdinalIgnoreCase)) return;
            if (_driver.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
            {
                _driver.FindElement(By.Id("Email")).SendKeys($"{username}@fakeemail.com");
                _driver.FindElement(By.Id("Password")).SendKeys("1234!Abcd");
                _driver.FindElement(By.ClassName("btn")).Click();
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
                    !d.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Given("{string} fills in the meal title as {string}")]
    public void GivenUserFillsInMealTitle(string username, string title)
    {
        new WebDriverWait(_driver, TimeSpan.FromSeconds(5)).Until(d =>
        {
            try { var e = d.FindElement(By.Id("Title")); return e.Displayed ? e : null; }
            catch (NoSuchElementException) { return null; }
        })!.SendKeys(title);
    }

    [Given("{string} sets the meal date to today")]
    public void GivenUserSetsMealDateToToday(string username)
    {
        var monthDropdown = new SelectElement(
            _driver.FindElement(By.Id("SelectedMonth"))
        );

        var dayDropdown = new SelectElement(
            _driver.FindElement(By.Id("SelectedDay"))
        );

        var today = DateTime.Today;
        _mealDate = today;

        monthDropdown.SelectByValue(today.Month.ToString());
        dayDropdown.SelectByValue(today.Day.ToString());
    }

    [Given("{string} searches for recipe {string}")]
    public void GivenUserSearchesForRecipe(string username, string recipeName)
    {
        var searchInput = new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
        {
            try
            {
                var el = driver.FindElement(By.Id("searchText"));
                return (el.Displayed && el.Enabled) ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        })!;

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView(true);", searchInput);
        searchInput.Click();
        searchInput.Clear();
        searchInput.SendKeys(recipeName);
        Thread.Sleep(1100); // wait for search debounce
    }

    [Given("{string} clicks the first recipe result")]
    public void GivenUserClicksFirstRecipeResult(string username)
    {
        var firstResult = new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
        {
            try
            {
                var el = driver.FindElement(By.CssSelector(".recipeSearchRow"));
                return el.Displayed ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        })!;

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView(true);", firstResult);
        firstResult.Click();

        // The redesigned UI requires a second click on "Add selected" to commit
        // the recipe into the form (appends hidden RecipeIds input).
        var addBtn = new WebDriverWait(_driver, TimeSpan.FromSeconds(5)).Until(driver =>
        {
            try
            {
                var el = driver.FindElement(By.Id("addSelectedRecipesBtn"));
                return (el.Displayed && el.Enabled) ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        })!;
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", addBtn);
    }

    [Given("{string} submits the meal form")]
    public void GivenUserSubmitsMealForm(string username)
    {
        var btn = new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d => {
            try
            {
                var el = d.FindElement(By.CssSelector("button[form='createMealForm']"));
                return (el.Displayed && el.Enabled) ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        })!;
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block:'center',behavior:'instant'});", btn);
        var urlBefore = _driver.Url;
        try
        {
            btn.Click();
        }
        catch (ElementClickInterceptedException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        }
        // Wait for the POST to navigate away from NewMeal before declaring done —
        // readyState alone catches the pre-navigation "complete" state too early.
        new WebDriverWait(_driver, TimeSpan.FromSeconds(15)).Until(d => d.Url != urlBefore);
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Given("'(.*)' marks the meal as completed")]
    public void GivenUserMarksTheMealAsCompleted(string user)
    {
        var date = (_mealDate ?? DateTime.Today).ToString("yyyy-MM-dd");
        _driver.Navigate().GoToUrl($"{_baseUrl}/?date={date}");
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");

        // The new Home/Index UI renders the checkbox with appearance:none overlaid on a button
        var checkbox = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d => {
                try { return d.FindElement(By.CssSelector(".MealCheckBox")); }
                catch (NoSuchElementException) { return null; }
            });

        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", checkbox);

        // Wait for navigation to start (form.submit()), then complete
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(3))
                .Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() != "complete");
        }
        catch (WebDriverTimeoutException) { }

        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete"
            && d.FindElements(By.CssSelector(".MealCheckBox")).Count > 0);
    }

    [Then("Meal Bars callories are at {int}\\/{int}")]
    public void ThenMealBarsCaloriesAreAt(int current, int goal)
    {
        var fraction = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try { var el = d.FindElement(By.Id("caloriesFraction")); return el.Displayed ? el : null; }
                catch (NoSuchElementException) { return null; }
            })!;
        Assert.That(fraction.Text.Trim(), Is.EqualTo($"{current} / {goal}"));
    }

    [Then("Meal Bars protien are at {int}\\/{int}")]
    public void ThenMealBarsProteinAreAt(int current, int goal)
    {
        var fraction = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try { var el = d.FindElement(By.Id("proteinFraction")); return el.Displayed ? el : null; }
                catch (NoSuchElementException) { return null; }
            })!;
        Assert.That(fraction.Text.Trim(), Is.EqualTo($"{current} / {goal}"));
    }

    [Then("Meal Bars fats are at {int}\\/{int}")]
    public void ThenMealBarsFatsAreAt(int current, int goal)
    {
        var fraction = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try { var el = d.FindElement(By.Id("fatFraction")); return el.Displayed ? el : null; }
                catch (NoSuchElementException) { return null; }
            })!;
        Assert.That(fraction.Text.Trim(), Is.EqualTo($"{current} / {goal}"));
    }

    [Then("Meal Bars carbs are at {int}\\/{int}")]
    public void ThenMealBarsCarbsAreAt(int current, int goal)
    {
        var fraction = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
            .Until(d =>
            {
                try { var el = d.FindElement(By.Id("carbsFraction")); return el.Displayed ? el : null; }
                catch (NoSuchElementException) { return null; }
            })!;
        Assert.That(fraction.Text.Trim(), Is.EqualTo($"{current} / {goal}"));
    }
    [Given("{string} is on the home page")]
    [When("{string} is on the home page")]
    [Then("{string} is on the home page")]
    public void GivenUserIsOnHomePage(string username)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/");
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Given("{string} is on the page {string}")]
    public void GivenUserIsOnPage(string username, string pageName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/{pageName}");
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");

        if (!_driver.Url.Contains(pageName, StringComparison.OrdinalIgnoreCase))
        {
            // Redirected away — re-authenticate if needed, then retry
            if (_driver.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
            {
                _driver.FindElement(By.Id("Email")).SendKeys($"{username}@fakeemail.com");
                _driver.FindElement(By.Id("Password")).SendKeys("1234!Abcd");
                _driver.FindElement(By.ClassName("btn")).Click();
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
                    !d.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase));
            }
            _driver.Navigate().GoToUrl($"{_baseUrl}/{pageName}");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState").ToString() == "complete");
        }
    }

    [Given("{string} fills in the nutrition targets")]
    public void GivenUserFillsInNutritionTargets(string username)
    {
        // Explicitly click the nutrition nav button to ensure the panel is active
        var nutritionNavBtn = new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
        {
            try
            {
                var el = driver.FindElement(By.CssSelector("button.settings-nav-item[data-section='nutrition']"));
                return el.Displayed ? el : null;
            }
            catch (NoSuchElementException) { return null; }
            catch (StaleElementReferenceException) { return null; }
        })!;
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nutritionNavBtn);

        // Wait for the nutrition panel to be visible
        var calorieInput = new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
        {
            try
            {
                var el = driver.FindElement(By.Id("CalorieTarget"));
                return (el.Displayed && el.Enabled) ? el : null;
            }
            catch (NoSuchElementException) { return null; }
            catch (StaleElementReferenceException) { return null; }
        })!;

        void SetInput(IWebElement el, string value)
        {
            el.Clear();
            el.SendKeys(value);
        }

        SetInput(calorieInput, "40");
        SetInput(_driver.FindElement(By.Id("ProteinTarget")), "50");
        SetInput(_driver.FindElement(By.Id("CarbTarget")), "70");
        SetInput(_driver.FindElement(By.Id("FatTarget")), "60");

        _driver.FindElement(By.CssSelector("#form-nutrition button.settings-save-btn")).Click();
        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(driver =>
            ((IJavaScriptExecutor)driver)
                .ExecuteScript("return document.readyState").ToString() == "complete");
    }
}