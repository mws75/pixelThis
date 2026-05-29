using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PixelThis.Views;

public partial class NewImageDialog : Window
{
    public record Result(int Width, int Height);

    public NewImageDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnPreset(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string s && int.TryParse(s, out int size))
        {
            this.FindControl<NumericUpDown>("WidthBox")!.Value = size;
            this.FindControl<NumericUpDown>("HeightBox")!.Value = size;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        int w = (int)(this.FindControl<NumericUpDown>("WidthBox")!.Value ?? 32);
        int h = (int)(this.FindControl<NumericUpDown>("HeightBox")!.Value ?? 32);
        w = Math.Clamp(w, 1, 1024);
        h = Math.Clamp(h, 1, 1024);
        Close(new Result(w, h));
    }
}
