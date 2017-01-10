using IQMedia.Service.DiscoveryExport.Config.Sections;
using System.Configuration;
namespace IQMedia.Service.DiscoveryExport.Config
{
    public sealed class ConfigSettings
    {
        private const string DISCOVERYEXPORT_SETTINGS = "DiscoveryExportSettings";

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static DiscoveryExportSettings Settings
        {
            get { return ConfigurationManager.GetSection(DISCOVERYEXPORT_SETTINGS) as DiscoveryExportSettings; }
        }
    }
}