using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;
using System.Data.Common;
using System.Data;
using Microsoft.Data.Extensions;
using System.Data.SqlClient;
using System.Xml.Linq;
using System.Data.SqlTypes;


namespace IQMedia.Service.Domain
{
    public partial class IQMediaEntities : ObjectContext
    {
        public ClientSettings GetClientSettings(Guid p_CustomerGUID)
        {
            ClientSettings clientSettings = new ClientSettings();
            clientSettings.SentimentSettings = new SentimentSettings();

            DbCommand command = this.CreateStoreCommand("usp_svc_DiscExp_SelectClientDetails", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@CustomerGUID", p_CustomerGUID));

            using (command.Connection.CreateConnectionScope())
            using (DbDataReader reader = command.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        clientSettings.ClientGUID = new Guid(Convert.ToString(reader["ClientGUID"]));
                        clientSettings.GMTHours = Convert.ToDouble(reader["GMTHours"]);
                        clientSettings.DSTHours = Convert.ToDouble(reader["DSTHours"]);
                        clientSettings.Exportlimit = Convert.ToInt32(reader["ExportLimit"]);
                        clientSettings.FeedsExportLimit = Convert.ToInt32(reader["FeedsExportLimit"]);
                        clientSettings.IsCompeteData = Convert.ToBoolean(reader["IsCompeteData"]);
                        clientSettings.IsNielsenData = Convert.ToBoolean(reader["IsNielsenData"]);
                        clientSettings.LicenseList = Convert.ToString(reader["License"]).Split(',').Select(s => Convert.ToInt32(s.Trim())).ToList();
                        clientSettings.TimeZone = Convert.ToString(reader["TimeZone"]);
                        clientSettings.RegionList = Convert.ToString(reader["Region"]).Split(',').Select(s => Convert.ToInt32(s.Trim())).ToList();
                        clientSettings.CountryList = Convert.ToString(reader["Country"]).Split(',').Select(s => Convert.ToInt32(s.Trim())).ToList();
                        clientSettings.UseProminenceMediaValue = Convert.ToString(reader["UseProminenceMediaValue"]).Trim() == "1";
                        clientSettings.RawMediaExpiration = reader["IQRawMediaExpiration"] != null ? Convert.ToInt32(reader["IQRawMediaExpiration"]) : (int?)null;

                        clientSettings.SentimentSettings.TVLowThreshold = Convert.ToString(reader["TVLowThreshold"]);
                        clientSettings.SentimentSettings.TVHighThreshold = Convert.ToString(reader["TVHighThreshold"]);
                        clientSettings.SentimentSettings.NMLowThreshold = Convert.ToString(reader["NMLowThreshold"]);
                        clientSettings.SentimentSettings.NMHighThreshold = Convert.ToString(reader["NMHighThreshold"]);
                        clientSettings.SentimentSettings.SMLowThreshold = Convert.ToString(reader["SMLowThreshold"]);
                        clientSettings.SentimentSettings.SMHighThreshold = Convert.ToString(reader["SMHighThreshold"]);
                        clientSettings.SentimentSettings.TwitterLowThreshold = Convert.ToString(reader["TwitterLowThreshold"]);
                        clientSettings.SentimentSettings.TwitterHighThreshold = Convert.ToString(reader["TwitterHighThreshold"]);
                        clientSettings.SentimentSettings.PQLowThreshold = Convert.ToString(reader["PQLowThreshold"]);
                        clientSettings.SentimentSettings.PQHighThreshold = Convert.ToString(reader["PQHighThreshold"]);
                    }
                }
            }

            return clientSettings;
        }

