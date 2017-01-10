using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IQMedia.Service.DiscoveryExport
{
    class DiscoveryExportTask
    {
        private Int64 _ID;
        public Int64 ID { get { return _ID; } }

        private Guid _CustomerGUID;
        public Guid CustomerGUID { get { return _CustomerGUID; } }

        private string _SearchCriteria;
        public string SearchCriteria { get { return _SearchCriteria; } }

        private string _ArticleXml;
        public string ArticleXml { get { return _ArticleXml; } }

        private int _RootPathID;
        public int RootPathID { get { return _RootPathID; } }

        private bool _IsSelectAll;
        public bool IsSelectAll { get { return _IsSelectAll; } }

        private DateTime _CreatedDate;
        public DateTime CreatedDate { get { return _CreatedDate; } }

        public TskStatus Status { get; set; }

        public string DownloadPath { get; set; }

        internal DiscoveryExportTask(Int64 p_ID, Guid p_CustomerGUID, string p_SearchCriteria, string p_ArticleXml, int p_RootPathID, bool p_IsSelectAll, DateTime p_CreatedDate)
        {
            _ID = p_ID;
            _CustomerGUID = p_CustomerGUID;
            _SearchCriteria = p_SearchCriteria;
            _ArticleXml = p_ArticleXml;
            _RootPathID = p_RootPathID;
            _IsSelectAll = p_IsSelectAll;
            _CreatedDate = p_CreatedDate;
        }

        public enum TskStatus
        {
            COMPLETED,            
            FAILED,
            FAILED_DIRECTORY_CREATION,
            FAILED_FILE_CREATION,
            FAILED_TSK_CANCELLED,
            FAILED_UPDATE_PATH,
            IN_PROCESS
        }
    }
}
