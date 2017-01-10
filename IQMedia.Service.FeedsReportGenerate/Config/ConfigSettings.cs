using System.Configuration;
using IQMedia.Service.FeedsReportGenerate.Config.Sections;

namespace IQMedia.Service.FeedsReportGenerate.Config
{
    public sealed class ConfigSettings
    {
        private const string FEEDSREPORTGENERATE_SETTINGS = "FeedsReportGenerateSettings";        

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static FeedsReportGenerateSettings Settings
        {
            get { return ConfigurationManager.GetSection(FEEDSREPORTGENERATE_SETTINGS) as FeedsReportGenerateSettings; }
        }

    }
}