        public List<IQCompeteAll> GetCompeteData(Guid p_ClientGuid, XElement p_DisplyUrlXml, string p_MediaType)
        {
            DbCommand command = this.CreateStoreCommand("usp_IQ_CompeteAll_SelectArtileAdShareByClientGuidAndXml", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@ClientGuid", p_ClientGuid));
            command.Parameters.Add(new SqlParameter("@PublicationXml", new SqlXml(p_DisplyUrlXml.CreateReader())));
            command.Parameters.Add(new SqlParameter("@MediaType", p_MediaType));

            List<IQCompeteAll> _ListOfIQ_CompeteAll = new List<IQCompeteAll>();

            using (command.Connection.CreateConnectionScope())
            using (DbDataReader reader = command.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {

                        IQCompeteAll iqCompeteAll = new IQCompeteAll();

                        if (!reader["CompeteURL"].Equals(DBNull.Value))
                        {
                            iqCompeteAll.CompeteURL = Convert.ToString(reader["CompeteURL"]);
                        }

                        if (!reader["IQ_AdShare_Value"].Equals(DBNull.Value))
                        {
                            iqCompeteAll.IQ_AdShare_Value = Convert.ToDecimal(reader["IQ_AdShare_Value"]);
                        }

                        if (!reader["c_uniq_visitor"].Equals(DBNull.Value))
                        {
                            iqCompeteAll.c_uniq_visitor = Convert.ToInt32(reader["c_uniq_visitor"]);
                        }

                        if (!reader["IsCompeteAll"].Equals(DBNull.Value))
                        {
                            iqCompeteAll.IsCompeteAll = Convert.ToBoolean(reader["IsCompeteAll"]);
                        }

                        if (!reader["IsUrlFound"].Equals(DBNull.Value))
                        {
                            iqCompeteAll.IsUrlFound = Convert.ToBoolean(reader["IsUrlFound"]);
                        }

                        _ListOfIQ_CompeteAll.Add(iqCompeteAll);
                    }
                }
            }

            return _ListOfIQ_CompeteAll;
        }

        public RootPath GetRootPathLocationByID(int p_ID)
        {
            DbCommand command = this.CreateStoreCommand("usp_IQCore_RootPath_SelectPathByID", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@ID", p_ID));

            RootPath rootPath = null;

            using (command.Connection.CreateConnectionScope())
            using (DbDataReader reader = command.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    rootPath = new RootPath();

                    while (reader.Read())
                    {
                        rootPath.StoragePath=Convert.ToString(reader["StoragePath"]);
                        rootPath.StreamSuffixPath = Convert.ToString(reader["StreamSuffixPath"]);
                    }
                }
            }

            return rootPath;
        }

        public ClientTVSearchSettings GetSSPData(Guid ClientGuid, out bool IsAllDmaAllowed, out bool IsAllClassAllowed, out bool IsAllStationAllowed)
        {
            DbCommand command = this.CreateStoreCommand("usp_v4_IQ_Station_SelectSSPDataWithStationByClientGUID", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@ClientGUID", ClientGuid));

            IsAllDmaAllowed = false;
            IsAllClassAllowed = false;
            IsAllStationAllowed = false;

            var clientTVSearchSettings = new ClientTVSearchSettings();

            clientTVSearchSettings.DmaList = new List<string>();
            clientTVSearchSettings.ClassList = new List<string>();
            clientTVSearchSettings.StationList = new List<Station>();
            clientTVSearchSettings.AffiliateList = new List<string>();
            clientTVSearchSettings.RegionList = new List<int>();
            clientTVSearchSettings.CountryList = new List<int>();

            using (command.Connection.CreateConnectionScope())
            using (DbDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    clientTVSearchSettings.DmaList.Add(Convert.ToString(reader["Dma_Name"]));
                }

                if (reader.NextResult())
                {
                    while (reader.Read())
                    {
                        clientTVSearchSettings.ClassList.Add(Convert.ToString(reader["IQ_Class_Num"]));
                    }
                }

                if (reader.NextResult())
                {
                    while (reader.Read())
                    {
                        Station station = new Station();
                        station.StationID = Convert.ToString(reader["IQ_Station_ID"]);
                        station.Station_Call_Sign = Convert.ToString(reader["Station_Call_Sign"]);

                        clientTVSearchSettings.StationList.Add(station);

                        clientTVSearchSettings.AffiliateList.Add(Convert.ToString(reader["Station_Affil"]));                        
                    }

                    clientTVSearchSettings.AffiliateList = clientTVSearchSettings.AffiliateList.Distinct().ToList();
                }

                if (reader.NextResult())
                {
                    while (reader.Read())
                    {
                        IsAllDmaAllowed = Convert.ToBoolean(reader["IsAllDmaAllowed"]);
                        IsAllClassAllowed = Convert.ToBoolean(reader["IsAllClassAllowed"]);
                        IsAllStationAllowed = Convert.ToBoolean(reader["IsAllStationAllowed"]);
                    }
                }

                if (reader.NextResult())
                {
                    while (reader.Read())
                    {
                        clientTVSearchSettings.RegionList.Add(Convert.ToInt32(reader["Region_Num"]));
                    }
                }

                if (reader.NextResult())
                {
                    while (reader.Read())
                    {
                        clientTVSearchSettings.CountryList.Add(Convert.ToInt32(reader["Country_Num"]));
                    }
                }
            }

            return clientTVSearchSettings;
        }

