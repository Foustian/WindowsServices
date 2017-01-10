using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.DiscoveryReportGenerate
{
    public class DiscoveryReportGenerateTask : IEquatable<DiscoveryReportGenerateTask>
    {
        public Int64 ID { get; set; }
        public string MediaID { get; set; }
        public Guid ClientGuid { get; set; }
        public Guid CustomerGuid { get; set; }
        public TskStatus Status { get; set; }

        public enum TskStatus
        {
            EXCEPTION,
            FAILED,
            IN_PROCESS,
            READY_FOR_METADATA
        }

        #region IEquatable<DiscoveryReportGenerateTask> Members

        public bool Equals(DiscoveryReportGenerateTask other)
        {
            return ID.Equals(other.ID);
        }

        #endregion
    }
}
