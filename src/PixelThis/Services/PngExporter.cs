using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelThis.Models;

namespace PixelThis.Services;

/// <summary>Renders a document to a transparent PNG suitable for Godot import.</summary>
public static class PngExporter
{
    public static unsafe WriteableBitmap ToBitmap(PixelDocument doc)
    {
        var bmp = new WriteableBitmap(
            new PixelSize(doc.Width, doc.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        var buffer = doc.Composite();
        using var fb = bmp.Lock();
        fixed (uint* p = buffer)
        {
            Buffer.MemoryCopy(p, (void*)fb.Address, (long)buffer.Length * 4, (long)buffer.Length * 4);
        }
        return bmp;
    }

    public static void Save(PixelDocument doc, Stream stream)
    {
        var bmp = ToBitmap(doc);
        bmp.Save(stream); // Avalonia writes PNG.
    }
}
