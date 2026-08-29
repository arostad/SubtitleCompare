namespace SubtitleCompare.Core.Ocr;

/// <summary>
/// Forwards at most one report per interval. Immediate reports (and the first
/// report) go through right away. <see cref="Flush"/> always delivers the latest pending value.
/// </summary>
public sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _interval;
    private readonly Func<T, bool>? _immediate;
    private readonly object _gate = new();
    private T? _pending;
    private bool _hasPending;
    private DateTime _lastEmit = DateTime.MinValue;

    public ThrottledProgress(IProgress<T> inner, TimeSpan minInterval, Func<T, bool>? immediate = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (minInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minInterval));
        _inner = inner;
        _interval = minInterval;
        _immediate = immediate;
    }

    public void Report(T value)
    {
        T emit;
        lock (_gate)
        {
            if (!ShouldEmitNow(value))
            {
                _pending = value;
                _hasPending = true;
                return;
            }

            _lastEmit = DateTime.UtcNow;
            _hasPending = false;
            emit = value;
        }

        _inner.Report(emit);
    }

    public void Flush()
    {
        T emit;
        lock (_gate)
        {
            if (!_hasPending)
                return;
            emit = _pending!;
            _hasPending = false;
            _lastEmit = DateTime.UtcNow;
        }

        _inner.Report(emit);
    }

    private bool ShouldEmitNow(T value) =>
        _immediate?.Invoke(value) == true || DateTime.UtcNow - _lastEmit >= _interval;
}
