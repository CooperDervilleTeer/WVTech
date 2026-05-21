using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;
namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT34Steps
{
    IWebDriver _driver;
    string _baseUrl;
    MealPlannerDBContext _context;
    WebDriverWait _wait;
    int _recipeId;
    
    // Runs before each scenerio
    [BeforeScenario]
    public void SetUp()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _context = BDDSetup.Context;
        _wait = BDDSetup.Wait;
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Then("he sees the recipe has a rating of {string}")]
    public void ThenHeSeesTheRecipeHasARatingOf(string rating)
    {
        IWebElement voteElement = _driver.FindElement(By.Id("votePercent"));
        WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(3));
        wait.Until(_ => voteElement.Text == rating);
        Assert.That(voteElement.Text, Is.EqualTo(rating));
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("he upvotes the recipe")]    
    [When("she upvotes the recipe")]
    public void WhenTheyUpvotesTheRecipe()
    {
        _driver.FindElement(By.Id("thumbs-up")).Click();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("{string} had upvoted the recipe")]
    public void GivenHadUpvotedTheRecipe(string userName)
    {
        var userId = SharedSteps.Users[userName].Id;
        var userRecipe = _context.Find<UserRecipe>(userId, _recipeId);
        if (userRecipe == null)
        {
            userRecipe = new UserRecipe { UserId = userId, RecipeId = _recipeId, UserVote = UserVoteType.UpVote };
            _context.Add(userRecipe);
        }
        else
        {
            userRecipe.UserVote = UserVoteType.UpVote;
        }
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [When("he downvotes the recipe")]
    public void WhenHeDownvotesTheRecipe()
    {
        _driver.FindElement(By.Id("thumbs-down")).Click();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("there is a recipe named {string} with no votes")]
    public void GivenThereIsARecipeNamedWithNoVotes(string recipeName)
    {
        _context.ChangeTracker.Clear();
        Recipe? recipe = _context.Set<Recipe>()
            .Where(r => r.Name == recipeName)
            .FirstOrDefault();

        recipe ??= new Recipe()
        {
            Name = recipeName,
            Directions = "",
        };

        if (recipe.Id == 0)
        {
            recipe = _context.Add(recipe).Entity;
            _context.SaveChanges();
        }

        _recipeId = recipe.Id;

        _context.RemoveRange(_context.Set<UserRecipe>().Where(ur => ur.RecipeId == _recipeId));
        _context.SaveChanges();
    }

    // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
    [Given("he is on the recipe page for the recipe")]
    public void GivenHeIsOnTheRecipePageForTheRecipe()
    {
        try { _wait.Until(d => !d.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase)); }
        catch (WebDriverTimeoutException) { }
        _driver.Navigate().GoToUrl($"{_baseUrl}/FoodEntries/Recipes/{_recipeId}");
        _wait.Until(d => ((IJavaScriptExecutor) d).ExecuteScript("return document.readyState")!.ToString() == "complete");
    }
}