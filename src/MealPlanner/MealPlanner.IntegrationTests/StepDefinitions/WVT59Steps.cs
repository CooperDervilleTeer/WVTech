using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT59Steps
{
    IWebDriver _driver;
    string _baseUrl;
    WebDriverWait _wait;
    readonly string _mealTitle = "test meal";
    MealPlannerDBContext _context;
    
    // Runs before each scenerio
    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
        _context = BDDSetup.Context;
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("he sees a recommend meal button")]
    public void ThenHeSeesARecommendMealButton()
    {
        IWebElement recommendButton = _wait.Until(d => d.FindElement(By.Id("recommendMeal")));
        Assert.That(recommendButton.Displayed);
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("he clicks the recommend meal button")]
    public void WhenHeClicksTheRecommendMealButton()
    {
        _wait.Until(d => d.FindElement(By.Id("recommendMeal"))).Click();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("he enters the meal title")]
    public void WhenHeEntersTheMealTitle()
    {
        IWebElement genMealTitle = _wait.Until(d => d.FindElement(By.Id("recommendMealTitle")));
        genMealTitle.SendKeys(_mealTitle);
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("he is redirected to that meals meal page")]
    public void ThenHeIsRedirectedToThatMealSMealPage()
    {
        _wait.Until(d => d.Url.Contains("Meal/ViewMeal"));
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("he sees a newly generated meal with that title")]
    public void ThenHeSeesANewlyGeneratedMeal()
    {
        IWebElement resultMealTitle = _wait.Until(d => d.FindElement(By.Id("mealTitle")));
        Assert.That(resultMealTitle.Text, Is.EqualTo(_mealTitle));
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("he clicks the generate meal button")]
    public void WhenHeClicksTheGenerateMealButton()
    {
        _wait.Until(d => d.FindElement(By.Id("finishRecommendMeal"))).Click();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("{string} has no other meals")]
    public void GivenHasNoOtherMeals(string userName)
    {
        _context.ChangeTracker.Clear();
        User user = SharedSteps.Users[userName];
        var userMeals = _context.Meals.Where(m => m.UserId == user.Id);
        _context.RemoveRange(userMeals);
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("he has a meal recommended for him")]
    public void WhenHeHasAMealRecommendedForHim()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
        _wait.Until(d => d.Url.Contains("Meal/NewMeal"));
        _driver.FindElement(By.Id("recommendMeal")).Click();
        _wait.Until(d => d.FindElement(By.Id("recommendMealTitle"))).SendKeys(_mealTitle);
        _driver.FindElement(By.Id("finishRecommendMeal")).Click();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("his new meal contains the recipe named {string}")]
    public void ThenHisNewMealContainsTheRecipeNamed(string recipeName)
    {
        _wait.Until(d => d.Url.Contains("ViewMeal"));

        var recipes = _driver.FindElements(By.ClassName("mealRecipeItem"));
        foreach (var element in recipes)
        {
            if (element.Text.Contains(recipeName))
            {
                Assert.Pass();
                return;
            }
        }
        Assert.Fail($"No recipes with name:{recipeName} found");
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("no other recipes have been upvoted")]
    public void GivenNoOtherRecipesHaveBeenUpvoted()
    {
        var upvoted = _context.Set<UserRecipe>().Where(ur => ur.RecipeId != -1);
        _context.RemoveRange(upvoted);
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("{string} has a daily calorie limit of {int} calories")]
    public void GivenHasADailyCalorieLimitOfCalories(string userName, int calorieLimit)
    {
        User user = SharedSteps.Users[userName];
        var existing = _context.Set<UserNutritionPreference>().FirstOrDefault(p => p.UserId == user.Id);
        if (existing != null)
        {
            existing.CalorieTarget = calorieLimit;
        }
        else
        {
            _context.Add(new UserNutritionPreference { UserId = user.Id, CalorieTarget = calorieLimit });
        }
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("he sees a newly generated meal with at least one recipe")]
    public void ThenHeSeesANewlyGeneratedMealWithAtLeastOneRecipe()
    {
        _wait.Until(d => d.Url.Contains("ViewMeal"));
        var recipes = _driver.FindElements(By.ClassName("mealRecipeItem"));
        Assert.That(recipes, Is.Not.Empty);
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("the meal has less total calories than {string}s calorie limit")]
    public void ThenTheMealHasLessTotalCaloriesThanJackSCalorieLimit(string userName)
    {
        User user = SharedSteps.Users[userName];
        int calorieTarget = _context.Set<UserNutritionPreference>().Where(p => p.UserId == user.Id).First().CalorieTarget ?? 0;
        Assert.That(calorieTarget, Is.Not.Zero);
        Meal meal = _context.Meals.Where(m => m.UserId == user.Id).First();
        int totalCalories = meal.Recipes.Sum(r => r.Calories);
        Assert.That(totalCalories, Is.LessThan(calorieTarget));
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("he has downvoted all recipes other than {string}")]
    public void GivenHeHasDownvotedAllRecipesOtherThan(string recipeToKeep)
    {
        User jack = SharedSteps.Users["Jack"];
        var recipes = _context.Recipes.ToList();
        foreach (var recipe in recipes)
        {
            if (recipe.Name == recipeToKeep) continue;

            var ur = _context.Set<UserRecipe>().Where(ur => ur.UserId == jack.Id && ur.RecipeId == recipe.Id).FirstOrDefault();
            
            if (ur == null)
            {
                ur = new UserRecipe
                {
                    UserId = jack.Id,
                    RecipeId = recipe.Id,
                    UserVote = UserVoteType.DownVote
                };
                _context.Add(ur);
            }
            else if (ur.UserVote != UserVoteType.DownVote)
            {
                ur.UserVote = UserVoteType.DownVote;
                _context.Update(ur);
            }
        }
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("he sees a newly generated meal with only {string}")]
    public void ThenHeSeesANewlyGeneratedMealWithOnly(string recipeName)
    {
        _wait.Until(d => d.Url.Contains("ViewMeal"));
        var recipes = _driver.FindElements(By.ClassName("mealRecipeItem"));
        Assert.That(recipes, Is.Not.Empty);
        foreach(var r in recipes)
        {
            Assert.That(r.Text, Does.Contain(recipeName));
        }
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("{string} has a meal with all recipes other than {string}")]
    public void GivenHasAMealWithAllRecipesOtherThan(string userName, string recipeName)
    {
        User user = SharedSteps.Users[userName];
        Meal meal = new Meal()
        {
            Title = "Big",
            UserId = user.Id,
            StartTime = DateTime.UtcNow
        };
        meal.Recipes = _context.Recipes.Where(r => r.Name != recipeName).ToList();
        _context.Add(meal);
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("{string} has another meal that has a recipe with {int} calories")]
    public void GivenHasAnotherMealThatHasARecipeWithCalories(string userName, int mealCalories)
    {
        User user = SharedSteps.Users[userName];
        Recipe recipe = new Recipe() { Calories = mealCalories, Name = "Filler", Directions = ""};
        // StartTime is anchored to local today (not DateTime.UtcNow) so the
        // seed meal lands on the same calendar date the controller uses for
        // the new meal — otherwise on the west coast (UTC-7/-8) the meal
        // could roll onto tomorrow's UTC date, get filtered out of the
        // assertion's same-day calorie sum, and silently hide a regression.
        Meal meal = new Meal()
        {
            Title = "calorie filler",
            UserId = user.Id,
            StartTime = DateTime.Today,
            Recipes = [recipe]
        };
        _context.AddRange(recipe, meal);
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("all meals in the day have less combined calories than {string}s calorie limit")]
    public void ThenAllMealsInTheDayHaveLessCombinedCaloriesThanJackSCalorieLimit(string userName)
    {
        User user = SharedSteps.Users[userName];
        UserNutritionPreference prefs = _context.Set<UserNutritionPreference>().Where(p => p.UserId == user.Id).First();
        int calorieCount = _context.Meals
            .Where(m => m.UserId==user.Id && m.StartTime!.Value.Date == DateTime.Today)
            .SelectMany(m => m.Recipes)
            .Sum(r => r.Calories);
        Assert.That(calorieCount, Is.LessThanOrEqualTo(prefs.CalorieTarget!));
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("{string} has no daily calorie limit")]
    public void GivenHasNoDailyCalorieLimit(string userName)
    {
        User user = SharedSteps.Users[userName];
        var prefs = _context.Set<UserNutritionPreference>().FirstOrDefault(p => p.UserId == user.Id);
        if (prefs != null)
        {
            prefs.CalorieTarget = int.MaxValue;
        }
        else
        {
            _context.Add(new UserNutritionPreference { UserId = user.Id, CalorieTarget = int.MaxValue });
        }
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("the database has recipes with combined calories greater than {int}")]
    public void GivenTheDatabaseHasRecipesWithCombinedCaloriesGreaterThan(int i)
    {
        while(_context.Recipes.Sum(r => r.Calories) < i)
        {
            _context.Add(new Recipe() { Name="Test Recipe", Directions="", Calories=200});
        }
        _context.SaveChanges();
    }

}