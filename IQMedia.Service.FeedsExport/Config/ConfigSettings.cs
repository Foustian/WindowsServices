using IQMedia.Service.FeedsExport.Config.Sections;
using System.Configuration;
namespace IQMedia.Service.FeedsExport.Config
{
    public sealed class ConfigSettings
    {
        private const string FEEDSEXPORT_SETTINGS = "FeedsExportSettings";

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static FeedsExportSettings Settings
        {
            get { return ConfigurationManager.GetSection(FEEDSEXPORT_SETTINGS) as FeedsExportSettings; }
        }
    }
}