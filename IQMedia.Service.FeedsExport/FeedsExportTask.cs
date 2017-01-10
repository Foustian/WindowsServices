using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.FeedsExport
{
    class FeedsExportTask
    {
        private Int64 _ID;
        public Int64 ID { get { return _ID; } }

        private Guid _CustomerGUID;
        public Guid CustomerGUID { get { return _CustomerGUID; } }

        private string _SearchCriteria;
        public string SearchCriteria { get { return _SearchCriteria; } }

        private string _ArticleXml;
        public string ArticleXml { get { return _ArticleXml; } }

        private string _SortType;
        public string SortType { get { return _SortType; } }

        private int _RootPathID;
        public int RootPathID { get { return _RootPathID; } }

        private bool _IsSelectAll;
        public bool IsSelectAll { get { return _IsSelectAll; } }

        private DateTime _CreatedDate;
        public DateTime CreatedDate { get { return _CreatedDate; } }

        public string _Title;
        public string Title { get { return _Title; } }

        public bool _GetTVUrl;
        public bool GetTVUrl { get { return _GetTVUrl; } }

        public string _TVUrlXml;
        public string TVUrlXml { get { return _TVUrlXml; } }

        public TskStatus Status { get; set; }

        public string DownloadPath { get; set; }

        internal FeedsExportTask(Int64 p_ID, Guid p_CustomerGUID, string p_SearchCriteria, string p_ArticleXml, string p_SortType, int p_RootPathID, bool p_IsSelectAll, DateTime p_CreatedDate, string p_Title, bool p_GetTVUrl, string p_TVUrlXml)
        {
            _ID = p_ID;
            _CustomerGUID = p_CustomerGUID;
            _SearchCriteria = p_SearchCriteria;
            _ArticleXml = p_ArticleXml;
            _SortType = p_SortType;
            _RootPathID = p_RootPathID;
            _IsSelectAll = p_IsSelectAll;
            _CreatedDate = p_CreatedDate;
            _Title = p_Title;
            _GetTVUrl = p_GetTVUrl;
            _TVUrlXml = p_TVUrlXml;
        }

        public enum TskStatus
        {
            COMPLETED,            
            FAILED,
            FAILED_DIRECTORY_CREATION,
            FAILED_FILE_CREATION,
            FAILED_TSK_CANCELLED,
            FAILED_UPDATE_PATH,
            IN_PROCESS,
            TIMEOUT_URL_GENERATION
        }
    }
}
