using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests
{
    [Binding]
    public class WVT128Steps
    {
        private readonly ScenarioContext _scenarioContext;
        private int _mealId;
        private string _userId = null!;
        private string _deletedRecipeName = null!;
        private IWebDriver _driver = null!;
        private string _baseUrl = null!;
        private WebDriverWait _wait = null!;

        public WVT128Steps(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;
        }

        // Runs before each scenerio
        [BeforeScenario]
        public void SetUp()
        {
            _driver = BDDSetup.Driver;
            _baseUrl = AUTHost.BaseUrl;
            _wait = BDDSetup.Wait;
        }

        [Given("'Jack' has a meal with recipes created")]
        public void GivenJackHasAMealWithRecipesCreated()
        {
            using var ctx = BDDSetup.CreateContext();
            _userId = ctx.Users.First(u => u.NormalizedEmail == "JACK@FAKEEMAIL.COM").Id;

            var meal = new Meal
            {
                UserId = _userId,
                Title = "Test Meal With Recipes",
                StartTime = DateTime.Now
            };

            var recipes = ctx.Set<Recipe>().Where(r => new[] { -1, -2, -3, -4 }.Contains(r.Id)).ToList();
            foreach (var recipe in recipes)
            {
                meal.Recipes.Add(recipe);
            }

            ctx.Meals.Add(meal);
            ctx.SaveChanges();
            _mealId = meal.Id;
            _scenarioContext["MealId"] = _mealId;
        }

        [Given("'Jack' is on the view meal page")]
        public void GivenJackIsOnTheViewMealPage()
        {
            if (_scenarioContext.ContainsKey("MealId"))
                _mealId = (int)_scenarioContext["MealId"];

            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/ViewMeal?id={_mealId}");
            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")
                    ?.ToString() == "complete");
            _scenarioContext["CurrentPage"] = "ViewMeal";
        }

        [Given("'Jack' is on the create meal page")]
        public void GivenJackIsOnTheCreateMealPage()
        {
            try { _wait.Until(d => !d.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase)); }
            catch (WebDriverTimeoutException) { }
            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")
                    ?.ToString() == "complete");
            _scenarioContext["CurrentPage"] = "CreateMeal";
        }

        [Given("'Jack' is on the edit meal page")]
        public void GivenJackIsOnTheEditMealPage()
        {
            if (_scenarioContext.ContainsKey("MealId"))
                _mealId = (int)_scenarioContext["MealId"];

            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/EditMeal?id={_mealId}");
            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")
                    ?.ToString() == "complete");
            _scenarioContext["CurrentPage"] = "EditMeal";
        }

        [When("'Jack' clicks the delete button on a recipe")]
        public void WhenJackClicksTheDeleteButtonOnARecipe()
        {
            var firstItem = _wait.Until(driver =>
            {
                try
                {
                    var el = driver.FindElement(By.CssSelector(".mealRecipeItem"));
                    return el.Displayed ? el : null;
                }
                catch (NoSuchElementException) { return null; }
            })!;

            _deletedRecipeName = firstItem.FindElement(By.CssSelector("h4")).Text;
            _scenarioContext["DeleteBtn"] = firstItem.FindElement(By.CssSelector(".delete-recipe-btn"));
        }

        [When("'Jack' confirms the deletion")]
        public void WhenJackConfirmsTheDeletion()
        {
            var btn = (IWebElement)_scenarioContext["DeleteBtn"];
            btn.Click();
            var confirmBtn = _wait.Until(d => d.FindElement(By.CssSelector(".inline-confirm-yes")));
            confirmBtn.Click();
            Thread.Sleep(600);
        }

        [When("'Jack' denies the deletion")]
        public void WhenJackDeniesTheDeletion()
        {
            var btn = (IWebElement)_scenarioContext["DeleteBtn"];
            btn.Click();
            var cancelBtn = _wait.Until(d => d.FindElement(By.CssSelector(".inline-confirm-no")));
            cancelBtn.Click();
        }

        [Then("the recipe is removed from the meal immediately")]
        public void ThenTheRecipeIsRemovedFromTheMealImmediately()
        {
            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").ToString() == "complete");

            var page = _scenarioContext.ContainsKey("CurrentPage") ? _scenarioContext["CurrentPage"]?.ToString() : "";

            if (page == "ViewMeal" && _mealId > 0)
            {
                // ViewMeal deletes immediately via AJAX — navigate back to confirm DB removal
                _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/ViewMeal?id={_mealId}");
                _wait.Until(driver =>
                    ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").ToString() == "complete");
            }
            // EditMeal and CreateMeal remove from DOM immediately; DB is updated on Save Changes — check DOM directly

            _wait.Until(driver =>
            {
                var items = driver.FindElements(By.CssSelector(".mealRecipeItem h4"));
                return items.All(item => !item.Text.Contains(_deletedRecipeName));
            });
        }

        [Then("the recipe is still shown in the meal recipe list")]
        public void ThenTheRecipeIsStillShownInTheMealRecipeList()
        {
            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").ToString() == "complete");

            var page = _scenarioContext.ContainsKey("CurrentPage") ? _scenarioContext["CurrentPage"]?.ToString() : "";

            if (page == "ViewMeal" && _mealId > 0)
            {
                _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/ViewMeal?id={_mealId}");
                _wait.Until(driver =>
                    ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").ToString() == "complete");
            }
            else if (page == "EditMeal" && _mealId > 0)
            {
                _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/EditMeal?id={_mealId}");
                _wait.Until(driver =>
                    ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").ToString() == "complete");
            }

            _wait.Until(driver =>
            {
                var items = driver.FindElements(By.CssSelector(".mealRecipeItem h4"));
                return items.Any(item => item.Text.Contains(_deletedRecipeName));
            });
        }

        [Given("'Jack' searches for a recipe {string}")]
        [When("'Jack' searches for a recipe {string}")]
        public void GivenJackSearchesForARecipe(string searchTerm)
        {
            var searchInput = _wait.Until(driver =>
            {
                try
                {
                    var el = driver.FindElement(By.CssSelector("#searchText"));
                    return (el.Displayed && el.Enabled) ? el : null;
                }
                catch (NoSuchElementException) { return null; }
            })!;

            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("arguments[0].scrollIntoView(true);", searchInput);
            searchInput.Click();
            searchInput.Clear();
            searchInput.SendKeys(searchTerm);
            Thread.Sleep(1100); // Use a wait!!
        }

        [Given("'Jack' clicks the first search result")]
        [When("'Jack' clicks the first search result")]
        public void GivenJackClicksTheFirstSearchResult()
        {
            var firstResult = _wait.Until(driver => driver.FindElement(By.CssSelector(".recipeSearchRow")));

            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("window.alert = function(msg) { window._alertMessage = msg; }; window._alertMessage = null;");
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("arguments[0].click();", firstResult);

            // On the create meal page recipes are multi-select pending until committed.
            // If the click selected a recipe (not a duplicate alert), commit it so subsequent
            // steps can find it in #createMealList. Use a short timeout so duplicate-click
            // scenarios (where no row gets selected and the button stays hidden) don't stall.
            var page = _scenarioContext.ContainsKey("CurrentPage") ? _scenarioContext["CurrentPage"]?.ToString() : "";
            if (page == "CreateMeal")
            {
                try
                {
                    var addBtn = new WebDriverWait(_driver, TimeSpan.FromSeconds(2)).Until(d =>
                    {
                        var btn = d.FindElement(By.Id("addSelectedRecipesBtn"));
                        return btn.Displayed ? btn : null;
                    });
                    addBtn?.Click();
                    _wait.Until(d => d.FindElements(By.CssSelector("#createMealList .delete-recipe-btn")).Count > 0);
                }
                catch (WebDriverTimeoutException) { }
            }
        }
    }
}
