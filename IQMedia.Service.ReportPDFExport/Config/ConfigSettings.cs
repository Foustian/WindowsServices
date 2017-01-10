using IQMedia.Service.ReportPDFExport.Config.Sections;
using System.Configuration;
namespace IQMedia.Service.ReportPDFExport.Config
{
    public sealed class ConfigSettings
    {
        private const string REPORTPDFEXPORT_SETTINGS = "ReportPDFExportSettings";

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static ReportPDFExportSettings Settings
        {
            get { return ConfigurationManager.GetSection(REPORTPDFEXPORT_SETTINGS) as ReportPDFExportSettings; }
        }
    }
}