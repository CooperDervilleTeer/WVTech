using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MealPlanner.Models;
using MealPlanner.ViewModels;
using MealPlanner.DAL.Abstract;
using MealPlanner.Services;
using Microsoft.AspNetCore.Identity;

namespace MealPlanner.Controllers
{
    // TODO: Remove business logic from controller
    [Authorize]
    public class UserSettingsController : Controller
    {
        private readonly IUserSettingsRepository _userSettings;
        private readonly IUserSettingsService _userSettingsService;
        private readonly ITagRepository _tagRepository;
        private readonly IUserFoodPreferenceRepository _foodPrefRepository;
        private readonly IUserNutritionPreferenceRepository _nutritionPrefRepository;
        private readonly IUserDietaryRestrictionRepository _userDietaryRestrictionRepository;
        private readonly UserManager<User> _userManager;

        public UserSettingsController(IUserSettingsRepository userSettings, IUserSettingsService userSettingsService, ITagRepository tagRepository, IUserFoodPreferenceRepository foodPrefRepository, IUserNutritionPreferenceRepository nutritionPrefRepository, IUserDietaryRestrictionRepository userDietaryRestrictionRepository, UserManager<User> userManager)
        {
            _userSettings = userSettings;
            _userSettingsService = userSettingsService;
            _tagRepository = tagRepository;
            _foodPrefRepository = foodPrefRepository;
            _nutritionPrefRepository = nutritionPrefRepository;
            _userDietaryRestrictionRepository = userDietaryRestrictionRepository;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string section = "profile")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var currentPrefs   = await _foodPrefRepository.GetFoodPreferenceNamesAsync(userId);
            var availableTags  = await _tagRepository.GetTagNamesAsync();
            var nutritionPref    = await _nutritionPrefRepository.GetUsersNutritionPreferenceAsync(userId);
            var allRestrictions  = await _userDietaryRestrictionRepository.GetAllDietaryRestrictionsAsync();
            var selectedIds      = (await _userDietaryRestrictionRepository.GetByUserIdAsync(userId))
                                       .Select(x => x.DietaryRestrictionId)
                                       .ToList();

            var user = await _userManager.FindByIdAsync(userId);
            var profile = await _userSettings.GetByUserIdAsync(userId);

            return View(new SettingsViewModel
            {
                CurrentPreferences = currentPrefs,
                AvailableTags     = availableTags,
                CalorieTarget     = nutritionPref?.CalorieTarget,
                ProteinTarget     = nutritionPref?.ProteinTarget,
                CarbTarget        = nutritionPref?.CarbTarget,
                FatTarget         = nutritionPref?.FatTarget,
                Restrictions      = allRestrictions.Select(r => new DietaryRestrictionOptionViewModel
                {
                    DietaryRestrictionId = r.Id,
                    Name       = r.Name ?? "",
                    IsSelected = selectedIds.Contains(r.Id)
                }).ToList(),
                ActiveSection     = section,
                FullName          = user?.FullName ?? "",
                ProfilePictureUrl = profile?.ProfilePictureUrl,
                DisplayHandle     = profile?.DisplayHandle
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFoodPreferences(FoodPreferenceViewModel vm)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (vm.NewPreferences.Count > 0)
                await _foodPrefRepository.AddFoodPreferencesAsync(userId, vm.NewPreferences);

            if (TempData is not null) TempData["Message"] = "Food preferences saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFoodPreference(string tagName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            await _foodPrefRepository.RemoveFoodPreferenceAsync(userId, tagName);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ThemeChange()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            await _userSettings.ToggleDarkThemeAsync(userId);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleTheme()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Challenge();

            await _userSettings.ToggleDarkThemeAsync(userId);
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.FullName = model.FullName.Trim();
            await _userManager.UpdateAsync(user);

            await _userSettings.UpsertProfileAsync(userId, model.DisplayHandle, model.RemovePhoto, model.PhotoData);
            var profile = await _userSettings.GetByUserIdAsync(userId);

            var nameParts = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var initials = nameParts.Length >= 2
                ? $"{char.ToUpper(nameParts[0][0])}{char.ToUpper(nameParts[^1][0])}"
                : (nameParts.Length == 1 ? char.ToUpper(nameParts[0][0]).ToString() : "");

            var effectiveDisplayName = !string.IsNullOrWhiteSpace(profile?.DisplayHandle)
                ? profile.DisplayHandle
                : user.FullName;

            return Json(new
            {
                success = true,
                initials,
                photoUrl = profile?.ProfilePictureUrl ?? "",
                fullName = user.FullName,
                displayName = effectiveDisplayName,
                handle = profile?.DisplayHandle ?? ""
            });
        }

        [HttpGet]
        public IActionResult Dietary() => RedirectToAction(nameof(Index), new { section = "dietary" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DietaryAutoSave([FromBody] List<int> selectedIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Json(new { success = false });

            await _userDietaryRestrictionRepository.SetForUserAsync(userId, selectedIds ?? []);
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dietary(DietarySettingsViewModel vm)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var chosen = vm.Restrictions
                .Where(x => x.IsSelected)
                .Select(x => x.DietaryRestrictionId);

            await _userDietaryRestrictionRepository.SetForUserAsync(userId, chosen);

            TempData["Message"] = "Dietary restrictions saved.";
            return RedirectToAction(nameof(Index), new { section = "dietary" });
        }

        [HttpGet]
        public IActionResult Nutrition() => RedirectToAction(nameof(Index), new { section = "nutrition" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nutrition(NutritionSettingsViewModel vm)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            await _nutritionPrefRepository.SaveNutritionPreferenceAsync(userId, vm.CalorieTarget, vm.ProteinTarget, vm.CarbTarget, vm.FatTarget);

            TempData["Message"] = "Nutrition goals saved.";
            return RedirectToAction(nameof(Index), new { section = "nutrition" });
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View(new UserSettingsResetPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            UserSettingsResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _userSettingsService.ResetPasswordAsync(
                User,
                model.Password,
                model.NewPassword);

            if (result.Succeeded)
                return ResetPasswordSuccess();

            AddErrors(result);
            return View(model);
        }

        // --- private helpers (controller-only concerns) ---

        private IActionResult ResetPasswordSuccess()
        {
            ViewBag.StatusMessage = "Your password has been reset successfully.";
            ModelState.Clear();
            return View(new UserSettingsResetPasswordViewModel());
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}