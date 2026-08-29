using SubtitleCompare.Core.Ocr;

namespace SubtitleCompare.Tests;

public class TessLanguageTests
{
    [Theory]
    [InlineData(null, "eng")]
    [InlineData("", "eng")]
    [InlineData("und", "eng")]
    [InlineData("eng", "eng")]
    [InlineData("en", "eng")]
    [InlineData("spa", "spa")]
    [InlineData("es", "spa")]
    [InlineData("fre", "fra")]
    [InlineData("ger", "deu")]
    [InlineData("jpn", "jpn")]
    [InlineData("chi", "chi_sim")]
    [InlineData("zh-Hant", "chi_tra")]
    [InlineData("zh-TW", "chi_tra")]
    [InlineData("zh-CN", "chi_sim")]
    [InlineData("pob", "por")]
    [InlineData("yue", "chi_tra")]
    public void FromTag_maps_or_falls_back(string? tag, string expected)
    {
        Assert.Equal(expected, TessLanguage.FromTag(tag));
    }

    [Fact]
    public void DisplayName_is_readable()
    {
        Assert.Equal("English", TessLanguage.DisplayName("eng"));
        Assert.Equal("Japanese", TessLanguage.DisplayName("jpn"));
        Assert.Equal("Chinese (Traditional)", TessLanguage.DisplayName("chi_tra"));
    }
}
