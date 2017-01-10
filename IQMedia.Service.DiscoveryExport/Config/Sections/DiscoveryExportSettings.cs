using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.DiscoveryExport.Config.Sections
{
    public class DiscoveryExportSettings
    {
        public int MaxTimeOut { get; set; }

        public int NoOfTasks { get; set; }

        public string PollIntervals { get; set; }

        public int QueueLimit { get; set; }

        public int WorkerThreads { get; set; }

    }
}
