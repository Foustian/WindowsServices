using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.DiscoveryArchiveMetaDataUpdate
{
    public class DiscoveryArchiveMetaDataUpdateTask : IEquatable<DiscoveryArchiveMetaDataUpdateTask>
    {

        public Int64 ID { get; set; }
        public string ArchiveTracking { get; set; }
        public TskStatus Status { get; set; }

        public enum TskStatus
        {
            COMPLETED,
            EXCEPTION_METADATA,
            METADATA_IN_PROCESS,
            TIMEOUT_METADATA
        }


        #region IEquatable<FeedReportGenerateTask> Members

        public bool Equals(DiscoveryArchiveMetaDataUpdateTask other)
        {
            return ID.Equals(other.ID);
        }

        #endregion

    }
}
