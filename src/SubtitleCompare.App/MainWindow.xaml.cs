using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SubtitleCompare.Core.Alignment;
using SubtitleCompare.Core.Diff;
using SubtitleCompare.Core.Ffmpeg;
using SubtitleCompare.Core.Models;
using SubtitleCompare.Core.Parsing;

namespace SubtitleCompare.App;

public partial class MainWindow : Window
{
    private static readonly string[] MediaExtensions = [".mkv", ".mka", ".mks", ".mp4", ".m4v"];

    private readonly ComboBox[] _slots;
    private readonly Border[] _overlays;
    private readonly TextBlock[] _overlayTexts;

    private TempSession? _temp;
    private FfmpegExtract? _extractor;
    private FfmpegProbe _probe = new();
    private string? _currentFile;
    private IReadOnlyList<SubtitleTrackInfo> _tracks = Array.Empty<SubtitleTrackInfo>();
    private readonly ParsedSubtitles?[] _parsed = new ParsedSubtitles?[3];
    private IReadOnlyList<AlignedRow> _rows = Array.Empty<AlignedRow>();
    private readonly List<Border[]> _rowBorders = new();
    private readonly List<bool> _rowIsDiff = new();
    private int _selectedRow = -1;
    private int _loadGeneration;
    private int _refreshGeneration;
    private bool _suppressSlotEvents;

