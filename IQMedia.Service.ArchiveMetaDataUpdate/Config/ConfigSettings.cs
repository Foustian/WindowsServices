using System.Configuration;
using IQMedia.Service.ArchiveMetaDataUpdate.Config.Sections;

namespace IQMedia.Service.ArchiveMetaDataUpdate.Config
{
    public sealed class ConfigSettings
    {
        private const string ARCHIVEMETADATAUPDATE_SETTINGS = "ArchiveMetaDataUpdateSettings";

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static ArchiveMetaDataUpdateSettings Settings
        {
            get { return ConfigurationManager.GetSection(ARCHIVEMETADATAUPDATE_SETTINGS) as ArchiveMetaDataUpdateSettings; }
        }

    }
}
