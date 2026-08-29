using System.Windows;

namespace SubtitleCompare.App;

public partial class KeyWindow : Window
{
    public KeyWindow()
    {
        InitializeComponent();
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Theme.ApplyCaption(this);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
