using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealPlanner.Controllers;

[Authorize(Roles = "Admin")]
public class MeasurementsController : Controller
{
    private readonly IMeasurementRepository _measurementRepo;
    private readonly MealPlannerDBContext _context;

    public MeasurementsController(IMeasurementRepository measurementRepo, MealPlannerDBContext context)
    {
        _measurementRepo = measurementRepo;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var measurements = await _measurementRepo.GetAllOrderedAsync();
        return View(measurements);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(abbreviation))
        {
            TempData["Error"] = "Name and abbreviation are required.";
            return RedirectToAction(nameof(Index));
        }

        name = name.Trim();
        abbreviation = abbreviation.Trim();

        if (await _measurementRepo.ExistsWithNameAsync(0, name))
        {
            TempData["Error"] = $"A measurement named \"{name}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        _measurementRepo.CreateOrUpdate(new Measurement { Name = name, Abbreviation = abbreviation });
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Measurement \"{name}\" added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(abbreviation))
        {
            TempData["Error"] = "Name and abbreviation are required.";
            return RedirectToAction(nameof(Index));
        }

        var measurement = (Measurement?)_measurementRepo.Read(id);
        if (measurement == null)
            return NotFound();

        name = name.Trim();
        abbreviation = abbreviation.Trim();

        if (await _measurementRepo.ExistsWithNameAsync(id, name))
        {
            TempData["Error"] = $"Another measurement named \"{name}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        measurement.Name = name;
        measurement.Abbreviation = abbreviation;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Measurement updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var measurement = (Measurement?)_measurementRepo.Read(id);
        if (measurement == null)
            return NotFound();

        bool inUse = await _measurementRepo.IsInUseAsync(id);
        if (inUse)
        {
            TempData["Error"] = $"Cannot delete \"{measurement.Name}\" — it is used by one or more ingredients.";
            return RedirectToAction(nameof(Index));
        }

        _measurementRepo.Delete(measurement);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Measurement \"{measurement.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }
}
