using MealPlanner.DAL.Abstract;
using MealPlanner.Helpers;
using MealPlanner.Models;
using MealPlanner.Services;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanner.Controllers;

[Authorize]
public class MealController : Controller
{
    private readonly IRegistrationService _registrationService;
    private readonly IRecipeRepository _recipeRepo;
    private readonly IMealRepository _mealRepo;
    private readonly MealPlannerDBContext _context;
    private readonly ITagRepository _tagRepo;
    private readonly IMealRecommendationService? _recommendationService;
    private readonly IPantryService? _pantryService;
    private readonly IExternalRecipeService? _externalRecipeService;

    public MealController(
        IRegistrationService registrationService,
        IRecipeRepository recipeRepo,
        IMealRepository mealRepo,
        MealPlannerDBContext context,
        ITagRepository tagRepo,
        IMealRecommendationService? mealRecommendationService = null,
        IPantryService? pantryService = null,
        IExternalRecipeService? externalRecipeService = null)
    {
        _registrationService = registrationService;
        _recipeRepo = recipeRepo;
        _mealRepo = mealRepo;
        _context = context;
        _tagRepo = tagRepo;
        _recommendationService = mealRecommendationService;
        _pantryService = pantryService;
        _externalRecipeService = externalRecipeService;
    }

    [HttpGet]
    public async Task<IActionResult> SelectMeal(string? date = null)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        DateTime selectedDate =
            DateTime.TryParse(date, out var parsed)
                ? parsed.Date
                : DateTime.Today;

        var meals = await _mealRepo.GetDistinctUserMealsAsync(user);

