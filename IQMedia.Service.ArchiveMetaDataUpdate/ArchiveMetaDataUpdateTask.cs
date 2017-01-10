using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.ArchiveMetaDataUpdate
{
    public class ArchiveMetaDataUpdateTask : IEquatable<ArchiveMetaDataUpdateTask>
    {
        public Int64 ID { get; set; }
        public string ArchiveTracking { get; set; }
        public TskStatus Status { get; set; }

        public enum TskStatus
        {
            COMPLETED,
            EXCEPTION_METADATA,
            METADATA_IN_PROCESS,
            FAILED,
            TIMEOUT_METADATA
        }

        #region IEquatable<FeedReportGenerateTask> Members

        public bool Equals(ArchiveMetaDataUpdateTask other)
        {
            return ID.Equals(other.ID);
        }

        #endregion
    }
}
