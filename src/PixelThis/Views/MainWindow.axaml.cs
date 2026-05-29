using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PixelThis.Controls;
using PixelThis.Models;
using PixelThis.Services;
using PixelThis.Tools;
using PixelThis.ViewModels;

namespace PixelThis.Views;

public partial class MainWindow : Window
{
    private PixelEditorControl _editor = null!;
    private TextBlock _hoverInfo = null!;

    public MainWindow()
    {
        InitializeComponent();
        _editor = this.FindControl<PixelEditorControl>("Editor")!;
        _hoverInfo = this.FindControl<TextBlock>("HoverInfo")!;

        _editor.Edited += (_, _) => Vm?.RefreshUndoState();
        _editor.PointerMoved += OnEditorPointerMoved;
        _editor.PointerExited += (_, _) => _hoverInfo.Text = string.Empty;

        KeyDown += OnKeyDown;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;
        var p = e.GetPosition(_editor);
        int x = (int)Math.Floor(p.X / vm.Zoom);
        int y = (int)Math.Floor(p.Y / vm.Zoom);
        _hoverInfo.Text = vm.Document.InBounds(x, y) ? $"{x}, {y}" : string.Empty;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;

        bool cmd = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (cmd && e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { vm.Document.Redo(); }
            else { vm.Document.Undo(); }
            vm.RefreshUndoState();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.B: vm.ActiveTool = ToolType.Pencil; break;
            case Key.E: vm.ActiveTool = ToolType.Eraser; break;
            case Key.G: vm.ActiveTool = ToolType.Fill; break;
            case Key.I: vm.ActiveTool = ToolType.Picker; break;
            case Key.OemPlus or Key.Add: vm.Zoom = Math.Min(64, vm.Zoom * 2); break;
            case Key.OemMinus or Key.Subtract: vm.Zoom = Math.Max(1, vm.Zoom / 2); break;
        }
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new NewImageDialog();
        var result = await dlg.ShowDialog<NewImageDialog.Result?>(this);
        if (result is not null)
            Vm?.NewDocument(result.Width, result.Height);
    }

    private async void OnImportPaletteClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new HexImportDialog();
        var result = await dlg.ShowDialog<HexImportDialog.Result?>(this);
        if (result is not null && Vm is not null)
            Vm.ImportPalette(result.Text, result.Replace);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export PNG for Godot",
            SuggestedFileName = "sprite.png",
            DefaultExtension = "png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
            }
        });

        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        PngExporter.Save(vm.Document, stream);
    }
}
