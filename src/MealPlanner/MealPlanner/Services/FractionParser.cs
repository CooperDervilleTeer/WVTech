using System.Globalization;

namespace MealPlanner.Services;

public static class FractionParser
{
    private static readonly (int Num, int Den)[] CommonFractions =
    [
        (1, 8), (1, 4), (1, 3), (3, 8), (1, 2), (5, 8), (2, 3), (3, 4), (7, 8)
    ];

    private const float Tolerance = 0.01f;

    // Accepts "3", "0.5", "1/2", "1 1/2". Returns null for unparseable input.
    public static float? ParseAmount(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();

        if (float.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out float plain))
            return plain >= 0 ? plain : null;

        int spaceIdx = input.IndexOf(' ');
        if (spaceIdx > 0)
        {
            string wholePart = input[..spaceIdx].Trim();
            string fracPart = input[(spaceIdx + 1)..].Trim();
            if (float.TryParse(wholePart, NumberStyles.Any, CultureInfo.InvariantCulture, out float whole) && whole >= 0)
            {
                float? frac = ParseFraction(fracPart);
                if (frac.HasValue) return whole + frac.Value;
            }
        }

        return ParseFraction(input);
    }

    // Formats stored floats as readable strings: 1.0 → "1", 1.5 → "1.5", 1/4 → "1/4".
    // Values that round cleanly to 1 decimal place are kept as decimals; others use fractions.
    public static string FormatAmount(float amount)
    {
        if (amount < 0) return amount.ToString("G", CultureInfo.InvariantCulture);

        float rounded = MathF.Round(amount);
        if (MathF.Abs(amount - rounded) < Tolerance)
            return ((int)rounded).ToString();

        int whole = (int)MathF.Floor(amount);
        float fractional = amount - whole;

        (int bestNum, int bestDen) = FindBestFraction(fractional);
        if (MathF.Abs(fractional - (float)bestNum / bestDen) > Tolerance)
            return amount.ToString("G", CultureInfo.InvariantCulture);

        string fractionStr = $"{bestNum}/{bestDen}";
        return whole > 0 ? $"{whole} {fractionStr}" : fractionStr;
    }

    private static float? ParseFraction(string input)
    {
        int slashIdx = input.IndexOf('/');
        if (slashIdx <= 0 || slashIdx >= input.Length - 1) return null;

        string numStr = input[..slashIdx].Trim();
        string denStr = input[(slashIdx + 1)..].Trim();

        if (!float.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float num)) return null;
        if (!float.TryParse(denStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float den)) return null;
        if (den == 0) return null;

        return num / den;
    }

    private static (int, int) FindBestFraction(float value)
    {
        (int, int) best = CommonFractions[0];
        float bestDiff = float.MaxValue;
        foreach (var (num, den) in CommonFractions)
        {
            float diff = MathF.Abs(value - (float)num / den);
            if (diff < bestDiff) { bestDiff = diff; best = (num, den); }
        }
        return best;
    }
}
