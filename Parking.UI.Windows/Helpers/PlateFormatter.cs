using System;
using System.Linq;

namespace Parking.UI.Windows.Helpers;

/// <summary>
/// Applies the standard Ecuadorian plate format while the operator types.
/// </summary>
public static class PlateFormatter
{
    private const int PrefixLength = 3;
    private const int MaximumAlphanumericLength = 7;

    public static string Format(string? value)
    {
        var characters = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .Take(MaximumAlphanumericLength)
            .ToArray());

        return characters.Length > PrefixLength
            ? characters.Insert(PrefixLength, "-")
            : characters;
    }
}
