using System.Collections.Generic;

namespace IQMedia.Service.DiscoveryArchiveMetaDataUpdate.Config.Sections
{
    public class DiscoveryArchiveMetaDataUpdateSettings
    {
        public string PollIntervals { get; set; }
        public int QueueLimit { get; set; }
        public int NoOfTasks { get; set; }
        public int MaxTimeOut { get; set; }
        public string WCFServicePort { get; set; }
    }
}
