using System.Collections.Generic;
using System.Text.RegularExpressions;
using PixelThis.Models;

namespace PixelThis.Services;

/// <summary>
/// Parses a free-form blob of hex codes into a de-duplicated, order-preserving
/// list of colors. Accepts codes separated by commas, whitespace, or newlines,
/// with or without '#', and ignores anything that isn't a valid color.
/// </summary>
public static class PaletteImporter
{
    private static readonly Regex TokenRegex = new(@"[#0-9a-fA-F]+", RegexOptions.Compiled);

    public static IReadOnlyList<uint> Parse(string text)
    {
        var result = new List<uint>();
        var seen = new HashSet<uint>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (Match m in TokenRegex.Matches(text))
        {
            if (PixelColor.TryParse(m.Value, out uint color) && seen.Add(color))
                result.Add(color);
        }
        return result;
    }
}
