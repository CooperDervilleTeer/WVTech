using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanner.DAL.Concrete;

public class RecipeRepository : Repository<Recipe>, IRecipeRepository
{
    DbSet<IngredientBase> _ingredientBaseSet;
    DbSet<Measurement> _measurementSet;
    DbSet<Tag> _tagSet;

    public RecipeRepository(MealPlannerDBContext context)
        : base(context)
    {
        _ingredientBaseSet = context.Set<IngredientBase>();
        _measurementSet = context.Set<Measurement>();
        _tagSet = context.Set<Tag>();
    }

    public List<Recipe> GetRecipesByName(string name)
    {        
        List<Recipe> results = _dbset
            .Where(r => 
                r.ExternalUri == null && 
                (r.Name.ToLower().Contains($" {name.ToLower()}") || 
                r.Name.ToLower().StartsWith($"{name.ToLower()}")))
            .ToList();
        
        return results;
    }

    public override Recipe CreateOrUpdate(Recipe recipe)
    {
        // Resolve tag shell objects (Id=0, name only) to real tracked Tag entities.
        // Find existing tags by name; deduplicate new custom tags with a dictionary.
        Dictionary<string, Tag> newTags = new(StringComparer.OrdinalIgnoreCase);
        List<Tag> resolvedTags = [];

        foreach (Tag t in recipe.Tags.ToList())
        {
            Tag? existing = _tagSet.FirstOrDefault(tag => tag.Name == t.Name);
            if (existing != null)
            {
                resolvedTags.Add(existing);
            }
            else if (newTags.TryGetValue(t.Name, out Tag? alreadyAdded))
            {
                resolvedTags.Add(alreadyAdded);
            }
            else
            {
                newTags[t.Name] = t;
                resolvedTags.Add(t);
            }
        }

        recipe.Tags.Clear();
        recipe.Tags.AddRange(resolvedTags);

        HashSet<IngredientBase> newIngredientBases = [];
        HashSet<Measurement> newMeasurements = [];

        foreach (Ingredient i in recipe.Ingredients)
        {
            if (i.IngredientBase.Id == 0)
            {
                i.IngredientBase.Name = IngredientNameNormalizer.NormalizeKey(i.IngredientBase.Name);

                // Use existing db entry for IngredientBase if it exits, ensuring Unique constraint
                try
                {
                    var found = _ingredientBaseSet.Where(b => b.Name == i.IngredientBase.Name).First();
                    i.IngredientBase = found;
                }
                catch (InvalidOperationException)
                {
                    var duplicate = !newIngredientBases.Add(i.IngredientBase);
                    if (duplicate)
                    {
                        // Make Object reference the same so EF knows that they're the same
                        i.IngredientBase = newIngredientBases.First(b => b.Name == i.IngredientBase.Name);
                    }
                }
            }

            if (i.Measurement.Id == 0)
            {
                if (string.IsNullOrEmpty(i.Measurement.Name)) continue;

                // Use existing db entry for Measurement if it exits, ensuring Unique constraint
                try
                {
                    var found = _measurementSet.Where(m => m.Name == i.Measurement.Name).First();
                    i.Measurement = found;
                }
                catch (InvalidOperationException)
                {
                    var duplicate = !newMeasurements.Add(i.Measurement);
                    if (duplicate)
                    {
                        // Make Object reference the same so EF knows that they're the same
                        i.Measurement = newMeasurements.First(m => m.Name == i.Measurement.Name);
                    }
                }
            }
        }

        if (!newIngredientBases.IsNullOrEmpty())  _ingredientBaseSet.AddRange(newIngredientBases); 
        if (!newMeasurements.IsNullOrEmpty())  _measurementSet.AddRange(newMeasurements); 

        return base.CreateOrUpdate(recipe);
    }

    public List<Recipe> GetRecipesByNameAndTag(string name, string tag)
    {
        var query = _dbset.Include(r => r.Tags)
            .Where(r => r.ExternalUri == null &&
                        r.Tags.Any(t => t.Name.ToLower() == tag.ToLower()));

        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(r =>
                r.Name.ToLower().Contains($" {name.ToLower()}") ||
                r.Name.ToLower().StartsWith(name.ToLower()));
        }

        return query.ToList();
    }

    public async Task<Recipe?> ReadRecipeWithIngredientsAsync(int id)
    {
        return await _dbset
            .Include(r => r.Ingredients)
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public Recipe? ReadRecipeByExternalUri(string uri)
    {
        return _dbset.FirstOrDefault(r => r.ExternalUri == uri);
    }

    public async Task<List<Recipe>> GetRecipesByExternalUrisAsync(IEnumerable<string> uris)
    {
        var uriList = uris.Distinct().ToList();
        if (uriList.Count == 0) return [];

        return await _dbset
            .Include(r => r.Tags)
            .Include(r => r.Ingredients)
            .Where(r => r.ExternalUri != null && uriList.Contains(r.ExternalUri))
            .ToListAsync();
    }

    public List<Recipe> GetRecipesByNameAndRestrictions(string name, IEnumerable<string> restrictionTagNames)
    {
        var required = restrictionTagNames.Select(n => n.ToLower()).ToList();

        var query = _dbset.Include(r => r.Tags)
            .Where(r => r.ExternalUri == null &&
                        required.All(tag => r.Tags.Any(t => t.Name.ToLower() == tag)));

        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(r =>
                r.Name.ToLower().Contains($" {name.ToLower()}") ||
                r.Name.ToLower().StartsWith(name.ToLower()));
        }

        return query.ToList();
    }

    public async Task<List<Recipe>> GetAllWithTagsAndIngredientsAsync()
    {
        return await _dbset
            .Include(r => r.Tags)
            .Include(r => r.Ingredients)
            .ToListAsync();
    }
}