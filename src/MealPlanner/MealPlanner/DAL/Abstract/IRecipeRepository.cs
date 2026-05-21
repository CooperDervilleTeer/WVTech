using MealPlanner.Models;

namespace MealPlanner.DAL.Abstract;

public interface IRecipeRepository : IRepository<Recipe>
{
    public List<Recipe> GetRecipesByName(string name);
    public Task<Recipe?> ReadRecipeWithIngredientsAsync(int id);
    public Recipe? ReadRecipeByExternalUri(string uri);
    public Task<List<Recipe>> GetRecipesByExternalUrisAsync(IEnumerable<string> uris);
    public Task<List<Recipe>> GetAllWithTagsAndIngredientsAsync();
    public List<Recipe> GetRecipesByNameAndTag(string name, string tag);
    public List<Recipe> GetRecipesByNameAndRestrictions(string name, IEnumerable<string> restrictionTagNames);
}