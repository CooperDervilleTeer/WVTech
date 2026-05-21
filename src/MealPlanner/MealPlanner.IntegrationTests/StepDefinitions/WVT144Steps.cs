using MealPlanner.Models;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Identity;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
using NUnit.Framework;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT144Steps
{
    private IWebDriver _driver = null!;
    private WebDriverWait _wait = null!;
    private string _baseUrl = null!;

    private string _mealNameBeforeRegeneration = string.Empty;

    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _wait = BDDSetup.Wait;
        _baseUrl = AUTHost.BaseUrl;
    }

    [Given("{string} has a calorie target set")]
    public void GivenUserHasACalorieTargetSet(string userName)
    {
        var ctx = BDDSetup.Context;
        ctx.ChangeTracker.Clear();
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var existing = ctx.Set<UserNutritionPreference>().FirstOrDefault(p => p.UserId == user.Id);
        if (existing != null) return;

        ctx.Add(new UserNutritionPreference { UserId = user.Id, CalorieTarget = 2000 });
        ctx.SaveChanges();
    }

    [When("{string} navigates to the Create Meal page")]
    public void WhenUserNavigatesToCreateMealPage(string userName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
        _wait.Until(d => d.Url.Contains("/Meal/NewMeal"));
    }

    [Then("a {string} button is visible alongside the existing generate meal button")]
    public void ThenButtonIsVisibleAlongsideGenerateMealButton(string buttonLabel)
    {
        var buttons = _driver.FindElements(By.TagName("button"))
            .Concat(_driver.FindElements(By.TagName("a")));
        var match = buttons.FirstOrDefault(el =>
            el.Text.Contains(buttonLabel, StringComparison.OrdinalIgnoreCase));
        Assert.That(match, Is.Not.Null, $"Could not find a button or link labelled '{buttonLabel}'");
        Assert.That(match!.Displayed, Is.True);
    }

    [Given("{string} is on the Create Meal page")]
    public void GivenUserIsOnCreateMealPage(string userName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
        _wait.Until(d => d.Url.Contains("/Meal/NewMeal"));
    }

    [When("{string} clicks {string}")]
    public void WhenUserClicksButton(string userName, string buttonLabel)
    {
        var buttons = _driver.FindElements(By.TagName("button"))
            .Concat(_driver.FindElements(By.TagName("a")));
        var btn = buttons.First(el =>
            el.Text.Contains(buttonLabel, StringComparison.OrdinalIgnoreCase));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("{string} is asked how many meals he would like for the day")]
    public void ThenUserIsAskedHowManyMeals(string userName)
    {
        _wait.Until(d => (bool)((IJavaScriptExecutor)d)
            .ExecuteScript("return document.getElementById('dayPlanModal')?.classList.contains('show') === true"));
        var input = _driver.FindElement(By.Id("MealCount"));
        Assert.That(input, Is.Not.Null, "Could not find meal count input (#MealCount)");
        Assert.That(input.Displayed, Is.True);
    }

    [When("{string} enters {int} for the number of meals")]
    public void WhenUserEntersMealCount(string userName, int count)
    {
        var input = _driver.FindElement(By.Id("MealCount"));
        input.Clear();
        input.SendKeys(count.ToString());
    }

    [Given("{string} has specified {int} meals for his day plan")]
    public void GivenUserHasSpecifiedMeals(string userName, int mealCount)
    {
        ClearTodaysMealsForUser(userName);
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
        _wait.Until(d => d.Url.Contains("/Meal/NewMeal"));

        var showWizard = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("showDayPlanWizard")); }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", showWizard!);

        _wait.Until(d => (bool)((IJavaScriptExecutor)d)
            .ExecuteScript("return document.getElementById('dayPlanModal')?.classList.contains('show') === true"));

        var input = _driver.FindElement(By.Id("MealCount"));
        input.Clear();
        input.SendKeys(mealCount.ToString());
    }

    private void ClearTodaysMealsForUser(string userName)
    {
        var ctx = BDDSetup.Context;
        ctx.ChangeTracker.Clear();
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());
        var today = DateTime.Today;
        var existing = ctx.Set<Meal>()
            .Where(m => m.UserId == user.Id && m.StartTime != null && m.StartTime.Value.Date == today)
            .ToList();
        ctx.Set<Meal>().RemoveRange(existing);
        ctx.SaveChanges();
    }

    [When("{string} is presented with the configuration for each meal")]
    public void WhenUserIsPresentedWithMealConfig(string userName)
    {
        var nextBtn = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector("[data-action='next-step']"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        if (nextBtn != null)
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nextBtn);
    }

    [Then("{string} is asked what size he would like each meal to be")]
    public void ThenUserIsAskedMealSize(string userName)
    {
        var el = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector("select[name*='MealPreferences']"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        Assert.That(el, Is.Not.Null, "Could not find meal size selector for first meal");
    }

    [Then("the meal size options include {string}, {string}, and {string}")]
    public void ThenMealSizeOptionsInclude(string option1, string option2, string option3)
    {
        var select = new SelectElement(
            _driver.FindElement(By.CssSelector("select[name*='MealPreferences'][name$='.Size']")));
        var optionTexts = select.Options.Select(o => o.Text).ToList();
        Assert.That(optionTexts, Does.Contain(option1), $"Meal size option '{option1}' not found");
        Assert.That(optionTexts, Does.Contain(option2), $"Meal size option '{option2}' not found");
        Assert.That(optionTexts, Does.Contain(option3), $"Meal size option '{option3}' not found");
    }

    [Then("{string} is asked what type of food he would like using tags")]
    public void ThenUserIsAskedFoodTypeUsingTags(string userName)
    {
        var el = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".tag-select"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        Assert.That(el, Is.Not.Null, "Could not find tag selector for meal configuration");
    }

    [When("{string} enters {string} as the title for the first meal")]
    public void WhenUserEntersTitleForFirstMeal(string userName, string title)
    {
        var input = _wait.Until(d =>
        {
            try { return d.FindElement(By.CssSelector("input[name='MealPreferences[0].Title']")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(input, Is.Not.Null, "Could not find title input for first meal (input[name='MealPreferences[0].Title'])");
        input!.Clear();
        input.SendKeys(title);
    }

    [Then("the summary contains a meal titled {string}")]
    public void ThenSummaryContainsMealTitled(string title)
    {
        var meals = _driver.FindElements(By.CssSelector("[data-meal-name]"));
        Assert.That(meals.Any(m => m.Text.Trim() == title), Is.True,
            $"No meal titled '{title}' found in the summary");
    }

    [Then("the second meal in the summary uses the default naming scheme")]
    public void ThenSecondMealUsesDefaultNaming()
    {
        var meals = _driver.FindElements(By.CssSelector("[data-meal-name]"));
        Assert.That(meals.Count, Is.GreaterThanOrEqualTo(2), "Expected at least 2 meals in the summary");
        var secondTitle = meals[1].Text.Trim();
        Assert.That(secondTitle, Does.Match(@"^Meal \d+$"),
            $"Expected second meal to follow default naming 'Meal N', but was '{secondTitle}'");
    }

    [Then("{string} can enter a custom tag name for the meal")]
    public void ThenUserCanEnterCustomTagName(string userName)
    {
        var el = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".custom-tag-input"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        Assert.That(el, Is.Not.Null, "Could not find custom tag input for meal configuration");
    }

    [When("{string} types {string} as a custom tag and generates the plan")]
    public void WhenUserTypesCustomTagAndGenerates(string userName, string tagName)
    {
        var input = _driver.FindElement(By.CssSelector(".custom-tag-input"));
        input.Clear();
        input.SendKeys(tagName);

        // Advance through any remaining meal steps
        while (true)
        {
            var nextBtn = _driver.FindElements(By.Id("btnNextMeal"))
                .FirstOrDefault(e => e.Displayed);
            if (nextBtn == null) break;
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nextBtn);
        }

        var submit = _wait.Until(d =>
        {
            try { return d.FindElements(By.Id("btnGeneratePlan")).FirstOrDefault(e => e.Displayed); }
            catch { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", submit!);
        _wait.Until(d => d.Url.Contains("/Meal/DayPlanSummary"));
    }

    [Given("{string} has completed the day plan configuration")]
    public void GivenUserHasCompletedDayPlanConfig(string userName)
    {
        ClearTodaysMealsForUser(userName);
        var ctx = BDDSetup.Context;
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var recipe = new Recipe
        {
            Name = "Day Plan Test Recipe",
            Directions = "Test",
            Calories = 400,
            Protein = 20,
            Fat = 10,
            Carbs = 50
        };
        ctx.Add(recipe);
        ctx.SaveChanges();
        ctx.Add(new UserRecipe
        {
            UserId = user.Id,
            RecipeId = recipe.Id,
            UserOwner = true,
            UserVote = UserVoteType.NoVote
        });
        ctx.SaveChanges();

        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
        _wait.Until(d => d.Url.Contains("/Meal/NewMeal"));

        var showWizard = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("showDayPlanWizard")); }
            catch (NoSuchElementException) { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", showWizard!);

        _wait.Until(d => (bool)((IJavaScriptExecutor)d)
            .ExecuteScript("return document.getElementById('dayPlanModal')?.classList.contains('show') === true"));

        var input = _driver.FindElement(By.Id("MealCount"));
        input.Clear();
        input.SendKeys("1");

        var nextBtn = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector("[data-action='next-step']"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        if (nextBtn != null)
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nextBtn);
    }

    [When("the day plan is generated")]
    public void WhenTheDayPlanIsGenerated()
    {
        // Advance through any remaining meal steps before the Generate button appears
        while (true)
        {
            var nextBtn = _driver.FindElements(By.Id("btnNextMeal"))
                .FirstOrDefault(e => e.Displayed);
            if (nextBtn == null) break;
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nextBtn);
        }

        var submit = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.Id("btnGeneratePlan"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", submit!);
        _wait.Until(d => d.Url.Contains("/Meal/DayPlanSummary"));
    }

    [Then("{string} sees a summary of his meal plan for the day")]
    public void ThenUserSeesASummary(string userName)
    {
        var summary = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("day-plan-summary")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(summary, Is.Not.Null, "Could not find day plan summary (#day-plan-summary)");
        Assert.That(summary!.Displayed, Is.True);
    }

    [Then("the summary shows each recommended meal by name")]
    public void ThenSummaryShowsEachMealByName()
    {
        var meals = _driver.FindElements(By.CssSelector("[data-meal-name]"));
        Assert.That(meals.Count, Is.GreaterThan(0), "No meal names found in the summary");
        Assert.That(meals.All(m => !string.IsNullOrWhiteSpace(m.Text)), Is.True);
    }

    [Given("{string} is viewing his generated day plan summary")]
    public void GivenUserIsViewingDayPlanSummary(string userName)
    {
        GivenUserHasCompletedDayPlanConfig(userName);
        WhenTheDayPlanIsGenerated();
        var meals = _driver.FindElements(By.CssSelector("[data-meal-name]"));
        _mealNameBeforeRegeneration = meals.First().Text;
    }

    [When("{string} chooses to regenerate one of the meals")]
    public void WhenUserChoosesToRegenerateAMeal(string userName)
    {
        var regenBtn = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector("[data-action='regenerate-meal']"))
                    .FirstOrDefault(e => e.Displayed);
            }
            catch { return null; }
        });
        Assert.That(regenBtn, Is.Not.Null, "Could not find regenerate button");
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", regenBtn!);

        _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector("[id^='regenerate-form-']"))
                    .Any(e => e.Displayed);
            }
            catch { return false; }
        });
    }

    [Then("{string} is shown the meal configuration form inline on the summary page")]
    public void ThenUserIsShownInlineMealConfig(string userName)
    {
        var sizeSelect = _wait.Until(d =>
        {
            try
            {
                var visibleForm = d.FindElements(By.CssSelector("[id^='regenerate-form-']"))
                    .FirstOrDefault(f => f.Displayed);
                return visibleForm?.FindElement(By.CssSelector("select[name='Size']"));
            }
            catch { return null; }
        });
        Assert.That(sizeSelect, Is.Not.Null, "Could not find inline size selector (select[name='Size'] inside visible regenerate form)");

        var select = new SelectElement(sizeSelect!);
        Assert.That(select.SelectedOption.Text, Is.Not.Empty, "Size selector has no selection");
    }

    [When("{string} confirms the configuration and regenerates")]
    public void WhenUserConfirmsAndRegenerates(string userName)
    {
        var submit = _wait.Until(d =>
        {
            try
            {
                var forms = d.FindElements(By.CssSelector("[id^='regenerate-form-']"));
                var visibleForm = forms.FirstOrDefault(f => f.Displayed);
                return visibleForm?.FindElement(By.CssSelector("[data-action='submit-regenerate']"));
            }
            catch { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", submit!);
        // Wait for the regenerate form to hide (fetch completed and DOM updated)
        _wait.Until(d =>
        {
            try
            {
                var forms = d.FindElements(By.CssSelector("[id^='regenerate-form-']"));
                return forms.All(f => !f.Displayed);
            }
            catch { return false; }
        });
    }

    [Given("{string} has a {string} dietary restriction")]
    public void GivenUserHasDietaryRestriction(string userName, string restrictionName)
    {
        var ctx = BDDSetup.Context;
        ctx.ChangeTracker.Clear();
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var stale = ctx.Set<UserDietaryRestriction>().Where(udr => udr.UserId == user.Id).ToList();
        ctx.Set<UserDietaryRestriction>().RemoveRange(stale);

        // Clear upvoted recipes to prevent contamination from WVT-176 scenarios
        var upvotes = ctx.Set<UserRecipe>()
            .Where(ur => ur.UserId == user.Id && ur.UserVote == UserVoteType.UpVote)
            .ToList();
        ctx.Set<UserRecipe>().RemoveRange(upvotes);

        ctx.SaveChanges();

        var restriction = ctx.Set<DietaryRestriction>().FirstOrDefault(dr => dr.Name == restrictionName);
        if (restriction == null)
        {
            restriction = new DietaryRestriction { Name = restrictionName };
            ctx.Add(restriction);
            ctx.SaveChanges();
        }

        ctx.Add(new UserDietaryRestriction { UserId = user.Id, DietaryRestrictionId = restriction.Id });
        ctx.SaveChanges();
    }

    [Given("{string} has a recipe tagged {string} named {string}")]
    public void GivenUserHasRecipeTaggedNamed(string userName, string tagName, string recipeName)
    {
        var ctx = BDDSetup.Context;
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var tag = ctx.Set<Tag>().FirstOrDefault(t => t.Name == tagName);
        if (tag == null)
        {
            tag = new Tag { Name = tagName };
            ctx.Add(tag);
            ctx.SaveChanges();
        }

        var recipe = new Recipe
        {
            Name = recipeName,
            Directions = "Test",
            Calories = 400,
            Protein = 20,
            Fat = 10,
            Carbs = 50,
            Tags = [tag]
        };
        ctx.Add(recipe);
        ctx.SaveChanges();

        ctx.Add(new UserRecipe { UserId = user.Id, RecipeId = recipe.Id, UserOwner = true, UserVote = UserVoteType.UpVote });
        ctx.SaveChanges();
    }

    [Given("{string} has a recipe named {string} without any tags")]
    public void GivenUserHasRecipeNamedWithoutTags(string userName, string recipeName)
    {
        var ctx = BDDSetup.Context;
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var recipe = new Recipe
        {
            Name = recipeName,
            Directions = "Test",
            Calories = 400,
            Protein = 20,
            Fat = 10,
            Carbs = 50
        };
        ctx.Add(recipe);
        ctx.SaveChanges();

        ctx.Add(new UserRecipe { UserId = user.Id, RecipeId = recipe.Id, UserOwner = true, UserVote = UserVoteType.DownVote });
        ctx.SaveChanges();
    }

    [Then("the day plan summary includes a recipe named {string}")]
    public void ThenDayPlanSummaryIncludesRecipeNamed(string recipeName)
    {
        var items = _driver.FindElements(By.CssSelector("#day-plan-summary .mealRecipeItem h4"));
        Assert.That(items.Any(el => el.Text.Contains(recipeName, StringComparison.OrdinalIgnoreCase)), Is.True,
            $"Expected recipe '{recipeName}' in the day plan summary but it was not found");
    }

    [Then("the day plan summary does not include a recipe named {string}")]
    public void ThenDayPlanSummaryDoesNotIncludeRecipeNamed(string recipeName)
    {
        var items = _driver.FindElements(By.CssSelector("#day-plan-summary .mealRecipeItem h4"));
        Assert.That(items.All(el => !el.Text.Contains(recipeName, StringComparison.OrdinalIgnoreCase)), Is.True,
            $"Expected recipe '{recipeName}' NOT to be in the day plan summary but it was found");
    }

    [Then("the updated meal appears in the summary in place of the previous recommendation")]
    public void ThenUpdatedMealAppearsInSummary()
    {
        var meals = _driver.FindElements(By.CssSelector("[data-meal-name]"));
        Assert.That(meals.Count, Is.GreaterThan(0), "No meals in summary after regeneration");
    }

    [Then("all other meals in the summary remain unchanged")]
    public void ThenOtherMealsRemainUnchanged()
    {
        var meals = _driver.FindElements(By.CssSelector("[data-meal-name]"));
        Assert.That(meals.Count, Is.EqualTo(1),
            "Expected the same number of meals in the summary after regenerating one");
    }

    [Given("{string} has a daily calorie target of {int}")]
    public void GivenUserHasSpecificCalorieTarget(string userName, int target)
    {
        var ctx = BDDSetup.Context;
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());
        var existing = ctx.Set<UserNutritionPreference>().FirstOrDefault(p => p.UserId == user.Id);
        if (existing != null)
        {
            existing.CalorieTarget = target;
        }
        else
        {
            ctx.Add(new UserNutritionPreference { UserId = user.Id, CalorieTarget = target });
        }
        ctx.SaveChanges();
    }

    [Given("{string} has {int} upvoted recipes each with {int} calories")]
    public void GivenUserHasUpvotedRecipesEachWithCalories(string userName, int count, int calories)
    {
        var ctx = BDDSetup.Context;
        ctx.ChangeTracker.Clear();
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var existingUpvotes = ctx.Set<UserRecipe>()
            .Where(ur => ur.UserId == user.Id && ur.UserVote == UserVoteType.UpVote)
            .ToList();
        ctx.Set<UserRecipe>().RemoveRange(existingUpvotes);

        var existingRestrictions = ctx.Set<UserDietaryRestriction>()
            .Where(udr => udr.UserId == user.Id)
            .ToList();
        ctx.Set<UserDietaryRestriction>().RemoveRange(existingRestrictions);

        ctx.SaveChanges();

        for (int i = 0; i < count; i++)
        {
            var recipe = new Recipe
            {
                Name = $"WVT176 {calories}cal #{i + 1}",
                Directions = "Test",
                Calories = calories
            };
            ctx.Add(recipe);
            ctx.SaveChanges();
            ctx.Add(new UserRecipe { UserId = user.Id, RecipeId = recipe.Id, UserOwner = false, UserVote = UserVoteType.UpVote });
            ctx.SaveChanges();
        }
    }

    [When("{string} sets meal {int} to {string} and meal {int} to {string}")]
    public void WhenUserSetsMealSizes(string userName, int meal1, string size1, int meal2, string size2)
    {
        var sizeSelect1 = _wait.Until(d =>
        {
            try { return d.FindElements(By.CssSelector($"select[name='MealPreferences[{meal1 - 1}].Size']")).FirstOrDefault(e => e.Displayed); }
            catch { return null; }
        });
        new SelectElement(sizeSelect1!).SelectByValue(size1);

        var nextBtn = _wait.Until(d =>
            d.FindElements(By.Id("btnNextMeal")).FirstOrDefault(e => e.Displayed));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", nextBtn!);

        var sizeSelect2 = _wait.Until(d =>
        {
            try { return d.FindElements(By.CssSelector($"select[name='MealPreferences[{meal2 - 1}].Size']")).FirstOrDefault(e => e.Displayed); }
            catch { return null; }
        });
        new SelectElement(sizeSelect2!).SelectByValue(size2);
    }

    [When("{string} sets the regenerate size to {string} and confirms")]
    public void WhenUserSetsRegenerateSizeAndConfirms(string userName, string size)
    {
        var sizeSelect = _wait.Until(d =>
        {
            try
            {
                var visibleForm = d.FindElements(By.CssSelector("[id^='regenerate-form-']")).FirstOrDefault(f => f.Displayed);
                return visibleForm?.FindElement(By.CssSelector("select[name='Size']"));
            }
            catch { return null; }
        });
        new SelectElement(sizeSelect!).SelectByValue(size);

        var submit = _wait.Until(d =>
        {
            try
            {
                var visibleForm = d.FindElements(By.CssSelector("[id^='regenerate-form-']")).FirstOrDefault(f => f.Displayed);
                return visibleForm?.FindElement(By.CssSelector("[data-action='submit-regenerate']"));
            }
            catch { return null; }
        });
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", submit!);

        _wait.Until(d =>
        {
            try { return d.FindElements(By.CssSelector("[id^='regenerate-form-']")).All(f => !f.Displayed); }
            catch { return false; }
        });
    }

    [Then("the first meal in the day plan summary shows more than {int} total calories")]
    public void ThenFirstMealShowsMoreThanCalories(int threshold)
    {
        var mealCards = _wait.Until(d =>
        {
            try
            {
                var cards = d.FindElements(By.CssSelector("#day-plan-summary [id^='meal-card-']"));
                return cards.Count > 0 ? cards : null;
            }
            catch { return null; }
        });
        var total = GetMealTotalCalories(mealCards![0]);
        Assert.That(total, Is.GreaterThan(threshold),
            $"Expected first meal to show more than {threshold} cal but was {total}");
    }

    [Then("the second meal in the day plan shows more total calories than the first meal")]
    public void ThenSecondMealShowsMoreCaloriesThanFirst()
    {
        var mealCards = _wait.Until(d =>
        {
            try
            {
                var cards = d.FindElements(By.CssSelector("#day-plan-summary [id^='meal-card-']"));
                return cards.Count >= 2 ? cards : null;
            }
            catch { return null; }
        });
        Assert.That(mealCards, Is.Not.Null, "Expected at least 2 meal cards in the summary");
        var firstTotal = GetMealTotalCalories(mealCards![0]);
        var secondTotal = GetMealTotalCalories(mealCards[1]);
        Assert.That(secondTotal, Is.GreaterThan(firstTotal),
            $"Expected second meal ({secondTotal} cal) to have more calories than first ({firstTotal} cal)");
    }

    private static int GetMealTotalCalories(IWebElement card)
    {
        var spans = card.FindElements(By.CssSelector(".mealRecipeItem span.text-nowrap"));
        return spans.Sum(s =>
        {
            var text = s.Text.Replace(" cal", "").Trim();
            return int.TryParse(text, out var n) ? n : 0;
        });
    }

    [Given("{string} has a nutrition target of {int} calories, {int}g protein, {int}g carbs, and {int}g fat")]
    public void GivenUserHasFullNutritionTarget(string userName, int calories, int protein, int carbs, int fat)
    {
        var ctx = BDDSetup.Context;
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());
        var existing = ctx.Set<UserNutritionPreference>().FirstOrDefault(p => p.UserId == user.Id);
        if (existing != null)
        {
            existing.CalorieTarget = calories;
            existing.ProteinTarget = protein;
            existing.CarbTarget = carbs;
            existing.FatTarget = fat;
        }
        else
        {
            ctx.Add(new UserNutritionPreference
            {
                UserId = user.Id,
                CalorieTarget = calories,
                ProteinTarget = protein,
                CarbTarget = carbs,
                FatTarget = fat
            });
        }
        ctx.SaveChanges();
    }

    [Given("{string} has a recipe named {string} with {int} calories, {int}g protein, {int}g carbs, and {int}g fat tagged {string}")]
    public void GivenUserHasRecipeWithMacrosTagged(string userName, string recipeName, int calories, int protein, int carbs, int fat, string tagName)
    {
        var ctx = BDDSetup.Context;
        var user = ctx.Set<User>().First(u => u.NormalizedEmail == $"{userName}@fakeemail.com".ToUpper());

        var tag = ctx.Set<Tag>().FirstOrDefault(t => t.Name == tagName);
        if (tag == null)
        {
            tag = new Tag { Name = tagName };
            ctx.Add(tag);
            ctx.SaveChanges();
        }

        var recipe = new Recipe
        {
            Name = recipeName,
            Directions = "Test",
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fat = fat,
            Tags = [tag]
        };
        ctx.Add(recipe);
        ctx.SaveChanges();

        ctx.Add(new UserRecipe { UserId = user.Id, RecipeId = recipe.Id, UserOwner = true, UserVote = UserVoteType.NoVote });
        ctx.SaveChanges();
    }
}
