using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace SubtitleCompare.App;

public partial class AboutWindow : Window
{
    public bool UpdateWasInstalled { get; private set; }

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion.Current}";
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Theme.ApplyCaption(this);

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = "Checking…";
        try
        {
            var info = await Task.Run(UpdateChecker.Check).ConfigureAwait(true);
            if (info.Error is not null && !info.IsNewer)
            {
                UpdateStatus.Text = info.Error;
                return;
            }

            if (!info.IsNewer)
            {
                UpdateStatus.Text = $"You're on the latest version ({AppVersion.Current}).";
                return;
            }

            UpdateStatus.Text = $"Version {info.RemoteVersion} is available.";
            CheckUpdatesButton.Content = "Update now";
            CheckUpdatesButton.Click -= OnCheckUpdatesClick;
            CheckUpdatesButton.Click += OnUpdateNowClick;
        }
        catch (Exception ex)
        {
            UpdateStatus.Text = ex.Message;
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async void OnUpdateNowClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = "Downloading. The app will restart when it is ready.";
        try
        {
            await Task.Run(UpdateChecker.DownloadAndRestart).ConfigureAwait(true);
            UpdateWasInstalled = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatus.Text = ex.Message;
            CheckUpdatesButton.IsEnabled = true;
        }
    }
}