        var vm = new SelectMealViewModel
        {
            SelectedDate = selectedDate,
            Meals = meals.ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMealToDay(int mealId, string? date = null)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        var source = await _mealRepo.ReadAsync(mealId);
        if (source == null || source.UserId != user.Id) return NotFound();

        await _mealRepo.LoadRecipesAsync(source);

        DateTime selectedDate =
            DateTime.TryParse(date, out var parsed)
                ? parsed.Date
                : DateTime.Today;

        var clone = new Meal
        {
            User = user,
            UserId = user.Id,
            Title = source.Title,
            StartTime = selectedDate,
            RepeatRule = source.RepeatRule,
            Recipes = source.Recipes.ToList()
        };

        _mealRepo.CreateOrUpdate(clone);
        _context.SaveChanges();

        Response.Cookies.Delete("ShoppingListSynced");
        return RedirectToAction("Index", "Home", new { date = selectedDate.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> PlannerHome(string? date)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        DateTime selectedDate =
            DateTime.TryParse(date, out var parsed)
                ? parsed.Date
                : DateTime.Today;

        var meals = await _mealRepo.GetUserMealsByDateAsync(user, selectedDate);

        var vm = new PlannerHomeViewModel
        {
            SelectedDate = selectedDate,
            Meals = meals
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> NewMeal(string? date)
    {
        ViewBag.AvailableTags = await _tagRepo.GetTagsByPopularityAsync();
        var selected = DateTime.TryParse(date, out var parsed) ? parsed : DateTime.Today;
        return View(new CreateMealViewModel
        {
            SelectedMonth = selected.Month,
            SelectedDay = selected.Day
        });
    }

    [HttpPost]
    public async Task<IActionResult> NewMeal(CreateMealViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var year = DateTime.Today.Year;
        DateTime selectedDate;

        try
        {
            selectedDate = new DateTime(year, model.SelectedMonth, model.SelectedDay);
        }
        catch
        {
            ModelState.AddModelError("", "Please select a valid month and day.");
            return View(model);
        }

        Meal newMeal = new Meal
        {
            User = user,
            UserId = user.Id,
            Title = model.Title.Trim(),
            StartTime = selectedDate,
            RepeatRule = model.RepeatWeekly ? "Weekly" : null,
            RepeatDays = model.RepeatWeekly ? MealSchedule.EncodeRepeatDays(model.RepeatDays) : null
        };

        foreach (int id in model.RecipeIds)
        {
            var recipe = _recipeRepo.Read(id);
            if (recipe != null)
            {
                newMeal.Recipes.Add(recipe);
            }
        }

        _mealRepo.CreateOrUpdate(newMeal);
        _context.SaveChanges();

        Response.Cookies.Delete("ShoppingListSynced");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> GenerateMeal(CreateMealViewModel model)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();
        if (_recommendationService == null) return Problem(statusCode:500);

        var selectedDate = new DateTime(DateTime.Today.Year, model.SelectedMonth, model.SelectedDay);

        var preference = new MealPreferenceViewModel
        {
            Title = model.Title.Trim(),
            Size = model.Size,
            TagIds = model.TagIds,
            CustomTagName = model.CustomTagName
        };
        await ResolveCustomTagNamesAsync([preference]);

        var config = new DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = model.SelectedMonth,
            SelectedDay = model.SelectedDay,
            MealPreferences = [preference]
        };

        var meals = await _recommendationService.GetRecommendedMealsForUser(user, selectedDate, config);
        var newMeal = meals.FirstOrDefault();
        if (newMeal == null || newMeal.Recipes.IsNullOrEmpty()) return NotFound();

        newMeal.IsGenerated = true;
        newMeal = _mealRepo.CreateOrUpdate(newMeal);
        _context.SaveChanges();
        Response.Cookies.Delete("ShoppingListSynced");
        return RedirectToAction("ViewMeal", new { id = newMeal.Id });
    }

    [HttpPost]
    public async Task<IActionResult> GenerateDayPlan(DayPlanConfigViewModel model)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();
        if (_recommendationService == null) return Problem(statusCode: 500);

        await ResolveCustomTagNamesAsync(model.MealPreferences);

        var selectedDate = new DateTime(DateTime.Today.Year, model.SelectedMonth, model.SelectedDay);
        var meals = await _recommendationService.GetRecommendedMealsForUser(user, selectedDate, model);

        foreach (var meal in meals)
        {
            meal.IsGenerated = true;
            _mealRepo.CreateOrUpdate(meal);
        }
        _context.SaveChanges();

        TempData["GeneratedMealIds"] = string.Join(",", meals.Select(m => m.Id));

        return RedirectToAction("DayPlanSummary", new { date = selectedDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> RegenerateMeal(int mealId, MealPreferenceViewModel preferences)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();
        if (_recommendationService == null) return Problem(statusCode: 500);

        var meal = await _mealRepo.ReadAsync(mealId);
        if (meal == null || meal.UserId != user.Id) return NotFound();

        var mealDate = meal.StartTime?.Date ?? DateTime.Today;
        await ResolveCustomTagNamesAsync([preferences]);
        var config = new ViewModels.DayPlanConfigViewModel
        {
            MealCount = 1,
            SelectedMonth = mealDate.Month,
            SelectedDay = mealDate.Day,
            MealPreferences = [preferences]
        };

        var newMeals = await _recommendationService.GetRecommendedMealsForUser(user, mealDate, config, excludeMealId: mealId);
        await _mealRepo.LoadRecipesAsync(meal);
        meal.Recipes = newMeals.FirstOrDefault()?.Recipes ?? [];
        _mealRepo.CreateOrUpdate(meal);
        _context.SaveChanges();

        return Json(new
        {
            mealId = meal.Id,
            title = meal.Title,
            recipes = meal.Recipes.Select(r => new { r.Id, r.Name, r.Calories })
        });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RegenerateRecipe(int mealId, int recipeId)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();
        if (_recommendationService == null) return Problem(statusCode: 500);

        var meal = await _mealRepo.ReadAsync(mealId);
        if (meal == null || meal.UserId != user.Id) return NotFound();

        await _mealRepo.LoadRecipesAsync(meal);
        var mealDate = meal.StartTime?.Date ?? DateTime.Today;
        var dayIds = await _mealRepo.GetUserRecipeIdsForDateAsync(user, mealDate);
        var excludeIds = dayIds.Union(meal.Recipes.Select(r => r.Id)).ToHashSet();

        var slotTemplate = await _recipeRepo.ReadRecipeWithIngredientsAsync(recipeId);
        var replacement = await _recommendationService.GetOneRecipeRecommendation(user, mealDate, excludeIds, slotTemplate);
        if (replacement == null)
            return Json(new { noAlternative = true });

        var replaced = meal.Recipes.FirstOrDefault(r => r.Id == recipeId);
        if (replaced == null) return NotFound();

        meal.Recipes.Remove(replaced);
        meal.Recipes.Add(replacement);
        _mealRepo.CreateOrUpdate(meal);
        _context.SaveChanges();

        return Json(new
        {
            newRecipe = new { replacement.Id, replacement.Name, replacement.Calories },
            replacedRecipeId = recipeId
        });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SwapRecipe(int mealId, int removeRecipeId, int addRecipeId)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        var meal = await _mealRepo.ReadAsync(mealId);
        if (meal == null || meal.UserId != user.Id) return NotFound();

        await _mealRepo.LoadRecipesAsync(meal);

        var toRemove = meal.Recipes.FirstOrDefault(r => r.Id == removeRecipeId);
        if (toRemove != null) meal.Recipes.Remove(toRemove);

        var toAdd = _recipeRepo.Read(addRecipeId);
        if (toAdd != null) meal.Recipes.Add(toAdd);

        _mealRepo.CreateOrUpdate(meal);
        _context.SaveChanges();

        return Json(new { restoredRecipe = toAdd != null ? new { toAdd.Id, toAdd.Name, toAdd.Calories } : null });
    }

    [HttpGet]
    public async Task<IActionResult> DayPlanSummary(string? date)
    {
        var generatedIdsRaw = TempData["GeneratedMealIds"] as string;
        if (string.IsNullOrEmpty(generatedIdsRaw))
            return RedirectToAction("Index", "Home");

        var generatedIds = generatedIdsRaw.Split(',').Select(int.Parse).ToList();

        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        DateTime selectedDate = DateTime.TryParse(date, out var parsed) ? parsed.Date : DateTime.Today;
        var meals = await _mealRepo.GetMealsByIdsAsync(generatedIds);
        await meals.LoadExternalRecipesAsync(_externalRecipeService);

        var vm = new DayPlanSummaryViewModel
        {
            Date = selectedDate,
            AvailableTags = await _tagRepo.GetTagsByPopularityAsync(),
            Meals = meals.Select(m => new DayPlanMealViewModel
            {
                MealId = m.Id,
                Title = m.Title ?? string.Empty,
                Recipes = m.Recipes
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ViewMeal(int id)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var meal = await _mealRepo.ReadAsync(id);
        if (meal == null || meal.UserId != user.Id)
        {
            return NotFound();
        }

        await _mealRepo.LoadRecipesAsync(meal);

        return View(meal);
    }

    [HttpGet]
    public async Task<IActionResult> EditMeal(int id, string? returnUrl = null)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var meal = await _mealRepo.ReadAsync(id);
        if (meal == null || meal.UserId != user.Id)
        {
            return NotFound();
        }

        await _mealRepo.LoadRecipesAsync(meal);

        ViewBag.ReturnUrl = returnUrl;

        var viewModel = new EditMealViewModel
        {
            Id = meal.Id,
            Title = meal.Title,
            Date = meal.StartTime ?? DateTime.Today,
            Time = meal.StartTime.HasValue
                ? meal.StartTime.Value.ToString("HH:mm")
                : "00:00",
            SelectedMonth = meal.StartTime?.Month ?? DateTime.Today.Month,
            SelectedDay = meal.StartTime?.Day ?? DateTime.Today.Day,
            RepeatWeekly = meal.RepeatRule == "Weekly",
            RepeatDays = MealSchedule.ParseRepeatDays(meal.RepeatDays).ToList(),
            RecipeIds = meal.Recipes?.Select(r => r.Id).ToList() ?? new List<int>(),
            Recipes = meal.Recipes?.Select(r => new RecipeDisplayViewModel
            {
                Id = r.Id,
                Name = r.Name
            }).ToList() ?? new List<RecipeDisplayViewModel>()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> EditMeal(EditMealViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
            return Challenge();

        var meal = _mealRepo.Read(model.Id);

        if (meal == null || meal.UserId != user.Id)
            return NotFound();

        await _mealRepo.LoadRecipesAsync(meal);

        
        DateTime selectedDate = new DateTime(
            DateTime.Today.Year, 
            model.SelectedMonth, 
            model.SelectedDay);

        meal.Title = model.Title.Trim();
        meal.StartTime = selectedDate;
        meal.RepeatRule = model.RepeatWeekly ? "Weekly" : null;
        meal.RepeatDays = model.RepeatWeekly ? MealSchedule.EncodeRepeatDays(model.RepeatDays) : null;

        var incomingRecipeIds = model.RecipeIds
            .Distinct()
            .ToList();

        meal.Recipes.RemoveAll(r => !incomingRecipeIds.Contains(r.Id));

        foreach (var recipeId in incomingRecipeIds)
        {
            if (!meal.Recipes.Any(r => r.Id == recipeId))
            {
                var recipe = await _context.Recipes.FindAsync(recipeId);
                if (recipe != null)
                {
                    meal.Recipes.Add(recipe);
                }
            }
        }

        await _context.SaveChangesAsync();

        Response.Cookies.Delete("ShoppingListSynced");

        TempData["SuccessMessage"] = "Meal updated successfully";
        return RedirectToAction("ViewMeal", new { id = model.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AddRecipeToMeal(int mealId, int recipeId)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        var meal = await _mealRepo.ReadAsync(mealId);
        if (meal == null || meal.UserId != user.Id) return NotFound();

        await _mealRepo.LoadRecipesAsync(meal);

        return RedirectToAction("EditMeal", new { id = meal.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMeal(int id, string? date, string? source, bool deleteAll = false)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var meal = await _context.Meals.FindAsync(id);
        if (meal == null)
        {
            return source == "home"
                ? RedirectToAction("Index", "Home", new { date })
                : RedirectToAction("PlannerHome", "Meal", new { date });
        }

        if (meal.UserId != user.Id)
        {
            return Forbid();
        }

        if (meal.RepeatRule == "Weekly" && deleteAll)
        {
            _context.Meals.Remove(meal);
        }
        else
        {
            var exclusionDate = DateTime.TryParse(date, out var pd) ? pd.Date : DateTime.Today;
            var alreadyExcluded = await _context.MealExclusions
                .FindAsync(meal.Id, exclusionDate);
            if (alreadyExcluded == null)
            {
                _context.MealExclusions.Add(new MealExclusion { MealId = meal.Id, ExclusionDate = exclusionDate });
            }
        }

        await _context.SaveChangesAsync();

        Response.Cookies.Delete("ShoppingListSynced");
        return source == "home"
            ? RedirectToAction("Index", "Home", new { date })
            : RedirectToAction("PlannerHome", "Meal", new { date });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMealTemplate(int mealId, string? date)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        var meal = await _mealRepo.ReadAsync(mealId);
        if (meal == null || meal.UserId != user.Id) return NotFound();

        await _mealRepo.RemoveAllMealsWithSameTitleAsync(user.Id, meal.Title ?? "");

        return RedirectToAction("SelectMeal", new { date });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteRecipeFromMeal(int mealId, int recipeId)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null) return Challenge();

        var meal = await _context.Meals
            .Include(m => m.Recipes)
            .FirstOrDefaultAsync(m => m.Id == mealId);

        if (meal == null || meal.UserId != user.Id) return NotFound();

        var recipe = meal.Recipes.FirstOrDefault(r => r.Id == recipeId);
        if (recipe != null)
        {
            meal.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMealCompleted(int id, string? date, bool? isCompleted, string? source, bool? removePantry, bool? restorePantry)
    {
        var user = await _registrationService.FindUserByClaimAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var meal = await _mealRepo.ReadWithIngredientsAsync(id);
        if (meal == null)
        {
            return NotFound();
        }

        if (meal.UserId != user.Id)
        {
            return Forbid();
        }

        var completionDate = DateTime.TryParse(date, out var parsedDate) ? parsedDate.Date : DateTime.Today;
        var existing = await _context.MealCompletions
            .FindAsync(meal.Id, completionDate);

        if (isCompleted == true && existing == null)
        {
            _context.MealCompletions.Add(new MealCompletion { MealId = meal.Id, CompletionDate = completionDate });

            if (removePantry == true && _pantryService != null)
                _pantryService.AutoRemovePantryItems(user.Id, meal.Id, completionDate, [meal]);
        }
        else if (isCompleted != true && existing != null)
        {
            _context.MealCompletions.Remove(existing);

            if (restorePantry == true && _pantryService != null)
                _pantryService.RestorePantryItems(user.Id, meal.Id, completionDate);
        }

        await _context.SaveChangesAsync();

        return source == "home"
            ? RedirectToAction("Index", "Home", new { date })
            : RedirectToAction("PlannerHome", "Meal", new { date });
    }

    private async Task ResolveCustomTagNamesAsync(IEnumerable<MealPreferenceViewModel> preferences)
    {
        foreach (var pref in preferences)
        {
            var titleTags = pref.Title?.Split(" ") ?? null;
            foreach (var name in new[] { pref.CustomTagName, pref.Title }
                .Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var tag = await _tagRepo.FindByNameAsync(name!.Trim());
                if (tag != null && !pref.TagIds.Contains(tag.Id))
                    pref.TagIds.Add(tag.Id);
            }
        }
    }
}