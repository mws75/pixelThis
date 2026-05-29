using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelThis.Models;

/// <summary>A swatch in the palette panel.</summary>
public partial class PaletteColor : ObservableObject
{
    public uint Value { get; }

    public PaletteColor(uint value) => Value = value;

    public string Hex => PixelColor.ToHex(Value, includeAlpha: PixelColor.A(Value) != 255);

    public IBrush Brush => new SolidColorBrush(Color.FromUInt32(SwapToArgb(Value)));

    // PixelColor stores 0xAARRGGBB already, which is what Color.FromUInt32 expects.
    private static uint SwapToArgb(uint c) => c;
}
