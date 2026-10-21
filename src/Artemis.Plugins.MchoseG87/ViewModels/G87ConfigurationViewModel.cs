using Artemis.Core;
using Artemis.UI.Shared;

namespace Artemis.Plugins.MchoseG87.ViewModels;

public sealed class G87ConfigurationViewModel : PluginConfigurationViewModel
{
    public PluginSetting<int> Brightness { get; }
    public PluginSetting<int> FramesPerSecond { get; }
    public G87ConfigurationViewModel(Plugin plugin, PluginSettings settings) : base(plugin)
    {
        Brightness = settings.GetSetting("BrightnessPercent", 100);
        FramesPerSecond = settings.GetSetting("FramesPerSecond", 9);
    }
    public void SetBrightness(int value)
    {
        Brightness.Value = Math.Clamp(value, 0, 100);
        Brightness.Save();
    }
    public void SetFramesPerSecond(int value)
    {
        FramesPerSecond.Value = Math.Clamp(value, 1, 30);
        FramesPerSecond.Save();
    }
}
