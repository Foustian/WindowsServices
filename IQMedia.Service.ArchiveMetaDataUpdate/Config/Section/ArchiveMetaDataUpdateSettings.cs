using System.Collections.Generic;

namespace IQMedia.Service.ArchiveMetaDataUpdate.Config.Sections
{
    public class ArchiveMetaDataUpdateSettings
    {
        public string PollIntervals { get; set; }
        public int QueueLimit { get; set; }
        public int NoOfTasks { get; set; }
        public int MaxTimeOut { get; set; }
        public string WCFServicePort { get; set; }
    }
}
