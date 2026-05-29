using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelThis.Models;

/// <summary>
/// The editable image: a stack of layers (index 0 = bottom) plus undo/redo and
/// drawing primitives. Compositing produces a single uint[] buffer for display
/// and export.
/// </summary>
public partial class PixelDocument : ObservableObject
{
    public int Width { get; }
    public int Height { get; }

    public ObservableCollection<Layer> Layers { get; } = new();

    [ObservableProperty] private int _activeLayerIndex;

    private readonly Stack<UndoEntry> _undo = new();
    private readonly Stack<UndoEntry> _redo = new();
    private const int MaxUndo = 100;

    public event Action? Changed;

    public PixelDocument(int width, int height)
    {
        Width = width;
        Height = height;
        Layers.Add(new Layer(width, height, "Layer 1"));
        ActiveLayerIndex = 0;
    }

    public Layer? ActiveLayer
        => ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count ? Layers[ActiveLayerIndex] : null;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    // ---- Editing primitives (operate on the active layer) ----

    public void SetPixel(int x, int y, uint color)
    {
        var layer = ActiveLayer;
        if (layer is null || !InBounds(x, y)) return;
        layer.Pixels[y * Width + x] = color;
    }

    /// <summary>Flatten all visible layers into a single row-major BGRA buffer.</summary>
    public uint[] Composite()
    {
        var buffer = new uint[Width * Height];
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible) continue;
            var src = layer.Pixels;
            double op = layer.Opacity;
            if (op >= 1.0)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    uint s = src[i];
                    if (s != 0) buffer[i] = PixelColor.Over(s, buffer[i]);
                }
            }
            else if (op > 0.0)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    uint s = PixelColor.ApplyOpacity(src[i], op);
                    if (s != 0) buffer[i] = PixelColor.Over(s, buffer[i]);
                }
            }
        }
        return buffer;
    }

    public uint GetCompositePixel(int x, int y)
    {
        if (!InBounds(x, y)) return PixelColor.Transparent;
        uint outc = PixelColor.Transparent;
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible) continue;
            uint src = PixelColor.ApplyOpacity(layer.Pixels[y * Width + x], layer.Opacity);
            outc = PixelColor.Over(src, outc);
        }
        return outc;
    }

    /// <summary>Bresenham line between two pixels, stamping with a square brush.</summary>
    public void DrawLine(int x0, int y0, int x1, int y1, uint color, int brush)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Stamp(x0, y0, color, brush);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private void Stamp(int cx, int cy, uint color, int brush)
    {
        if (brush <= 1) { SetPixel(cx, cy, color); return; }
        int half = brush / 2;
        for (int oy = -half; oy < brush - half; oy++)
            for (int ox = -half; ox < brush - half; ox++)
                SetPixel(cx + ox, cy + oy, color);
    }

    /// <summary>Flood fill the active layer from (x,y) with <paramref name="color"/> (4-way).</summary>
    public void FloodFill(int x, int y, uint color)
    {
        var layer = ActiveLayer;
        if (layer is null || !InBounds(x, y)) return;

        var px = layer.Pixels;
        uint target = px[y * Width + x];
        if (target == color) return;

        var stack = new Stack<(int, int)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (!InBounds(cx, cy)) continue;
            int i = cy * Width + cx;
            if (px[i] != target) continue;
            px[i] = color;
            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }
    }

    // ---- Layer management ----

    public Layer AddLayer()
    {
        var layer = new Layer(Width, Height, $"Layer {Layers.Count + 1}");
        int insertAt = ActiveLayerIndex + 1;
        Layers.Insert(insertAt, layer);
        ActiveLayerIndex = insertAt;
        NotifyChanged();
        return layer;
    }

    public void RemoveActiveLayer()
    {
        if (Layers.Count <= 1) return;
        Layers.RemoveAt(ActiveLayerIndex);
        ActiveLayerIndex = Math.Clamp(ActiveLayerIndex, 0, Layers.Count - 1);
        NotifyChanged();
    }

    public void MoveActiveLayer(int delta)
    {
        int from = ActiveLayerIndex;
        int to = from + delta;
        if (to < 0 || to >= Layers.Count) return;
        Layers.Move(from, to);
        ActiveLayerIndex = to;
        NotifyChanged();
    }

    // ---- Undo / redo (per-layer pixel snapshots) ----

    private readonly record struct UndoEntry(int LayerIndex, uint[] Pixels);

    public void PushUndoSnapshot()
    {
        var layer = ActiveLayer;
        if (layer is null) return;
        _undo.Push(new UndoEntry(ActiveLayerIndex, layer.ClonePixels()));
        if (_undo.Count > MaxUndo)
        {
            // Drop oldest by rebuilding (rare path).
            var keep = _undo.ToArray();
            _undo.Clear();
            for (int i = keep.Length - 2; i >= 0; i--) _undo.Push(keep[i]);
        }
        _redo.Clear();
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Undo() => Swap(_undo, _redo);
    public void Redo() => Swap(_redo, _undo);

    private void Swap(Stack<UndoEntry> from, Stack<UndoEntry> to)
    {
        if (from.Count == 0) return;
        var entry = from.Pop();
        if (entry.LayerIndex < 0 || entry.LayerIndex >= Layers.Count) return;
        var layer = Layers[entry.LayerIndex];
        to.Push(new UndoEntry(entry.LayerIndex, layer.ClonePixels()));
        Array.Copy(entry.Pixels, layer.Pixels, entry.Pixels.Length);
        ActiveLayerIndex = entry.LayerIndex;
        NotifyChanged();
    }

    public void NotifyChanged() => Changed?.Invoke();
}
