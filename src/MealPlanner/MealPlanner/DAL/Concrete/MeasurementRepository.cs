using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.DAL.Concrete;

public class MeasurementRepository : Repository<Measurement>, IMeasurementRepository
{
    private readonly MealPlannerDBContext _context;

    public MeasurementRepository(MealPlannerDBContext context) : base(context)
    {
        _context = context;
    }

    public async Task<List<Measurement>> GetAllOrderedAsync()
    {
        return await _dbset
            .Where(m => m.Abbreviation != "")
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
    }

    public async Task<bool> ExistsWithNameAsync(int excludeId, string name)
    {
        return await _dbset.AnyAsync(m => m.Id != excludeId && m.Name.ToLower() == name.ToLower());
    }

    public async Task<Measurement> FindOrCreateByNameAsync(string name)
    {
        var trimmed = name.Trim();
        var existing = await _dbset.FirstOrDefaultAsync(m =>
            m.Name.ToLower() == trimmed.ToLower() ||
            m.Abbreviation.ToLower() == trimmed.ToLower());

        if (existing != null) return existing;

        var measurement = new Measurement { Name = trimmed, Abbreviation = trimmed };
        _dbset.Add(measurement);
        await _context.SaveChangesAsync();
        return measurement;
    }

    public async Task<bool> IsInUseAsync(int id)
        => await _context.Set<Ingredient>().AnyAsync(i => i.Measurement.Id == id);
}
