using Microsoft.AspNetCore.Identity;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MealPlanner.Services;

public class SeedService
{
    public static async Task SeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<MealPlannerDBContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedService>>();

        try
        {
            logger.LogInformation("Applying migrations (Database.Migrate).");
            await context.Database.MigrateAsync();

            // ✅ Seed Dietary Restrictions (only if none exist)
            logger.LogInformation("Purging expired auto-removed pantry records.");
            await PurgeExpiredAutoRemovedIngredientsAsync(context, logger);

            logger.LogInformation("Seeding measurements.");
            await SeedMeasurementsAsync(context, logger);

            logger.LogInformation("Migrating orphan measurements.");
            await MigrateOrphanMeasurementsAsync(context, logger);

            logger.LogInformation("Seeding dietary restrictions.");
            await SeedDietaryRestrictionsAsync(context, logger);

            logger.LogInformation("Seeding tags.");
            await SeedTagsAsync(context, logger);

            logger.LogInformation("Seeding recipes.");
            await SeedRecipesAsync(context, logger);

            logger.LogInformation("Seeding roles.");
            await AddRoleAsync(roleManager, "Admin");
            await AddRoleAsync(roleManager, "User");

            logger.LogInformation("Seeding admin user.");
            var adminEmail = "admin@codehub.com";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new User
                {
                    FullName = "Code Hub",
                    UserName = adminEmail,
                    NormalizedUserName = adminEmail.ToUpper(),
                    Email = adminEmail,
                    NormalizedEmail = adminEmail.ToUpper(),
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");

                if (result.Succeeded)
                {
                    logger.LogInformation("Assigning Admin role to the admin user.");
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Admin user already exists.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding data.");
            throw;
        }
    }

    private static async Task PurgeExpiredAutoRemovedIngredientsAsync(MealPlannerDBContext context, ILogger logger)
    {
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var expired = context.MealAutoRemovedIngredients
            .Where(r => r.CreatedAt < cutoff)
            .ToList();

        if (expired.Count == 0) return;

        context.MealAutoRemovedIngredients.RemoveRange(expired);
        await context.SaveChangesAsync();
        logger.LogInformation("Purged {Count} expired auto-removed pantry records.", expired.Count);
    }

    private static async Task SeedMeasurementsAsync(MealPlannerDBContext context, ILogger logger)
    {
        (string Name, string Abbreviation, int SortOrder)[] definitions =
        [
            ("Count",       "Count",  1),
            ("Pinch",       "Pinch",  2),
            ("Teaspoon",    "tsp",    3),
            ("Tablespoon",  "tbsp",   4),
            ("Fluid Ounce", "fl oz",  5),
            ("Cup",         "cup",    6),
            ("Pint",        "pt",     7),
            ("Quart",       "qt",     8),
            ("Gallon",      "gal",    9),
            ("Milliliter",  "mL",    10),
            ("Liter",       "L",     11),
            ("Ounce",       "oz",    12),
            ("Pound",       "lb",    13),
            ("Gram",        "g",     14),
        ];

        var existing = (await context.Set<Measurement>().ToListAsync())
            .ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        foreach (var (name, abbrev, order) in definitions)
        {
            if (existing.TryGetValue(name, out var row))
            {
                if (row.Abbreviation != abbrev || row.SortOrder != order)
                {
                    row.Abbreviation = abbrev;
                    row.SortOrder = order;
                    updated++;
                }
            }
            else
            {
                context.Set<Measurement>().Add(new Measurement { Name = name, Abbreviation = abbrev, SortOrder = order });
                added++;
            }
        }

        if (added > 0 || updated > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Measurements seeded: {Added} added, {Updated} updated.", added, updated);
        }
        else
        {
            logger.LogInformation("All measurements up to date; skipping seed.");
        }
    }

    private static async Task MigrateOrphanMeasurementsAsync(MealPlannerDBContext context, ILogger logger)
    {
        var orphans = await context.Set<Measurement>()
            .Where(m => m.Abbreviation == "")
            .ToListAsync();

        if (orphans.Count == 0)
        {
            logger.LogInformation("No orphan measurements to migrate.");
            return;
        }

        var orphanIds = orphans.Select(m => m.Id).ToHashSet();

        // Measurements used by recipe ingredients must keep their name as abbreviation so
        // recipe data isn't corrupted. Only truly unused measurements are deleted.
        var usedByIngredients = await context.Set<Ingredient>()
            .Where(i => orphanIds.Contains(i.Measurement.Id))
            .Select(i => i.Measurement.Id)
            .Distinct()
            .ToListAsync();

        var usedByIngredientIds = usedByIngredients.ToHashSet();

        int preserved = 0, deleted = 0;

        var countMeasurement = await context.Set<Measurement>()
            .FirstOrDefaultAsync(m => m.Name == "Count");

        foreach (var orphan in orphans)
        {
            if (usedByIngredientIds.Contains(orphan.Id))
            {
                // Keep the measurement — just give it an abbreviation equal to its name
                // so it is no longer treated as an orphan on future runs.
                orphan.Abbreviation = orphan.Name;
                preserved++;
            }
            else
            {
                // Not used by any ingredient. Remap any shopping-list items to Count, then delete.
                if (countMeasurement != null)
                {
                    var affectedItems = await context.Set<ShoppingListItem>()
                        .Where(i => i.MeasurementId == orphan.Id)
                        .ToListAsync();
                    foreach (var item in affectedItems)
                        item.MeasurementId = countMeasurement.Id;
                }

                context.Set<Measurement>().Remove(orphan);
                deleted++;
            }
        }

        await context.SaveChangesAsync();

        if (preserved > 0 || deleted > 0)
            logger.LogInformation(
                "Orphan measurements: {Preserved} preserved (used by ingredients), {Deleted} deleted (unused).",
                preserved, deleted);
    }

    private static async Task SeedDietaryRestrictionsAsync(MealPlannerDBContext context, ILogger logger)
    {
        HashSet<string> existing = (await context.DietaryRestrictions
            .Select(d => d.Name)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] names = ["Gluten-Free", "Dairy-Free", "Vegetarian", "Vegan", "Keto", "Halal", "Kosher", "Nut Allergy"];
        var toAdd = names.Where(n => !existing.Contains(n)).Select(n => new DietaryRestriction { Name = n }).ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation("All dietary restrictions already exist; skipping seed.");
            return;
        }

        context.DietaryRestrictions.AddRange(toAdd);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} dietary restrictions.", toAdd.Count);
    }

    private static async Task SeedTagsAsync(MealPlannerDBContext context, ILogger logger)
    {
        HashSet<string> existing = (await context.Tags
            .Select(t => t.Name)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] names =
        [
            "Breakfast", "Lunch", "Dinner", "Snack", "Dessert", "Quick & Easy",
            "High Protein", "Low Calorie", "Vegetarian", "Vegan", "Gluten-Free",
            "Dairy-Free", "Keto", "Halal", "Kosher", "Nut Allergy", "Appetizer",
            "Italian", "Mexican", "Mediterranean", "Japanese", "Thai", "Chinese",
            "Indian", "American", "Spicy", "Comfort Food", "BBQ", "Sushi",
        ];
        var toAdd = names.Where(n => !existing.Contains(n)).Select(n => new Tag { Name = n }).ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation("All tags already exist; skipping seed.");
            return;
        }

        context.Tags.AddRange(toAdd);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} tags.", toAdd.Count);
    }

    private static async Task SeedRecipesAsync(MealPlannerDBContext context, ILogger logger)
    {
        Dictionary<string, Tag> tagsByName = await context.Tags
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IngredientBase> basesByName = (await context.Set<IngredientBase>().ToListAsync())
            .ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Measurement> measurementsByName = (await context.Set<Measurement>().ToListAsync())
            .ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        HashSet<string> existingNames = (await context.Recipes
            .Select(r => r.Name)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Recipe> recipes = RecipeSeedData.GetRecipes();
        int seededCount = 0;

        foreach (Recipe recipe in recipes)
        {
            if (existingNames.Contains(recipe.Name))
            {
                logger.LogInformation("Recipe '{Name}' already exists; skipping.", recipe.Name);
                continue;
            }
            List<Tag> resolvedTags = new(recipe.Tags.Count);
            foreach (Tag tagShell in recipe.Tags)
            {
                if (!tagsByName.TryGetValue(tagShell.Name, out Tag? tag))
                {
                    throw new InvalidOperationException(
                        $"Seed recipe '{recipe.Name}' references unknown tag '{tagShell.Name}'. Add it to SeedTagsAsync.");
                }
                resolvedTags.Add(tag);
            }
            recipe.Tags.Clear();
            recipe.Tags.AddRange(resolvedTags);

            foreach (Ingredient ingredient in recipe.Ingredients)
            {
                string baseName = ingredient.IngredientBase.Name;
                if (basesByName.TryGetValue(baseName, out IngredientBase? existingBase))
                {
                    ingredient.IngredientBase = existingBase;
                }
                else
                {
                    basesByName[baseName] = ingredient.IngredientBase;
                }

                string measurementName = ingredient.Measurement.Name;
                if (measurementsByName.TryGetValue(measurementName, out Measurement? existingMeasurement))
                {
                    ingredient.Measurement = existingMeasurement;
                }
                else
                {
                    measurementsByName[measurementName] = ingredient.Measurement;
                }
            }

            context.Recipes.Add(recipe);
            seededCount++;
        }

        if (seededCount > 0)
            await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} recipes.", seededCount);
    }

    private static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create {roleName} role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}