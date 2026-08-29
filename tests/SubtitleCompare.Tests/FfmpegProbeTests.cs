using SubtitleCompare.Core.Ffmpeg;
using SubtitleCompare.Core.Language;

namespace SubtitleCompare.Tests;

public class FfmpegProbeTests
{
    private const string SampleJson = """
        {
          "streams": [
            {
              "index": 2,
              "codec_name": "subrip",
              "codec_long_name": "SubRip subtitle",
              "codec_type": "subtitle",
              "disposition": { "forced": 0, "hearing_impaired": 1 },
              "tags": { "language": "eng", "title": "English SDH" }
            },
            {
              "index": 3,
              "codec_name": "hdmv_pgs_subtitle",
              "codec_long_name": "HDMV Presentation Graphic Stream subtitles",
              "codec_type": "subtitle",
              "disposition": { "forced": 1, "hearing_impaired": 0 },
              "tags": { "language": "jpn", "title": "Signs" }
            }
          ]
        }
        """;

    [Fact]
    public void ParseJson_maps_real_ffprobe_fields()
    {
        var tracks = FfmpegProbe.ParseJson(SampleJson);
        Assert.Equal(2, tracks.Count);

        Assert.Equal(0, tracks[0].Index);
        Assert.Equal(2, tracks[0].StreamIndex);
        Assert.Equal("eng", tracks[0].Language);
        Assert.Equal("English SDH", tracks[0].Title);
        Assert.Equal("subrip", tracks[0].CodecName);
        Assert.False(tracks[0].IsForced);
        Assert.True(tracks[0].IsHearingImpaired);
        Assert.False(tracks[0].IsImageBased);

        Assert.Equal(1, tracks[1].Index);
        Assert.Equal(3, tracks[1].StreamIndex);
        Assert.Equal("jpn", tracks[1].Language);
        Assert.True(tracks[1].IsForced);
        Assert.True(tracks[1].IsImageBased);
        Assert.False(tracks[0].IsPgs);
        Assert.True(tracks[1].IsPgs);
    }

    [Fact]
    public void Text_in_pgs_codec_is_not_ocrable_pgs()
    {
        var json = """
            {"streams":[{"index":4,"codec_name":"hdmv_text_subtitle","codec_type":"subtitle"}]}
            """;
        var tracks = FfmpegProbe.ParseJson(json);
        Assert.True(tracks[0].IsImageBased);
        Assert.False(tracks[0].IsPgs);
    }

    [Fact]
    public void Track_label_uses_1_based_index_and_flags()
    {
        var tracks = FfmpegProbe.ParseJson(SampleJson);
        Assert.Equal("1 — English (subrip) \"English SDH\" [SDH]", TrackLabelFormatter.Format(tracks[0]));
        Assert.Equal("2 — Japanese (hdmv_pgs_subtitle) \"Signs\" [forced]", TrackLabelFormatter.Format(tracks[1]));
    }
}
