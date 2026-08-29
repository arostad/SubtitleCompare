using System.Diagnostics;
using System.Windows;
using SubtitleCompare.Core.Ffmpeg;

namespace SubtitleCompare.App;

public partial class FfmpegMissingWindow : Window
{
    public const string WingetCommand =
        "winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements";

    public bool Installed { get; private set; }

    public FfmpegMissingWindow()
    {
        InitializeComponent();
        CommandBox.Text = WingetCommand;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Theme.ApplyCaption(this);

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(WingetCommand);
        StatusText.Text = "Copied. Paste that into PowerShell if you would rather install it yourself.";
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        CopyButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        StatusText.Text = "A console window is opening. Watch winget, then press a key when it says to.";

        int exit;
        try
        {
            exit = await Task.Run(RunWinget).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            InstallButton.IsEnabled = true;
            CopyButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
            return;
        }

        FfmpegLocator.RefreshSearchPath();
        if (FfmpegLocator.IsAvailable())
        {
            Installed = true;
            StatusText.Text = "FFmpeg is ready.";
            DialogResult = true;
            return;
        }

        StatusText.Text = exit == 0
            ? "winget finished, but ffprobe is still not on PATH. Close Subtitle Compare and open it again."
            : $"winget exited with code {exit}. You can copy the command and run it in PowerShell.";
        InstallButton.IsEnabled = true;
        CopyButton.IsEnabled = true;
        CloseButton.IsEnabled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static int RunWinget()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments =
                "/c title Installing FFmpeg && " +
                WingetCommand +
                " && echo. && echo Done. && pause || (echo. && echo Install did not finish. && pause)",
            UseShellExecute = true,
            CreateNoWindow = false,
        };
        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Could not open a console window for winget.");
        process.WaitForExit();
        return process.ExitCode;
    }
}