        public List<DiscoveryMediaResult> GetNielsenData(Guid ClientGuid, XDocument xmldata, List<DiscoveryMediaResult> lstDiscoveryMediaResult)
        {
            DbCommand command = this.CreateStoreCommand("usp_v4_IQ_NIELSEN_SQAD_SelectByIQCCKeyList", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@ClientGuid", ClientGuid));
            command.Parameters.Add(new SqlParameter("@IQCCKeyList", new SqlXml(xmldata.CreateReader())));

            using (command.Connection.CreateConnectionScope())
            using (DbDataReader reader = command.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        DiscoveryMediaResult discoveryMediaResult = lstDiscoveryMediaResult.Find(p => p.IQ_CC_Key.Equals(reader["IQ_CC_KEY"]));

                        if (discoveryMediaResult != null)
                        {
                            if (!reader["AUDIENCE"].Equals(DBNull.Value))
                            {
                                discoveryMediaResult.Audience = Convert.ToInt32(reader["AUDIENCE"]);
                            }

                            if (!reader["SQAD_SHAREVALUE"].Equals(DBNull.Value))
                            {
                                discoveryMediaResult.IQAdsharevalue = Convert.ToDecimal(reader["SQAD_SHAREVALUE"]);
                                discoveryMediaResult.Nielsen_Result = Convert.ToBoolean(reader["IsActualNielsen"]) == true ? " (A)" : " (E)";
                            }
                        }
                    }
                }
            }

            return lstDiscoveryMediaResult;
        }

        public int UpdateDiscExpDownloadPath(Int64 p_ID, string p_DownloadPath)
        {
            DbCommand command = this.CreateStoreCommand("usp_svc_DiscExp_UpdateDownloadPath", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@ID", p_ID));
            command.Parameters.Add(new SqlParameter("@DownloadPath", p_DownloadPath));


            using (command.Connection.CreateConnectionScope())
                return command.ExecuteNonQuery();            
        }

        public int UpdateFeedsExpDownloadPath(Int64 p_ID, string p_DownloadPath)
        {
            DbCommand command = this.CreateStoreCommand("usp_feedsexport_IQService_FeedsExport_UpdateDownloadPath", CommandType.StoredProcedure);

            command.Parameters.Add(new SqlParameter("@ID", p_ID));
            command.Parameters.Add(new SqlParameter("@DownloadPath", p_DownloadPath));

            using (command.Connection.CreateConnectionScope())
                return command.ExecuteNonQuery();
        }

        //public List<IQCompeteAll> GetCompeteData(Guid ClientGuid, XElement displyUrlXml, string MediaType)
        //{

        //    DbCommand command = this.CreateStoreCommand("usp_IQ_CompeteAll_SelectArtileAdShareByClientGuidAndXml", CommandType.StoredProcedure);

        //    command.Parameters.Add(new SqlParameter("@ClientGuid", ClientGuid));
        //    command.Parameters.Add(new SqlParameter("@PublicationXml", new SqlXml(displyUrlXml.CreateReader())));
        //    command.Parameters.Add(new SqlParameter("@MediaType", MediaType));

        //    List<IQCompeteAll> competeList = new List<IQCompeteAll>();

        //    using (command.Connection.CreateConnectionScope())
        //    using (DbDataReader reader = command.ExecuteReader())
        //    {
        //        if (reader.HasRows)
        //        {
        //            while (reader.Read())
        //            {
        //                IQCompeteAll iqCompeteAll = new IQCompeteAll();

        //                if (!reader["CompeteURL"].Equals(DBNull.Value))
        //                {
        //                    iqCompeteAll.CompeteURL = Convert.ToString(reader["CompeteURL"]);
        //                }

        //                if (!reader["IQ_AdShare_Value"].Equals(DBNull.Value))
        //                {
        //                    iqCompeteAll.IQ_AdShare_Value = Convert.ToDecimal(reader["IQ_AdShare_Value"]);
        //                }

        //                if (!reader["c_uniq_visitor"].Equals(DBNull.Value))
        //                {
        //                    iqCompeteAll.c_uniq_visitor = Convert.ToInt32(reader["c_uniq_visitor"]);
        //                }

        //                if (!reader["IsCompeteAll"].Equals(DBNull.Value))
        //                {
        //                    iqCompeteAll.IsCompeteAll = Convert.ToBoolean(reader["IsCompeteAll"]);
        //                }

        //                if (!reader["IsUrlFound"].Equals(DBNull.Value))
        //                {
        //                    iqCompeteAll.IsUrlFound = Convert.ToBoolean(reader["IsUrlFound"]);
        //                }

        //                competeList.Add(iqCompeteAll);
        //            }
        //        }
        //    }            

        //    return competeList;
        //}
    }
}
