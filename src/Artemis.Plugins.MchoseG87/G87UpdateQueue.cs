using System.Diagnostics;
using RGB.NET.Core;
using Serilog;

namespace Artemis.Plugins.MchoseG87;

internal sealed class G87UpdateQueue : UpdateQueue
{
    private readonly object _sync = new();
    private readonly byte[] _rgb = new byte[G87Protocol.SlotCount * 3];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly G87Transport _transport;
    private readonly ILogger _logger;
    private readonly Func<int> _brightness;
    private readonly Func<int> _fps;
    private readonly Timer _timer;
    private double _lastSend = double.NegativeInfinity;
    private int _lastBrightness = -1;
    private bool _dirty;
    private bool _hasFrame;
    private bool _disposed;
    private bool _failed;

    public G87UpdateQueue(IDeviceUpdateTrigger trigger, G87Transport transport, ILogger logger, Func<int> brightness, Func<int> fps) : base(trigger)
    {
        _transport = transport;
        _logger = logger;
        _brightness = brightness;
        _fps = fps;
        _timer = new Timer(SendPending, null, 20, 20);
    }

    protected override bool Update(ReadOnlySpan<(object key, Color color)> dataSet)
    {
        lock (_sync)
        {
            if (_disposed) return false;
            foreach (var (key, color) in dataSet)
            {
                if (key is not int slot || slot < 0 || slot >= G87Protocol.SlotCount) continue;
                var (_, r, g, b) = color.GetRGBBytes();
                _rgb[slot * 3] = r;
                _rgb[slot * 3 + 1] = g;
                _rgb[slot * 3 + 2] = b;
            }
            _dirty = true;
            _hasFrame = true;
            return true;
        }
    }

    private void SendPending(object? state)
    {
        // No timer callback backlog; latest rendered frame replaces older data.
        if (!Monitor.TryEnter(_sync)) return;
        try
        {
            if (_disposed || !_hasFrame) return;
            int brightness = Math.Clamp(_brightness(), 0, 100);
            double elapsed = _clock.Elapsed.TotalMilliseconds - _lastSend;
            if (elapsed < 1000.0 / Math.Clamp(_fps(), 1, 30)) return;
            // Repeat static frames every second, before the ~3 second timeout.
            if (!_dirty && brightness == _lastBrightness && elapsed < 1000) return;
            _lastSend = _clock.Elapsed.TotalMilliseconds;
            try
            {
                _transport.Send(G87Protocol.EncodeWireless(G87Protocol.ScaleBrightness(_rgb, brightness)));
                _dirty = false;
                _lastBrightness = brightness;
                _failed = false;
            }
            catch (Exception ex)
            {
                if (!_failed) _logger.Warning(ex, "G87 wireless lighting write failed; reconnect receiver and toggle plugin");
                _failed = true;
            }
        }
        finally { Monitor.Exit(_sync); }
    }

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            try { _transport.Send(G87Protocol.Packetize([0, 0, 0, 1])); }
            catch (Exception ex) { _logger.Debug(ex, "Could not clear G87 lighting"); }
            finally { _transport.Dispose(); }
        }
    }
}
