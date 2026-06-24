using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelThis.Models;
using PixelThis.Tools;

namespace PixelThis.Controls;

/// <summary>
/// Renders a <see cref="PixelDocument"/> as a crisp, zoomable, nearest-neighbor
/// bitmap and routes pointer input to the active tool. Holds its own composite
/// WriteableBitmap that is rebuilt whenever the document changes.
/// </summary>
public class PixelEditorControl : Control
{
    public static readonly StyledProperty<PixelDocument?> DocumentProperty =
        AvaloniaProperty.Register<PixelEditorControl, PixelDocument?>(nameof(Document));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<PixelEditorControl, double>(nameof(Zoom), 16.0);

    public static readonly StyledProperty<ToolType> ActiveToolProperty =
        AvaloniaProperty.Register<PixelEditorControl, ToolType>(nameof(ActiveTool), ToolType.Pencil);

    public static readonly StyledProperty<uint> PrimaryColorProperty =
        AvaloniaProperty.Register<PixelEditorControl, uint>(nameof(PrimaryColor),
            PixelColor.Pack(255, 255, 255, 255), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> BrushSizeProperty =
        AvaloniaProperty.Register<PixelEditorControl, int>(nameof(BrushSize), 1);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<PixelEditorControl, bool>(nameof(ShowGrid), true);

    public PixelDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public ToolType ActiveTool
    {
        get => GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public uint PrimaryColor
    {
        get => GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public int BrushSize
    {
        get => GetValue(BrushSizeProperty);
        set => SetValue(BrushSizeProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    private WriteableBitmap? _bitmap;
    private PixelDocument? _subscribed;
    private bool _drawing;
    private int _lastX, _lastY;
    private bool _hasHover;
    private int _hoverX, _hoverY;

    static PixelEditorControl()
    {
        AffectsRender<PixelEditorControl>(ZoomProperty, ShowGridProperty, ActiveToolProperty, BrushSizeProperty);
        AffectsMeasure<PixelEditorControl>(ZoomProperty, DocumentProperty);
    }

    public PixelEditorControl()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            if (_subscribed is not null) _subscribed.Changed -= OnDocumentChanged;
            _subscribed = Document;
            if (_subscribed is not null) _subscribed.Changed += OnDocumentChanged;
            RebuildBitmap();
        }
    }

    private void OnDocumentChanged()
    {
        Recomposite();
        InvalidateVisual();
    }

    private void RebuildBitmap()
    {
        var doc = Document;
        if (doc is null) { _bitmap = null; return; }
        _bitmap = new WriteableBitmap(
            new PixelSize(doc.Width, doc.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        Recomposite();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private unsafe void Recomposite()
    {
        var doc = Document;
        if (doc is null || _bitmap is null) return;

        var buffer = doc.Composite();

        using var fb = _bitmap.Lock();
        fixed (uint* p = buffer)
        {
            Buffer.MemoryCopy(p, (void*)fb.Address, (long)buffer.Length * 4, (long)buffer.Length * 4);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var doc = Document;
        if (doc is null) return new Size(0, 0);
        return new Size(doc.Width * Zoom, doc.Height * Zoom);
    }

    public override void Render(DrawingContext context)
    {
        var doc = Document;
        if (doc is null || _bitmap is null) return;

        double w = doc.Width * Zoom;
        double h = doc.Height * Zoom;
        var dest = new Rect(0, 0, w, h);

        // Transparency checkerboard behind the image.
        DrawCheckerboard(context, dest);

        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        context.DrawImage(_bitmap, new Rect(0, 0, doc.Width, doc.Height), dest);

        if (ShowGrid && Zoom >= 8)
            DrawGrid(context, doc.Width, doc.Height);

        // Outer frame.
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1), dest);

        // Brush footprint preview under the cursor.
        if (_hasHover && doc.InBounds(_hoverX, _hoverY))
            DrawBrushPreview(context, doc);
    }

    /// <summary>Outline the pixels the active tool would affect at the hovered cell.</summary>
    private void DrawBrushPreview(DrawingContext ctx, PixelDocument doc)
    {
        // Pencil/eraser stamp a BrushSize square (see PixelDocument.Stamp); other
        // tools act on the single hovered cell.
        int brush = ActiveTool is ToolType.Pencil or ToolType.Eraser ? Math.Max(1, BrushSize) : 1;
        int half = brush / 2;
        var rect = new Rect((_hoverX - half) * Zoom, (_hoverY - half) * Zoom, brush * Zoom, brush * Zoom);

        // Two-tone outline (dark halo + thin white) so it stays legible over any color.
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), 3), rect);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), 1), rect);
    }

    private void DrawCheckerboard(DrawingContext ctx, Rect dest)
    {
        var light = new SolidColorBrush(Color.FromRgb(45, 48, 65));
        var dark = new SolidColorBrush(Color.FromRgb(55, 58, 75));
        ctx.FillRectangle(dark, dest);

        const double cell = 8;
        int cols = (int)Math.Ceiling(dest.Width / cell);
        int rows = (int)Math.Ceiling(dest.Height / cell);
        using var clip = ctx.PushClip(dest);
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                if (((x + y) & 1) == 0)
                    ctx.FillRectangle(light, new Rect(x * cell, y * cell, cell, cell));
    }

    private void DrawGrid(DrawingContext ctx, int pw, int ph)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
        for (int x = 1; x < pw; x++)
            ctx.DrawLine(pen, new Point(x * Zoom, 0), new Point(x * Zoom, ph * Zoom));
        for (int y = 1; y < ph; y++)
            ctx.DrawLine(pen, new Point(0, y * Zoom), new Point(pw * Zoom, y * Zoom));
    }

    // ---- Input ----

    private (int x, int y) ToPixel(Point p)
        => ((int)Math.Floor(p.X / Zoom), (int)Math.Floor(p.Y / Zoom));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var doc = Document;
        if (doc is null) return;
        Focus();

        var pt = e.GetCurrentPoint(this);
        if (!pt.Properties.IsLeftButtonPressed) return;

        var (x, y) = ToPixel(e.GetPosition(this));
        if (!doc.InBounds(x, y)) return;

        switch (ActiveTool)
        {
            case ToolType.Picker:
                uint picked = doc.GetCompositePixel(x, y);
                if (PixelColor.A(picked) != 0) PrimaryColor = picked;
                return;

            case ToolType.Fill:
                doc.PushUndoSnapshot();
                doc.FloodFill(x, y, PrimaryColor);
                doc.NotifyChanged();
                NotifyEdited();
                return;

            default:
                doc.PushUndoSnapshot();
                _drawing = true;
                _lastX = x; _lastY = y;
                ApplyPaint(doc, x, y);
                doc.NotifyChanged();
                e.Pointer.Capture(this);
                return;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var doc = Document;
        if (doc is null) return;

        var (x, y) = ToPixel(e.GetPosition(this));

        // Keep the brush preview in sync with the cursor, drawing or not.
        if (!_hasHover || x != _hoverX || y != _hoverY)
        {
            _hasHover = true;
            _hoverX = x; _hoverY = y;
            InvalidateVisual();
        }

        if (!_drawing || (x == _lastX && y == _lastY)) return;

        uint color = ActiveTool == ToolType.Eraser ? PixelColor.Transparent : PrimaryColor;
        doc.DrawLine(_lastX, _lastY, x, y, color, BrushSize);
        _lastX = x; _lastY = y;
        doc.NotifyChanged();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hasHover) { _hasHover = false; InvalidateVisual(); }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drawing)
        {
            _drawing = false;
            e.Pointer.Capture(null);
            NotifyEdited();
        }
    }

    private void ApplyPaint(PixelDocument doc, int x, int y)
    {
        uint color = ActiveTool == ToolType.Eraser ? PixelColor.Transparent : PrimaryColor;
        doc.DrawLine(x, y, x, y, color, BrushSize);
    }

    /// <summary>Raised after a committed edit so the view-model can refresh undo state.</summary>
    public event EventHandler? Edited;
    private void NotifyEdited() => Edited?.Invoke(this, EventArgs.Empty);
}
