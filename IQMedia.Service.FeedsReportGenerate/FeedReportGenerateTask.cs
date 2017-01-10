using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;


namespace IQMedia.Service.FeedsReportGenerate
{
    public class FeedReportGenerateTask : IEquatable<FeedReportGenerateTask>
    {
        public Guid clientGUID { get; set; }
        public Guid customerGUID { get; set; }
        public Int64 ID { get; set; }
        public string MediaID { get; set; }
        public TskStatus Status { get; set; }

        public enum TskStatus
        {
            EXCEPTION,
            FAILED,
            IN_PROCESS,
            READY_FOR_METADATA
        }

        #region IEquatable<FeedReportGenerateTask> Members

        public bool Equals(FeedReportGenerateTask other)
        {
            return ID.Equals(other.ID);
        }

        #endregion
    }
}
