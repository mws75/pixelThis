using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelThis.Models;

/// <summary>A single raster layer. Pixels are packed 0xAARRGGBB, row-major, length = W*H.</summary>
public partial class Layer : ObservableObject
{
    public uint[] Pixels { get; }
    public int Width { get; }
    public int Height { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private double _opacity = 1.0;

    public Layer(int width, int height, string name)
    {
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
        _name = name;
    }

    private Layer(int width, int height, string name, uint[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        _name = name;
    }

    /// <summary>Rehydrate a layer from a saved project (pixels are taken as-is, not copied).</summary>
    public static Layer FromSaved(int width, int height, string name, uint[] pixels, bool visible, double opacity)
        => new(width, height, name, pixels) { IsVisible = visible, Opacity = opacity };

    public uint[] ClonePixels()
    {
        var copy = new uint[Pixels.Length];
        System.Array.Copy(Pixels, copy, Pixels.Length);
        return copy;
    }

    public Layer Clone()
        => new(Width, Height, Name, ClonePixels()) { IsVisible = IsVisible, Opacity = Opacity };
}
