using System;

namespace IQMedia.Service.ReportPDFExport
{
    class ReportPDFExportTask
    {
        private Int64 _ID;
        public Int64 ID { get { return _ID; } }

        private Guid _CustomerGUID;
        public Guid CustomerGUID { get { return _CustomerGUID; } }

        private string _BaseUrl;
        public string BaseUrl { get { return _BaseUrl; } }

        private string _HTMLFilename;
        public string HTMLFilename { get { return _HTMLFilename; } }

        private int _RootPathID;
        public int RootPathID { get { return _RootPathID; } }

        private DateTime _CreatedDate;
        public DateTime CreatedDate { get { return _CreatedDate; } }

        private string _ReportTitle;
        public string ReportTitle { get { return _ReportTitle; } }

        public TskStatus Status { get; set; }

        public string DownloadPath { get; set; }

        internal ReportPDFExportTask(Int64 p_ID, Guid p_CustomerGUID, string p_BaseUrl, string p_HTMLFilename, int p_RootPathID, DateTime p_CreatedDate, string p_ReportTitle)
        {
            _ID = p_ID;
            _CustomerGUID = p_CustomerGUID;
            _BaseUrl = p_BaseUrl;
            _HTMLFilename = p_HTMLFilename;
            _RootPathID = p_RootPathID;
            _CreatedDate = p_CreatedDate;
            _ReportTitle = p_ReportTitle;
        }

        public enum TskStatus
        {
            COMPLETED,
            FAILED,
            FAILED_DIRECTORY_CREATION,
            FAILED_FILE_CREATION,
            FAILED_TSK_CANCELLED,
            FAILED_UPDATE_PATH,
            FAILED_READ_HTML,
            IN_PROCESS
        }
    }
}
