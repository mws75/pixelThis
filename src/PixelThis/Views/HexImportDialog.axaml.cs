using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PixelThis.Views;

public partial class HexImportDialog : Window
{
    public record Result(string Text, bool Replace);

    public HexImportDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnImport(object? sender, RoutedEventArgs e)
    {
        var input = this.FindControl<TextBox>("Input")!;
        var replace = this.FindControl<CheckBox>("ReplaceCheck")!;
        Close(new Result(input.Text ?? string.Empty, replace.IsChecked ?? true));
    }
}
