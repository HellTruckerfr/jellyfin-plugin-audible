using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Audible.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string Region { get; set; } = "fr";
    }
}