    public MainWindow()
    {
        InitializeComponent();
        _slots = [SlotA, SlotB, SlotC];
        _overlays = [OverlayA, OverlayB, OverlayC];
        _overlayTexts = [OverlayAText, OverlayBText, OverlayCText];

        foreach (var box in _slots)
            box.DisplayMemberPath = nameof(TrackChoice.Label);

        Theme.Changed += OnThemeChanged;

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]) && IsSupportedMedia(args[1]))
            _ = LoadFileAsync(args[1]);
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Theme.ApplyCaption(this);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Theme.ApplyCaption(this);
        if (_rows.Count > 0)
            RebuildCompare();
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
        Theme.Changed -= OnThemeChanged;
        ResetSession();
        TempSession.TryDeleteAllSessions();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnOpenClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F7)
        {
            OnPrevDiffClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F8)
        {
            OnNextDiffClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedFile(e, out var path) && path is not null)
            _ = LoadFileAsync(path);
        e.Handled = true;
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open media file",
            Filter = "Matroska / media|*.mkv;*.mka;*.mks;*.mp4;*.m4v|All files|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            _ = LoadFileAsync(dlg.FileName);
    }

    private async Task LoadFileAsync(string path)
    {
        var gen = ++_loadGeneration;
        ResetSession();
        _temp = new TempSession();
        _extractor = new FfmpegExtract(_temp.Root);
        _currentFile = path;

        EmptyState.Visibility = Visibility.Collapsed;
        LoadedState.Visibility = Visibility.Visible;
        HideOverlays();
        CompareGrid.Children.Clear();
        CompareGrid.RowDefinitions.Clear();
        SetBanner(null);
        Title = $"Subtitle Compare — {Path.GetFileName(path)}";
        StatusFile.Text = Path.GetFileName(path);
        StatusExtract.Text = "Probing subtitle tracks…";
        StatusError.Text = "";

        IReadOnlyList<SubtitleTrackInfo> tracks;
        try
        {
            tracks = await Task.Run(() => _probe.Probe(path)).ConfigureAwait(true);
        }
        catch (FfmpegNotFoundException ex)
        {
            if (gen != _loadGeneration) return;
            SetBanner(ex.Message);
            StatusExtract.Text = "ffprobe not found";
            StatusError.Text = ex.Message;
            ShowEmptyAfterFailure();
            return;
        }
        catch (Exception ex)
        {
            if (gen != _loadGeneration) return;
            StatusExtract.Text = "Probe failed";
            StatusError.Text = ex.Message;
            ShowEmptyAfterFailure();
            return;
        }

        if (gen != _loadGeneration) return;
        _tracks = tracks;

        if (_tracks.Count == 0)
        {
            StatusExtract.Text = "No subtitle tracks";
            StatusError.Text = "This file has no subtitle streams.";
            _suppressSlotEvents = true;
            PopulateEmptyCombos();
            _suppressSlotEvents = false;
            SetOverlay(0, "No subtitle tracks in this file.");
            SetOverlay(1, "No subtitle tracks in this file.");
            SetOverlay(2, "No subtitle tracks in this file.");
            PrevDiffButton.IsEnabled = false;
            NextDiffButton.IsEnabled = false;
            return;
        }

        _suppressSlotEvents = true;
        PopulateCombos();
        SelectDefaultTracks();
        _suppressSlotEvents = false;
        StatusExtract.Text = $"{_tracks.Count} subtitle track{(_tracks.Count == 1 ? "" : "s")}";
        await RefreshSlotsAsync(gen);
    }

    private void PopulateCombos()
    {
        var items = new List<TrackChoice> { TrackChoice.None };
        items.AddRange(_tracks.Select(t => new TrackChoice(t)));
        foreach (var box in _slots)
        {
            box.ItemsSource = null;
            box.ItemsSource = items;
            box.SelectedIndex = 0;
        }
    }

    private void PopulateEmptyCombos()
    {
        var items = new List<TrackChoice> { TrackChoice.None };
        foreach (var box in _slots)
        {
            box.ItemsSource = items;
            box.SelectedIndex = 0;
        }
    }

    private void SelectDefaultTracks()
    {
        var text = _tracks.Where(t => !t.IsImageBased).Take(3).ToList();
        for (var i = 0; i < 3; i++)
        {
            if (i < text.Count)
            {
                var match = _slots[i].Items.OfType<TrackChoice>().FirstOrDefault(c => c.Track == text[i]);
                _slots[i].SelectedItem = match ?? TrackChoice.None;
            }
            else
            {
                _slots[i].SelectedIndex = 0;
            }
        }
    }

    private async void OnSlotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSlotEvents || _currentFile is null)
            return;
        await RefreshSlotsAsync(_loadGeneration);
    }

    private async Task RefreshSlotsAsync(int gen)
    {
        if (_currentFile is null || _extractor is null)
            return;

        var refresh = ++_refreshGeneration;
        StatusError.Text = "";
        var tasks = new List<Task>();
        for (var pane = 0; pane < 3; pane++)
            tasks.Add(LoadPaneAsync(pane, gen, refresh));

        await Task.WhenAll(tasks);
        if (gen != _loadGeneration || refresh != _refreshGeneration) return;
        RebuildCompare();
    }

    private async Task LoadPaneAsync(int pane, int gen, int refresh)
    {
        var choice = _slots[pane].SelectedItem as TrackChoice;
        if (refresh == _refreshGeneration)
            _parsed[pane] = null;

        if (choice is null || choice.IsNone)
        {
            SetOverlay(pane, null);
            return;
        }

        if (choice.IsImage)
        {
            SetOverlay(pane, "Image subtitles (PGS/VobSub) can't be compared as text.");
            return;
        }

        SetOverlay(pane, "Extracting…");
        Dispatcher.Invoke(() => StatusExtract.Text = $"Extracting track {choice.Track!.Index + 1}…");

        var file = _currentFile!;
        var index = choice.Track!.Index;
        var extractor = _extractor!;

        try
        {
            var path = await Task.Run(() => extractor.Extract(file, index)).ConfigureAwait(true);
            if (gen != _loadGeneration || refresh != _refreshGeneration) return;
            var parsed = await Task.Run(() => SubtitleParser.ParseFile(path)).ConfigureAwait(true);
            if (gen != _loadGeneration || refresh != _refreshGeneration) return;
            _parsed[pane] = parsed;
            SetOverlay(pane, parsed.Cues.Count == 0 ? "This track has no cues." : null);
            Dispatcher.Invoke(() => StatusExtract.Text = "Ready");
        }
        catch (FfmpegNotFoundException ex)
        {
            if (gen != _loadGeneration || refresh != _refreshGeneration) return;
            SetBanner(ex.Message);
            SetOverlay(pane, ex.Message);
            Dispatcher.Invoke(() =>
            {
                StatusExtract.Text = "ffmpeg not found";
                StatusError.Text = ex.Message;
            });
        }
        catch (Exception ex)
        {
            if (gen != _loadGeneration || refresh != _refreshGeneration) return;
            SetOverlay(pane, ex.Message);
            Dispatcher.Invoke(() =>
            {
                StatusExtract.Text = "Extract failed";
                StatusError.Text = ex.Message;
            });
        }
    }

    private void RebuildCompare()
    {
        CompareGrid.Children.Clear();
        CompareGrid.RowDefinitions.Clear();
        _rowBorders.Clear();
        _rowIsDiff.Clear();
        _selectedRow = -1;

        var cuesA = _parsed[0]?.Cues;
        var cuesB = _parsed[1]?.Cues;
        var cuesC = _parsed[2]?.Cues;
        var active = new[]
        {
            _parsed[0] is not null,
            _parsed[1] is not null,
            _parsed[2] is not null,
        };

        if (!active[0] && !active[1] && !active[2])
        {
            PrevDiffButton.IsEnabled = false;
            NextDiffButton.IsEnabled = false;
            return;
        }

        _rows = CueAligner.Align(cuesA, cuesB, cuesC);

        for (var i = 0; i < _rows.Count; i++)
        {
            CompareGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = _rows[i];
            var texts = new string?[3];
            var present = new bool[3];
            for (var p = 0; p < 3; p++)
            {
                if (!active[p])
                    continue;
                var cue = row[p];
                present[p] = cue is not null;
                texts[p] = cue?.Text;
            }

            var selectedTexts = Enumerable.Range(0, 3)
                .Where(p => active[p] && present[p])
                .Select(p => texts[p] ?? "")
                .ToArray();

            IReadOnlyList<IReadOnlyList<DiffSegment>>? diffs = null;
            if (selectedTexts.Length >= 2)
                diffs = TextDiffer.Compare(selectedTexts);

            var diffByPane = new IReadOnlyList<DiffSegment>?[3];
            if (diffs is not null)
            {
                var di = 0;
                for (var p = 0; p < 3; p++)
                {
                    if (active[p] && present[p])
                        diffByPane[p] = diffs[di++];
                }
            }

            var anyMissing = active.Select((on, p) => on && !present[p] && active.Count(x => x) > 1).Any(x => x);
            var textDiffers = diffs is not null && TextDiffer.RowHasDifference(diffs);
            var isDiff = textDiffers || anyMissing || (active.Count(x => x) > 1 && present.Count(x => x) == 1);
            _rowIsDiff.Add(isDiff);

            var borders = new Border[3];
            for (var p = 0; p < 3; p++)
            {
                var cell = BuildCell(row, p, active[p], present[p], diffByPane[p], i, isDiff);
                Grid.SetRow(cell, i);
                Grid.SetColumn(cell, p * 2);
                CompareGrid.Children.Add(cell);
                borders[p] = cell;
            }

            // gutters
            for (var g = 0; g < 2; g++)
            {
                var gutter = new Border { Background = Theme.Get("GutterBg") };
                Grid.SetRow(gutter, i);
                Grid.SetColumn(gutter, g * 2 + 1);
                CompareGrid.Children.Add(gutter);
            }

            _rowBorders.Add(borders);
        }

        var diffsCount = _rowIsDiff.Count(x => x);
        PrevDiffButton.IsEnabled = diffsCount > 0;
        NextDiffButton.IsEnabled = diffsCount > 0;
        StatusExtract.Text = $"{_rows.Count} aligned row{(_rows.Count == 1 ? "" : "s")}, {diffsCount} difference{(diffsCount == 1 ? "" : "s")}";
    }

    private Border BuildCell(
        AlignedRow row,
        int pane,
        bool active,
        bool present,
        IReadOnlyList<DiffSegment>? segments,
        int rowIndex,
        bool isDiff)
    {
        var bg = rowIndex % 2 == 0 ? Theme.Get("RowBg") : Theme.Get("AltRowBg");
        var border = new Border
        {
            Background = bg,
            Padding = new Thickness(10, 7, 10, 8),
            BorderBrush = isDiff ? Theme.Get("DiffAccent") : Theme.Get("GutterBg"),
            BorderThickness = isDiff ? new Thickness(3, 0, 0, 1) : new Thickness(0, 0, 0, 1),
            MinHeight = 36,
            Cursor = Cursors.Hand,
            Tag = rowIndex,
        };

        if (!active)
        {
            border.Background = Theme.Get("GutterBg");
            return border;
        }

        if (!present)
        {
            border.Background = Theme.Get("MissingBg");
            border.Padding = new Thickness(0);
            var accent = new Border
            {
                Width = 3,
                Background = Theme.Get("MissingAccent"),
            };
            var missing = new TextBlock
            {
                Text = "no cue",
                Foreground = Theme.Get("MissingFg"),
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 8, 10, 8),
            };
            var dock = new DockPanel();
            DockPanel.SetDock(accent, Dock.Left);
            dock.Children.Add(accent);
            dock.Children.Add(missing);
            border.Child = dock;
            border.MouseLeftButtonDown += OnRowClicked;
            return border;
        }

        var cue = row[pane]!;
        var stack = new StackPanel();
        var stamp = FormatTimestamp(cue.Start, cue.End);
        stack.Children.Add(new TextBlock
        {
            Text = stamp,
            FontSize = 11,
            Foreground = Theme.Get("TimestampFg"),
            Margin = new Thickness(0, 0, 0, 2),
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
        });

        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            LineHeight = 20,
        };

        if (segments is null || segments.Count == 0)
        {
            body.Text = cue.Text;
            body.Foreground = Theme.Get("EqualFg");
        }
        else
        {
            foreach (var seg in segments)
            {
                var run = new Run(seg.Text) { Foreground = Theme.Get("EqualFg") };
                run.Background = seg.Kind switch
                {
                    DiffKind.Unique => Theme.Get("UniqueBg"),
                    DiffKind.Changed => Theme.Get("ChangedBg"),
                    _ => Brushes.Transparent,
                };
                body.Inlines.Add(run);
            }
        }

        stack.Children.Add(body);
        border.Child = stack;
        border.MouseLeftButtonDown += OnRowClicked;
        return border;
    }

    private void OnRowClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: int index })
            SelectRow(index, scrollIntoView: false);
    }

    private void SelectRow(int index, bool scrollIntoView)
    {
        if (index < 0 || index >= _rowBorders.Count)
            return;

        if (_selectedRow >= 0 && _selectedRow < _rowBorders.Count)
        {
            var previous = _rowBorders[_selectedRow];
            var prevBg = _selectedRow % 2 == 0 ? Theme.Get("RowBg") : Theme.Get("AltRowBg");
            var wasDiff = _rowIsDiff[_selectedRow];
            for (var p = 0; p < 3; p++)
            {
                var wasMissing = IsMissingCell(_selectedRow, p);
                previous[p].Background = wasMissing ? Theme.Get("MissingBg") : prevBg;
                previous[p].BorderBrush = wasDiff ? Theme.Get("DiffAccent") : Theme.Get("GutterBg");
                previous[p].BorderThickness = wasDiff ? new Thickness(3, 0, 0, 1) : new Thickness(0, 0, 0, 1);
            }
        }

        _selectedRow = index;
        foreach (var cell in _rowBorders[index])
        {
            cell.Background = Theme.Get("SelectedBg");
            cell.BorderBrush = Theme.Get("SelectedBorder");
            cell.BorderThickness = new Thickness(0, 0, 0, 2);
        }

        if (scrollIntoView)
            _rowBorders[index][FirstActivePane()].BringIntoView();
    }

    private bool IsMissingCell(int row, int pane)
    {
        var choice = _slots[pane].SelectedItem as TrackChoice;
        if (choice is null || choice.IsNone || choice.IsImage)
            return false;
        return _rows[row][pane] is null;
    }

    private int FirstActivePane()
    {
        for (var p = 0; p < 3; p++)
        {
            if (_parsed[p] is not null)
                return p;
        }
        return 0;
    }

    private void OnPrevDiffClick(object sender, RoutedEventArgs e) => JumpDifference(-1);

    private void OnNextDiffClick(object sender, RoutedEventArgs e) => JumpDifference(1);

    private void JumpDifference(int direction)
    {
        if (_rowIsDiff.Count == 0 || !_rowIsDiff.Contains(true))
            return;

        var start = _selectedRow;
        if (start < 0)
            start = direction > 0 ? -1 : _rowIsDiff.Count;

        var i = start;
        for (var n = 0; n < _rowIsDiff.Count; n++)
        {
            i += direction;
            if (i < 0) i = _rowIsDiff.Count - 1;
            if (i >= _rowIsDiff.Count) i = 0;
            if (_rowIsDiff[i])
            {
                SelectRow(i, scrollIntoView: true);
                return;
            }
        }
    }

    private void SetOverlay(int pane, string? message)
    {
        Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _overlays[pane].Visibility = Visibility.Collapsed;
                _overlayTexts[pane].Text = "";
            }
            else
            {
                _overlayTexts[pane].Text = message;
                _overlays[pane].Visibility = Visibility.Visible;
            }
        });
    }

    private void HideOverlays()
    {
        foreach (var o in _overlays)
            o.Visibility = Visibility.Collapsed;
    }

    private void SetBanner(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            FfmpegBanner.Visibility = Visibility.Collapsed;
            FfmpegBannerText.Text = "";
            return;
        }

        FfmpegBannerText.Text = message;
        FfmpegBanner.Visibility = Visibility.Visible;
    }

    private void ShowEmptyAfterFailure()
    {
        Title = "Subtitle Compare";
        EmptyState.Visibility = Visibility.Visible;
        LoadedState.Visibility = Visibility.Collapsed;
    }

    private void ResetSession()
    {
        _extractor = null;
        _temp?.Dispose();
        _temp = null;
        Array.Clear(_parsed);
        _tracks = Array.Empty<SubtitleTrackInfo>();
        _rows = Array.Empty<AlignedRow>();
        _rowBorders.Clear();
        _rowIsDiff.Clear();
        _selectedRow = -1;
    }

    private static bool TryGetDroppedFile(DragEventArgs e, out string? path)
    {
        path = null;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return false;
        var file = files[0];
        if (!IsSupportedMedia(file))
            return false;
        path = file;
        return true;
    }

    private static bool IsSupportedMedia(string path)
    {
        var ext = Path.GetExtension(path);
        return MediaExtensions.Any(x => x.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatTimestamp(TimeSpan start, TimeSpan end) =>
        $"{FormatTs(start)}  →  {FormatTs(end)}";

    private static string FormatTs(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        var h = (int)t.TotalHours;
        return h > 0
            ? $"{h:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}"
            : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
    }

}
