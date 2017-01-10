using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IQMedia.Service.DiscoveryReportGenerate.Config.Sections;
using System.Configuration;

namespace IQMedia.Service.DiscoveryReportGenerate.Config
{
    public sealed class ConfigSettings
    {
        private const string DISCOVERYREPORTGENERATE_SETTINGS = "DiscoveryReportGenerateSettings";

        /// <summary>
        /// The Singleton instance of the ExportSettings ConfigSection
        /// </summary>
        public static DiscoveryReportGenerateSettings Settings
        {
            get { return ConfigurationManager.GetSection(DISCOVERYREPORTGENERATE_SETTINGS) as DiscoveryReportGenerateSettings; }
        }
    }
}
