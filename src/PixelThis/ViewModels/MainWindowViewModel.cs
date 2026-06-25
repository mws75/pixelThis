using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelThis.Models;
using PixelThis.Services;
using PixelThis.Tools;

namespace PixelThis.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private PixelDocument _document;
    [ObservableProperty] private ToolType _activeTool = ToolType.Pencil;
    [ObservableProperty] private double _zoom = 16;
    [ObservableProperty] private int _brushSize = 1;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private uint _currentColor = PixelColor.Pack(255, 0xE8, 0xE8, 0xF0);

    /// <summary>Side length of the current canvas when it is square; 0 otherwise. Drives the Canvas Size tiles.</summary>
    [ObservableProperty] private int _activeCanvasSize = 32;

    public ObservableCollection<PaletteColor> Palette { get; } = new();

    public MainWindowViewModel()
    {
        _document = new PixelDocument(32, 32);
        SeedDefaultPalette();
    }

    // ---- Derived display properties ----

    public IBrush CurrentColorBrush => new SolidColorBrush(Color.FromUInt32(CurrentColor));

    /// <summary>Two-way bridge for the ColorPicker, which works in <see cref="Color"/> not packed uint.</summary>
    public Color CurrentColorValue
    {
        get => Color.FromUInt32(CurrentColor);
        set => CurrentColor = value.ToUInt32();
    }

    public string CurrentColorHex
    {
        get => PixelColor.ToHex(CurrentColor);
        set
        {
            if (PixelColor.TryParse(value, out uint c)) CurrentColor = c;
        }
    }

    public string DocumentInfo => $"{Document.Width} × {Document.Height} px";

    partial void OnCurrentColorChanged(uint value)
    {
        OnPropertyChanged(nameof(CurrentColorBrush));
        OnPropertyChanged(nameof(CurrentColorHex));
        OnPropertyChanged(nameof(CurrentColorValue));
    }

    partial void OnDocumentChanged(PixelDocument value)
    {
        OnPropertyChanged(nameof(DocumentInfo));
        RefreshUndoState();
    }

    public bool CanUndo => Document.CanUndo;
    public bool CanRedo => Document.CanRedo;

    public void RefreshUndoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    // ---- Tool commands ----

    [RelayCommand]
    private void SelectTool(ToolType tool) => ActiveTool = tool;

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(64, Zoom * 2);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(1, Zoom / 2);

    [RelayCommand]
    private void Undo() { Document.Undo(); RefreshUndoState(); }

    [RelayCommand]
    private void Redo() { Document.Redo(); RefreshUndoState(); }

    // ---- Layer commands ----

    [RelayCommand]
    private void AddLayer() => Document.AddLayer();

    [RelayCommand]
    private void RemoveLayer() => Document.RemoveActiveLayer();

    [RelayCommand]
    private void MoveLayerUp() => Document.MoveActiveLayer(+1);

    [RelayCommand]
    private void MoveLayerDown() => Document.MoveActiveLayer(-1);

    // ---- Palette commands ----

    [RelayCommand]
    private void SelectColor(PaletteColor color) => CurrentColor = color.Value;

    [RelayCommand]
    private void AddCurrentColorToPalette()
    {
        foreach (var c in Palette)
            if (c.Value == CurrentColor) return;
        Palette.Add(new PaletteColor(CurrentColor));
    }

    [RelayCommand]
    private void ClearPalette() => Palette.Clear();

    public void ImportPalette(string text, bool replace)
    {
        var colors = PaletteImporter.Parse(text);
        if (replace) Palette.Clear();
        foreach (var c in colors)
        {
            bool exists = false;
            foreach (var existing in Palette)
                if (existing.Value == c) { exists = true; break; }
            if (!exists) Palette.Add(new PaletteColor(c));
        }
    }

    /// <summary>
    /// Default magnification a freshly created/opened canvas opens at. Kept fixed
    /// (rather than fit-to-view) so a larger pixel resolution visibly opens larger;
    /// the user zooms in/out from here.
    /// </summary>
    private const double DefaultZoom = 16;

    /// <summary>Start a fresh square canvas at the chosen pixel resolution (from the Canvas Size tiles).</summary>
    [RelayCommand]
    private void SetCanvasSize(string size)
    {
        if (int.TryParse(size, out int n)) NewDocument(n, n);
    }

    public void NewDocument(int width, int height)
    {
        Document = new PixelDocument(width, height);
        Zoom = DefaultZoom;
        ActiveCanvasSize = width == height ? width : 0;
    }

    /// <summary>Replace the whole working session with a loaded project.</summary>
    public void LoadProject(PixelDocument document, IReadOnlyList<uint> palette, uint currentColor)
    {
        Document = document;
        Palette.Clear();
        foreach (var c in palette) Palette.Add(new PaletteColor(c));
        CurrentColor = currentColor;
        Zoom = DefaultZoom;
        ActiveCanvasSize = document.Width == document.Height ? document.Width : 0;
    }

    private void SeedDefaultPalette()
    {
        // A pleasant default 16-color ramp (DB16-inspired) so the palette isn't empty.
        string[] hex =
        {
            "#140c1c", "#442434", "#30346d", "#4e4a4e", "#854c30", "#346524", "#d04648", "#757161",
            "#597dce", "#d27d2c", "#8595a1", "#6daa2c", "#d2aa99", "#6dc2ca", "#dad45e", "#deeed6",
        };
        foreach (var h in hex)
            if (PixelColor.TryParse(h, out uint c))
                Palette.Add(new PaletteColor(c));
    }
}
