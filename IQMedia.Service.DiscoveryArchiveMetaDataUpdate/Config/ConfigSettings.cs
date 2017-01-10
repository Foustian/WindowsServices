using System.Configuration;
using IQMedia.Service.DiscoveryArchiveMetaDataUpdate.Config.Sections;

namespace IQMedia.Service.DiscoveryArchiveMetaDataUpdate.Config
{
    public sealed class ConfigSettings
    {
        private const string DISCOVERYARCHIVEMETADATAUPDATE_SETTINGS = "DiscoveryArchiveMetaDataUpdateSettings";

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static DiscoveryArchiveMetaDataUpdateSettings Settings
        {
            get { return ConfigurationManager.GetSection(DISCOVERYARCHIVEMETADATAUPDATE_SETTINGS) as DiscoveryArchiveMetaDataUpdateSettings; }
        }

    }
}
