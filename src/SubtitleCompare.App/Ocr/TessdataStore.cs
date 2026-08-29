using System.IO;
using System.Net.Http;
using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.App.Ocr;

/// <summary>
/// Downloads Tesseract LSTM <c>traineddata</c> into
/// <c>%LOCALAPPDATA%\SubtitleCompare\tessdata</c> on first use.
/// </summary>
internal static class TessdataStore
{
    private const string DownloadBase =
        "https://github.com/tesseract-ocr/tessdata/raw/main/";

    private static readonly HttpClient Http = CreateClient();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string AppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SubtitleCompare");

    public static string TessdataDirectory => Path.Combine(AppDataRoot, "tessdata");

    /// <summary>Parent of the tessdata folder — what <c>TesseractEngine</c> expects.</summary>
    public static string DataPrefix => AppDataRoot;

    public static async Task<string> EnsureLanguageAsync(
        string language,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        language = string.IsNullOrWhiteSpace(language) ? TessLanguage.Default : language.Trim();
        Directory.CreateDirectory(TessdataDirectory);

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasLanguage(language))
                return language;

            status?.Report($"Downloading {TessLanguage.DisplayName(language)} OCR data…");
            if (await TryDownloadAsync(language, cancellationToken).ConfigureAwait(false))
                return language;

            if (!string.Equals(language, TessLanguage.Default, StringComparison.OrdinalIgnoreCase))
            {
                if (HasLanguage(TessLanguage.Default))
                    return TessLanguage.Default;

                status?.Report("Downloading English OCR data…");
                if (await TryDownloadAsync(TessLanguage.Default, cancellationToken).ConfigureAwait(false))
                    return TessLanguage.Default;
            }

            throw new InvalidOperationException(
                "Could not download OCR language data. Check your internet connection and try again.");
        }
        finally
        {
            Gate.Release();
        }
    }

    public static bool HasLanguage(string language)
    {
        var path = LanguagePath(language);
        return File.Exists(path) && new FileInfo(path).Length > 50_000;
    }

    internal static string LanguagePath(string language) =>
        Path.Combine(TessdataDirectory, language + ".traineddata");

    private static async Task<bool> TryDownloadAsync(string language, CancellationToken cancellationToken)
    {
        var dest = LanguagePath(language);
        var tmp = dest + ".tmp";
        try
        {
            var url = DownloadBase + language + ".traineddata";
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = File.Create(tmp))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(tmp) || new FileInfo(tmp).Length < 50_000)
            {
                TryDelete(tmp);
                return false;
            }

            File.Move(tmp, dest, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            TryDelete(tmp);
            throw;
        }
        catch
        {
            TryDelete(tmp);
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromMinutes(2),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleCompare");
        return client;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}
