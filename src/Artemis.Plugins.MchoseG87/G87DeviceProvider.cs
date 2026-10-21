using Artemis.Core;
using Artemis.Core.DeviceProviders;
using Artemis.Core.Services;
using HidSharp;
using RGB.NET.Core;
using Serilog;

namespace Artemis.Plugins.MchoseG87;

[PluginFeature(Name = "MCHOSE G87 Wireless", Description = "2.4 GHz lighting; calibrated 87-key mapping.")]
public sealed class G87DeviceProvider : DeviceProvider
{
    private readonly ILogger _logger;
    private readonly IDeviceService _devices;
    private G87RgbProvider _provider;
    private readonly PluginSetting<int> _brightness;
    private readonly PluginSetting<int> _fps;
    public override IRGBDeviceProvider RgbDeviceProvider => _provider;

    public G87DeviceProvider(ILogger logger, IDeviceService deviceService, PluginSettings settings)
    {
        _logger = logger;
        _devices = deviceService;
        _brightness = settings.GetSetting("BrightnessPercent", 100);
        _fps = settings.GetSetting("FramesPerSecond", 9);
        _provider = new G87RgbProvider(logger, () => _brightness.Value, () => _fps.Value);
        CreateMissingLedsSupported = false;
        RemoveExcessiveLedsSupported = false;
        SuspendSupported = true;
    }
    public override void Enable()
    {
        _provider = new G87RgbProvider(_logger, () => _brightness.Value, () => _fps.Value);
        _devices.AddDeviceProvider(this);
    }
    public override void Disable()
    {
        _devices.RemoveDeviceProvider(this);
        _provider.Dispose();
    }
}

internal sealed class G87RgbProvider(ILogger logger, Func<int> brightness, Func<int> fps) : AbstractRGBDeviceProvider
{
    protected override void InitializeSDK() { }
    protected override IEnumerable<IRGBDevice> LoadDevices()
    {
        foreach (var hid in DeviceList.Local.GetHidDevices(0x41e4, 0x2001))
        {
            G87UpdateQueue? queue = null;
            try
            {
                if (!G87Transport.Matches(hid)) continue;
                var transport = new G87Transport(hid);
                try { queue = new G87UpdateQueue(GetUpdateTrigger(), transport, logger, brightness, fps); }
                catch { transport.Dispose(); throw; }
                logger.Information("G87 wireless lighting endpoint opened: {Path}", hid.DevicePath);
            }
            catch (Exception ex) { logger.Warning(ex, "Could not open G87 receiver {Path}", hid.DevicePath); }
            if (queue != null) yield return new G87Keyboard(queue);
        }
    }
}
