using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MealPlanner.ViewModels;
using MealPlanner.Models;
using MealPlanner.DAL.Abstract;
using MealPlanner.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;

namespace MealPlanner.Controllers;

public class FoodEntriesController : Controller
{
    private readonly MealPlannerDBContext _context;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IUserRecipeRepository _userRecipeRepository;
    private readonly INutritionProgressService? _nutritionProgressService;
    private readonly IRegistrationService _registrationService;
    private readonly IExternalRecipeService? _externalRecipeService;
    private readonly BlobContainerClient? _blobContainer;
    private readonly IWebHostEnvironment? _env;
    private readonly IShoppingListService? _shoppingListService;

    public FoodEntriesController(
        IRecipeRepository recipeRepository,
        ITagRepository tagRepository,
        IUserRecipeRepository userRecipeRepository,
        MealPlannerDBContext context,
        IRegistrationService registrationService,
        IWebHostEnvironment env,
        BlobContainerClient? blobContainer = null,
        IExternalRecipeService? externalRecipeService = null,
        INutritionProgressService? nutritionProgressService = null,
        IShoppingListService? shoppingListService = null)
    {
        _recipeRepository = recipeRepository;
        _tagRepository = tagRepository;
        _context = context;
        _registrationService = registrationService;
        _nutritionProgressService = nutritionProgressService;
        _userRecipeRepository = userRecipeRepository;
        _externalRecipeService = externalRecipeService;
        _blobContainer = blobContainer;
        _env = env;
        _shoppingListService = shoppingListService;
    }

    public IActionResult SearchRecipes()
    {
        return View();
    }

