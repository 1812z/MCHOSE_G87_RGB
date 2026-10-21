using Artemis.Core;
using Artemis.UI.Shared;
using Artemis.Plugins.MchoseG87.ViewModels;

namespace Artemis.Plugins.MchoseG87;

public sealed class G87Bootstrapper : PluginBootstrapper
{
    public override void OnPluginLoaded(Plugin plugin) =>
        plugin.ConfigurationDialog = new PluginConfigurationDialog<G87ConfigurationViewModel>();
}
