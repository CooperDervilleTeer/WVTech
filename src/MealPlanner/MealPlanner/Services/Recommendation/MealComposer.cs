using MealPlanner.Models;

namespace MealPlanner.Services.Recommendation;

public static class MealComposer
{
    // Cap on candidates fed to the brute-force search: subset enumeration is
    // combinatorial, so the per-slot candidate list is bounded here.
    private const int MaxBruteForceCandidates = 20;

    // How hard a subset is penalized for straying from its macro targets,
    // relative to recipe-rank "profit". Tunable.
    private const float MacroMisfitWeight = 2.0f;

    /// <summary>
    /// Selects the subset of candidates that best fits the meal slot. Calories
    /// and recipe count are hard limits; protein/carb/fat targets are soft —
    /// a subset is scored by recipe rank ("profit") minus a penalty for how
    /// far its macro totals stray from the targets, and the highest-scoring
    /// subset wins.
    /// </summary>
    public static List<Recipe> Compose(
        IEnumerable<Recipe> rankedCandidates,
        int calorieTarget,
        int maxRecipes,
        int? proteinTarget = null,
        int? carbTarget = null,
        int? fatTarget = null)
    {
        var pool = rankedCandidates.Take(MaxBruteForceCandidates).ToList();
        int limit = Math.Min(maxRecipes, pool.Count);
        if (limit <= 0) return [];

        var best = new Selection();
        Search(pool, calorieTarget, limit, proteinTarget, carbTarget, fatTarget,
            0, new List<int>(limit), best);

        return best.Indices is null ? [] : best.Indices.Select(i => pool[i]).ToList();
    }

    private sealed class Selection
    {
        public float Objective = float.NegativeInfinity;
        public List<int>? Indices;
    }

    private static void Search(
        List<Recipe> pool, int calorieTarget, int maxRecipes,
        int? proteinTarget, int? carbTarget, int? fatTarget,
        int start, List<int> chosen, Selection best)
    {
        if (chosen.Count > 0)
        {
            int calories = 0, protein = 0, carbs = 0, fat = 0;
            float profit = 0f;
            foreach (int i in chosen)
            {
                var r = pool[i];
                calories += r.Calories;
                protein += r.Protein;
                carbs += r.Carbs;
                fat += r.Fat;
                profit += 1f - (float)i / pool.Count;
            }

            if (calories <= calorieTarget)
            {
                float misfit = 0f;
                if (proteinTarget is > 0) misfit += MacroDeviation(protein, proteinTarget.Value);
                if (carbTarget is > 0) misfit += MacroDeviation(carbs, carbTarget.Value);
                if (fatTarget is > 0) misfit += MacroDeviation(fat, fatTarget.Value);

                float objective = profit - MacroMisfitWeight * misfit;
                if (objective > best.Objective)
                {
                    best.Objective = objective;
                    best.Indices = [.. chosen];
                }
            }
        }

        if (chosen.Count == maxRecipes) return;
        for (int i = start; i < pool.Count; i++)
        {
            chosen.Add(i);
            Search(pool, calorieTarget, maxRecipes, proteinTarget, carbTarget, fatTarget,
                i + 1, chosen, best);
            chosen.RemoveAt(chosen.Count - 1);
        }
    }

    private static float MacroDeviation(int actual, int target)
    {
        float ratio = (float)actual / target - 1f;
        return ratio * ratio;
    }
}
