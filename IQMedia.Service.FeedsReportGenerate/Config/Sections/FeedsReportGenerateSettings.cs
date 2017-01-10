using System.Collections.Generic;

namespace IQMedia.Service.FeedsReportGenerate.Config.Sections
{
    public class FeedsReportGenerateSettings
    {
        public string PollIntervals { get; set; }
        public int QueueLimit { get; set; }
        public int NoOfTasks { get; set; }
        public int MaxTimeOut { get; set; }
        public string WCFServicePort { get; set; }
        public int ProcessBatchSize { get; set; }
    }
}
