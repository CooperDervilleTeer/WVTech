using MealPlanner.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests
{

    [Binding]
    public class WVT127Steps
    {
        private IWebDriver _driver = null!;
        private string _baseUrl = null!;
        private WebDriverWait _wait = null!;
        private IWebElement _titleInput = null!;
        private int _mealId;
        private string _updatedTitle = null!;
        private string _addedRecipeName = null!;
        private string _userId = null!;
        private readonly ScenarioContext _scenarioContext;

        public WVT127Steps(ScenarioContext scenarioContext)
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

        [Given("'Jack' has a meal created")]
        public void GivenJackHasAMealCreated()
        {
            using var ctx = BDDSetup.CreateContext();
            _userId = ctx.Users.First(u => u.NormalizedEmail == "JACK@FAKEEMAIL.COM").Id;
            _mealId = CreateTestMeal(_userId);
            _scenarioContext["MealId"] = _mealId;
        }

        [Given("the edit meal page is open")]
        public void GivenTheEditMealPageIsOpen()
        {
            NavigateToEditMealPage(_mealId);
        }

        [When("'Jack' clicks the edit button")]
        public void WhenJackClicksTheEditButton()
        {
            var editButton = _driver.FindElement(By.CssSelector("button[type='submit']"));
            editButton.Click();
        }

        [Then("the meal edit form is shown")]
        public void ThenTheMealEditFormIsShown()
        {
            _wait.Until(d =>d.FindElement(By.CssSelector("#editMealForm")));
            Assert.Pass("Edit meal form loaded");
        }

        // When

        [When("User updates the meal title")]
        public void WhenUserUpdatesTheMealTitle()
        {
            _updatedTitle = "New Meal Title " + DateTime.Now.Ticks;
            _titleInput.Click();
            _titleInput.Clear();
            _titleInput.SendKeys(_updatedTitle);
        }

       [When("User saves the meal")]
        public void WhenUserSavesTheMeal()
        {
            var saveButton = _driver.FindElement(By.Id("saveMealBtn"));

            // Navbar does weird stuff with this button in headless, forcing the click with JS
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", saveButton);

            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")
                    .ToString() == "complete");
        }

        [When("User searches for a recipe {string}")]
        public void WhenUserSearchesForARecipe(string searchTerm)
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
        }

        [When("User clicks the first search result")]
        public void WhenUserClicksTheFirstSearchResult()
        {
            var firstResult = _wait.Until(driver =>
            {
                try
                {
                    var el = driver.FindElement(By.CssSelector(".recipeSearchRow"));
                    return el.Displayed ? el : null;
                }
                catch (NoSuchElementException) { return null; }
            })!;

            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("arguments[0].scrollIntoView(true);", firstResult);
            _addedRecipeName = firstResult.FindElement(By.CssSelector(".recipeName")).Text;
            firstResult.Click();

            // Recipe search uses multi-select — click "Add selected" to commit the selection
            // to #mealRecipeList so subsequent steps can find it.
            var addBtn = _wait.Until(d =>
            {
                var btn = d.FindElement(By.Id("addSelectedRecipesBtn"));
                return btn.Displayed ? btn : null;
            });
            addBtn!.Click();
        }

        // Then

        [Then("the updated meal title is shown immediately")]
        public void ThenTheUpdatedMealTitleIsShownImmediately()
        {
            _wait.Until(driver =>
            {
                try
                {
                    var el = driver.FindElement(By.CssSelector("#mealTitle"));
                    return (el.Displayed && el.GetAttribute("value") == _updatedTitle) ? el : null;
                }
                catch (NoSuchElementException) { return null; }
            });
        }

        [Then("the meal is saved with the updated title")]
        public void ThenTheMealIsSavedWithTheUpdatedTitle()
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/EditMeal?id={_mealId}");
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
            wait.Until(driver =>
            {
                try
                {
                    var el = driver.FindElement(By.CssSelector("#mealTitle"));
                    return el.GetAttribute("value") == _updatedTitle;
                }
                catch (NoSuchElementException) { return false; }
            });
        }

        [Then("the recipe is shown in the meal recipe list")]
        public void ThenTheRecipeIsShownInTheMealRecipeList()
        {
            _wait.Until(driver =>
            {
                var items = driver.FindElements(By.CssSelector("#mealRecipeList .mealRecipeItem"));
                return items.Any(item => item.Text.Contains(_addedRecipeName));
            });
        }

        [Then("the recipe is saved with the meal")]
        public void ThenTheRecipeIsSavedWithTheMeal()
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
            wait.Until(driver =>
            {
                try
                {
                    var items = driver.FindElements(By.CssSelector(".mealRecipeItem h4"));
                    return items.Any(item =>
                    {
                        try { return item.Text.Contains(_addedRecipeName); }
                        catch (StaleElementReferenceException) { return false; }
                    });
                }
                catch (StaleElementReferenceException) { return false; }
            });
        }

        // This step definition uses Cucumber Expressions. See https://github.com/gasparnagy/CucumberExpressions.SpecFlow
        [Given("he is on the meal page")]
        public void GivenHeIsOnTheMealPage()
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/ViewMeal/{_mealId}");
        }

        // Helpers

        private void NavigateToEditMealPage(int mealId)
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/EditMeal?id={mealId}");

            _wait.Until(driver =>
                ((IJavaScriptExecutor)driver)
                    .ExecuteScript("return document.readyState")
                    .ToString() == "complete");

            _titleInput = _wait.Until(driver =>
            {
                try
                {
                    var el = driver.FindElement(By.CssSelector("#mealTitle"));
                    return (el.Displayed && el.Enabled) ? el : null;
                }
                catch (NoSuchElementException) { return null; }
            })!;

            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("arguments[0].scrollIntoView(true);", _titleInput);
        }

        private static int CreateTestMeal(string userId)
        {
            
            using var ctx = BDDSetup.CreateContext();
            var meal = new Meal
            {
                UserId = userId,
                Title = "Test Meal",
                StartTime = DateTime.Now
            };
            ctx.Meals.Add(meal);
            ctx.SaveChanges();
            return meal.Id;
        }
    }
}