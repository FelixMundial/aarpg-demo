using System;

namespace DAARPG.core.extensions;

public static class NumberExtensions
{
    public static bool TryRoundToInt(this float value, out int result)
    {
        if (!float.IsFinite(value))
        {
            result = default(int);
            return false;
        }

        var roundedValue = Math.Round(value, MidpointRounding.AwayFromZero);
        if (roundedValue is < int.MinValue or > int.MaxValue)
        {
            result = default(int);
            return false;
        }

        result = (int)roundedValue;
        return true;
    }
}
