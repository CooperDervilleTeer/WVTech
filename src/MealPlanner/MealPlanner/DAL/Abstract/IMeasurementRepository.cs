using MealPlanner.Models;

namespace MealPlanner.DAL.Abstract;

public interface IMeasurementRepository : IRepository<Measurement>
{
    Task<List<Measurement>> GetAllOrderedAsync();
    Task<bool> ExistsWithNameAsync(int excludeId, string name);
    Task<Measurement> FindOrCreateByNameAsync(string name);
    Task<bool> IsInUseAsync(int id);
}
