using System;
using System.Globalization;

namespace PixelThis.Models;

/// <summary>
/// Helpers for packed 32-bit colors. Pixels are stored as uint in 0xAARRGGBB.
/// On little-endian machines this lays out in memory as B,G,R,A, which matches
/// Avalonia's <c>PixelFormat.Bgra8888</c> with <c>AlphaFormat.Unpremul</c>, so a
/// uint[] buffer can be blitted straight into a WriteableBitmap.
/// </summary>
public static class PixelColor
{
    public const uint Transparent = 0x00000000u;

    public static uint Pack(byte a, byte r, byte g, byte b)
        => ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

    public static byte A(uint c) => (byte)(c >> 24);
    public static byte R(uint c) => (byte)(c >> 16);
    public static byte G(uint c) => (byte)(c >> 8);
    public static byte B(uint c) => (byte)c;

    /// <summary>Source-over alpha blend of <paramref name="src"/> on top of <paramref name="dst"/>.</summary>
    public static uint Over(uint src, uint dst)
    {
        uint sa = A(src);
        if (sa == 255) return src;
        if (sa == 0) return dst;

        uint da = A(dst);
        // outA = sa + da*(1-sa)
        uint outA = sa + da * (255 - sa) / 255;
        if (outA == 0) return Transparent;

        uint sr = R(src), sg = G(src), sb = B(src);
        uint dr = R(dst), dg = G(dst), db = B(dst);

        uint w2 = da * (255 - sa) / 255;
        byte r = (byte)((sr * sa + dr * w2) / outA);
        byte g = (byte)((sg * sa + dg * w2) / outA);
        byte b = (byte)((sb * sa + db * w2) / outA);
        return Pack((byte)outA, r, g, b);
    }

    /// <summary>Scale a color's alpha by an opacity in [0,1].</summary>
    public static uint ApplyOpacity(uint c, double opacity)
    {
        if (opacity >= 1.0) return c;
        if (opacity <= 0.0) return Transparent;
        byte a = (byte)Math.Clamp(A(c) * opacity, 0, 255);
        return Pack(a, R(c), G(c), B(c));
    }

    public static string ToHex(uint c, bool includeAlpha = false)
    {
        return includeAlpha
            ? $"#{R(c):X2}{G(c):X2}{B(c):X2}{A(c):X2}"
            : $"#{R(c):X2}{G(c):X2}{B(c):X2}";
    }

    /// <summary>
    /// Parse a single hex color token. Accepts optional '#', and 3/4/6/8 digit forms.
    /// Returns false when the token is not a valid color.
    /// </summary>
    public static bool TryParse(string token, out uint color)
    {
        color = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var s = token.Trim();
        if (s.StartsWith("#")) s = s[1..];
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];

        // Expand shorthand 3/4 digit forms (RGB / RGBA).
        if (s.Length is 3 or 4)
        {
            var expanded = string.Empty;
            foreach (var ch in s) expanded += new string(ch, 2);
            s = expanded;
        }

        if (s.Length is not (6 or 8)) return false;

        byte r, g, b, a = 255;
        if (!TryByte(s, 0, out r) || !TryByte(s, 2, out g) || !TryByte(s, 4, out b)) return false;
        if (s.Length == 8 && !TryByte(s, 6, out a)) return false;

        color = Pack(a, r, g, b);
        return true;
    }

    private static bool TryByte(string s, int i, out byte value)
        => byte.TryParse(s.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}
