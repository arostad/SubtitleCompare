using SubtitleCompare.Core.Diagnostics;

namespace SubtitleCompare.Tests;

public class AnonTests
{
    [Fact]
    public void Strips_windows_user_path_and_media_filename()
    {
        var text = Anon.Text(@"Could not open C:\Users\andy\Videos\The.Show.S01E02.1080p.mkv");

        Assert.DoesNotContain("andy", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The.Show", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".mkv", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<media>", text);
        Assert.Contains(@"C:\Users\<user>", text);
    }

    [Fact]
    public void Strips_localappdata_style_tesseract_path()
    {
        var text = Anon.Text(
            @"Unable to load DLL 'tesseract50'. C:\Users\andy\AppData\Local\SubtitleCompare\tesseract-native\x64\tesseract50.dll");

        Assert.DoesNotContain("andy", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%LOCALAPPDATA%", text);
        Assert.Contains("tesseract50.dll", text);
    }

    [Fact]
    public void Strips_mac_and_linux_home_paths()
    {
        var mac = Anon.Text("/Users/sam/Movies/Film.Title.mp4");
        Assert.DoesNotContain("sam", mac, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/Users/<user>", mac);
        Assert.Contains("<media>", mac);

        var linux = Anon.Text("/home/sam/videos/episode.srt");
        Assert.DoesNotContain("sam", linux, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/home/<user>", linux);
        Assert.Contains("<media>", linux);
    }

    [Fact]
    public void Strips_current_machine_identity()
    {
        var user = Environment.UserName;
        if (string.IsNullOrWhiteSpace(user) || user.Length < 2)
            return;

        var text = Anon.Text($"failed for user {user} on {Environment.MachineName}");
        Assert.DoesNotContain(user, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<user>", text);
    }

    [Fact]
    public void Strips_email_addresses()
    {
        var text = Anon.Text("SMTP failed for jane.doe@example.com while reading notes.srt");
        Assert.DoesNotContain("jane.doe@example.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<email>", text);
        Assert.Contains("<media>", text);
    }

    [Fact]
    public void Leaves_non_media_names_alone()
    {
        var text = Anon.Text("embedded tesseract50.dll and eng.traineddata are present");
        Assert.Contains("tesseract50.dll", text);
        Assert.Contains("eng.traineddata", text);
        Assert.DoesNotContain("<media>", text);
    }

    [Fact]
    public void Empty_and_null_are_safe()
    {
        Assert.Equal("", Anon.Text(null));
        Assert.Equal("", Anon.Text(""));
    }
}