    public IActionResult SelectType()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> NutritionSummary(string tab = "weekly")
    {
        if (_nutritionProgressService is null)
            return StatusCode(500, "NutritionProgressService not configured.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var startDay = today.AddDays(-29);
        var progress = await _nutritionProgressService.GetRangeProgressAsync(userId, startDay, today);
        var allDays   = await _nutritionProgressService.GetDailyBreakdownAsync(userId, startDay, today);

        var todayData = allDays.FirstOrDefault(d => d.Day == today);
        var todayBar = new NutritionBarInfoViewModel(
            todayData?.Calories ?? 0, progress.Targets.Calories,
            todayData?.Protein  ?? 0, progress.Targets.Protein,
            todayData?.Fat      ?? 0, progress.Targets.Fat,
            todayData?.Carbs    ?? 0, progress.Targets.Carbs);

        return View(new NutritionSummaryViewModel
        {
            ActiveTab    = tab,
            DailyTargets = progress.Targets,
            AllDays      = allDays,
            TodayBar     = todayBar
        });
    }




    [Route("/FoodEntries/Recipes")]
    public async Task<IActionResult> Recipes()
    {
        User? user = await _registrationService.FindUserByClaimAsync(User);
        IEnumerable<Recipe> userRecipes = [];
        if (user != null)
        {
            userRecipes = await _userRecipeRepository.GetUserOwnedRecipesByUserIdAsync(user.Id);
        }

        var recipeVMs = userRecipes.Select(ViewModelService.RecipeToRecipeVM).ToList();
        foreach (RecipeViewModel vm in recipeVMs)
        {
            if (vm.Id == null) continue;
            vm.VotePercentage = await _userRecipeRepository.GetRecipeVotePercentage(vm.Id ?? 0);
        }

        return View(new RecipesAndShoppingViewModel
        {
            Recipes = recipeVMs,
        });
    }

    [HttpGet]
    [Route("/FoodEntries/Recipes/{id}")]
    public async Task<IActionResult> Recipes(int id)
    {
        Recipe? recipe = await _recipeRepository.ReadRecipeWithIngredientsAsync(id);
        
        // Get info for external recipe
        if (!recipe?.ExternalUri.IsNullOrEmpty() ?? false && _externalRecipeService != null)
        {
            try
            {
                recipe = await _externalRecipeService.GetExternalRecipeByURI(recipe.ExternalUri!);
                recipe.Id = id;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        // Change to not-found view!
        if (recipe == null)
        {
            return RedirectToAction("SearchRecipes");
        }
        RecipeViewModel viewModel = ViewModelService.RecipeToRecipeVM(recipe);
        viewModel.VotePercentage = await _userRecipeRepository.GetRecipeVotePercentage(id);
        
        //if you do not own the recipe
        User? user = await _registrationService.FindUserByClaimAsync(User);
        if (user != null)
        {
            viewModel.UserVote = await _userRecipeRepository.GetUserRecipeVoteAsync(user.Id, id);
            var ownedRecipes = await _userRecipeRepository.GetUserOwnedRecipesByUserIdAsync(user.Id);
            viewModel.IsOwned = ownedRecipes.Any(r => r.Id == id);
        }
        
        return View("SingleRecipe", viewModel);
    }

    public async Task<IActionResult> AddNewRecipe()
    {
        return View(new RecipeViewModel { AvailableTags = await _tagRepository.GetTagNamesAsync() });
    }

    [HttpPost]
    public async Task<IActionResult> RecipeAdded(RecipeViewModel newRecipeViewModel)
    {
        //error checking
        if (!ModelState.IsValid)
        {
            newRecipeViewModel.AvailableTags = await _tagRepository.GetTagNamesAsync();
            return View("AddNewRecipe", newRecipeViewModel);
        }

        if (newRecipeViewModel.Ingredients.Any(i => string.IsNullOrWhiteSpace(i)))
        {
            ModelState.AddModelError("Ingredients", "Ingredient names cannot be blank.");
            newRecipeViewModel.AvailableTags = await _tagRepository.GetTagNamesAsync();
            return View("AddNewRecipe", newRecipeViewModel);
        }

        Recipe recipe = ViewModelService.RecipeFromRecipeVM(newRecipeViewModel);
        if (_blobContainer != null)
            await recipe.SaveImageAsync(newRecipeViewModel.ImageFile, _blobContainer);
        else if (_env != null)
            await recipe.SaveImageAsync(newRecipeViewModel.ImageFile, _env.WebRootPath);
        _recipeRepository.CreateOrUpdate(recipe);

        User? user = await _registrationService.FindUserByClaimAsync(User);
        if (user != null)
        {
            UserRecipe userRecipe = new UserRecipe { User = user, Recipe = recipe, UserOwner = true, UserVote = UserVoteType.UpVote };
            _userRecipeRepository.CreateOrUpdate(userRecipe);
        }

        _context.SaveChanges();

        return RedirectToAction("Recipes");
    }

    [HttpGet]
    [Route("/FoodEntries/EditRecipe/{id}")]
    public async Task<IActionResult> EditRecipe(int id)
    {
        Recipe? recipe = await _recipeRepository.ReadRecipeWithIngredientsAsync(id);

        // Change to not-found view!
        if (recipe == null)
        {
            return RedirectToAction("Recipes");
        }

        //if you do not own the recipe redirect
        User? user = await _registrationService.FindUserByClaimAsync(User);
        if (user != null)
        {
            var ownedRecipes = await _userRecipeRepository.GetUserOwnedRecipesByUserIdAsync(user.Id);
            if(ownedRecipes.Any(r => r.Id == id) == false)
            {
                return RedirectToAction("Recipes");
            }
        }

        RecipeViewModel viewModel = ViewModelService.RecipeToRecipeVM(recipe);
        viewModel.AvailableTags = await _tagRepository.GetTagNamesAsync();
        return View("EditRecipe", viewModel);
    }

    [HttpPost]
    [Route("/FoodEntries/EditRecipe/{id}")]
    public async Task<IActionResult> RecipeEditFinished(RecipeViewModel editedRecipeViewModel, int id)
    {
        if (!ModelState.IsValid)
        {
            editedRecipeViewModel.AvailableTags = await _tagRepository.GetTagNamesAsync();
            return View("EditRecipe", editedRecipeViewModel);
        }

        //if you do not own the recipe redirect
        User? user = await _registrationService.FindUserByClaimAsync(User);
        if (user != null)
        {
            var ownedRecipes = await _userRecipeRepository.GetUserOwnedRecipesByUserIdAsync(user.Id);
            if(ownedRecipes.Any(r => r.Id == id) == false)
            {
                return RedirectToAction("Recipes");
            }
        }

        //gets the existing recipe by id
        Recipe? existing = await _recipeRepository.ReadRecipeWithIngredientsAsync(id);
        if (existing == null)
        {
            return RedirectToAction("SearchRecipes");
        }

        if (editedRecipeViewModel.RemoveImage || editedRecipeViewModel.ImageFile != null)
            await Recipe.DeleteImageAsync(existing.ImageUrl, _blobContainer, _env?.WebRootPath);

        if (editedRecipeViewModel.RemoveImage)
            editedRecipeViewModel.ImageUrl = null;

        Recipe updated = ViewModelService.EditRecipeVMToModel(existing, editedRecipeViewModel);

        if (!editedRecipeViewModel.RemoveImage && editedRecipeViewModel.ImageFile != null)
        {
            if (_blobContainer != null)
                await updated.SaveImageAsync(editedRecipeViewModel.ImageFile, _blobContainer);
            else if (_env != null)
                await updated.SaveImageAsync(editedRecipeViewModel.ImageFile, _env.WebRootPath);
        }

        //updates the databse with the new and improved existing
        _recipeRepository.CreateOrUpdate(updated);
        _context.SaveChanges();

        return RedirectToAction("Recipes");
    }

  [HttpPost]
[Authorize]
[IgnoreAntiforgeryToken]
public async Task<IActionResult> DeleteRecipe(int id)
{
    User? user = await _registrationService.FindUserByClaimAsync(User);
    if (user == null) return Challenge();

    var ownedRecipes = await _userRecipeRepository.GetUserOwnedRecipesByUserIdAsync(user.Id);
    if (!ownedRecipes.Any(r => r.Id == id))
        return Forbid();

    var recipe = await _recipeRepository.ReadRecipeWithIngredientsAsync(id);
    if (recipe == null) return NotFound();

    await Recipe.DeleteImageAsync(recipe.ImageUrl, _blobContainer, _env?.WebRootPath);

    // Remove ingredients first
    _context.Set<Ingredient>().RemoveRange(recipe.Ingredients);

    // Remove user recipes
    var userRecipes = _context.Set<UserRecipe>().Where(ur => ur.RecipeId == id);
    _context.Set<UserRecipe>().RemoveRange(userRecipes);

    _context.Recipes.Remove(recipe);
    await _context.SaveChangesAsync();

    return Ok();
}

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}