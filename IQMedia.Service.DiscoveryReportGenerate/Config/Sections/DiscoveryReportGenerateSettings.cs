using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.DiscoveryReportGenerate.Config.Sections
{
    public class DiscoveryReportGenerateSettings
    {
        public string PollIntervals { get; set; }
        public int QueueLimit { get; set; }
        public int NoOfTasks { get; set; }
        public int MaxTimeOut { get; set; }
        public string WCFServicePort { get; set; }
        public int ProcessBatchSize { get; set; }
    }
}
