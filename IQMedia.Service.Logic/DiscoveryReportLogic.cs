using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using PMGSearch;
using System.Xml.Linq;
using IQMedia.Service.Common.Util;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using IQMedia.Service.Domain;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using IQCommon.Model;
using System.Reflection;


namespace IQMedia.Service.Logic
{
    public class DiscoveryReportLogic : BaseLogic, ILogic
    {
        public void GetSentimentSettingsByClientGuid(Guid p_ClientGuid, out float? TVLowThresholdValue, out float? TVHighThresholdValue, out float? NMLowThresholdValue, out float? NMHighThresholdValue, out float? SMLowThresholdValue, out float? SMHighThresholdValue, out float? PQLowThresholdValue, out float? PQHighThresholdValue)
        {

            try
            {
                TVLowThresholdValue = null;
                TVHighThresholdValue = null;
                NMLowThresholdValue = null;
                NMHighThresholdValue = null;
                SMLowThresholdValue = null;
                SMHighThresholdValue = null;
                PQLowThresholdValue = null;
                PQHighThresholdValue = null;
                IQMedia.Service.Domain.SentimentSettings sentimentSettings = Context.GetSentimentSettingsByClientGuid(p_ClientGuid).FirstOrDefault();
                if (sentimentSettings != null)
                {
                    TVLowThresholdValue = float.Parse(sentimentSettings.TVLowThreshold);
                    TVHighThresholdValue = float.Parse(sentimentSettings.TVHighThreshold);
                    NMLowThresholdValue = float.Parse(sentimentSettings.NMLowThreshold);
                    NMHighThresholdValue = float.Parse(sentimentSettings.NMHighThreshold);
                    SMLowThresholdValue = float.Parse(sentimentSettings.SMLowThreshold);
                    SMHighThresholdValue = float.Parse(sentimentSettings.SMHighThreshold);
                    PQLowThresholdValue = float.Parse(sentimentSettings.PQLowThreshold);
                    PQHighThresholdValue = float.Parse(sentimentSettings.PQHighThreshold);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool GetUseProminenceMediaValue(Guid customerGuid)
        {
            try
            {
                return Context.GetClientSettings(customerGuid).UseProminenceMediaValue;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Called by DiscoveryReportGenerate.ProcessBatches() via reflection. Not all parameters are used, but are necessary to match the method signature.
        public XDocument GetProQuestResult(List<Article> lstArticles, IQ_MediaTypeModel objSubMediaType, Dictionary<string, Tuple<float?, float?>> dictThresholds, bool useProminenceMediaValue, string logPrefix)
        {
            try
            {
                Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " information from their id and searchterm from PMG");
                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.PQ.ToString(), null, null));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);

                List<string> listOfSearchTerm = lstArticles.Select(a => a.SearchTerm).Distinct().ToList();
                XDocument xDocPQ = new XDocument(new XElement(objSubMediaType.DataModelType));

                foreach (string searchTerm in listOfSearchTerm)
                {
                    Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " items for search term :" + searchTerm + "\n and article ids :" + string.Join(",", lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID).ToList()));

                    SearchProQuestRequest searchPQRequest = new SearchProQuestRequest();
                    bool isError;

                    searchPQRequest.Facet = false;
                    searchPQRequest.SortFields = "date-";
                    searchPQRequest.IDs = lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID).ToList();
                    searchPQRequest.PageSize = lstArticles.Count();
                    searchPQRequest.SearchTerm = searchTerm;
                    searchPQRequest.IsSentiment = true;
                    searchPQRequest.LowThreshold = dictThresholds["PQ"].Item1;
                    searchPQRequest.HighThreshold = dictThresholds["PQ"].Item2;
                    searchPQRequest.IsOutRequest = true;
                    searchPQRequest.IsReturnHighlight = true;
                    SearchProQuestResult searchPQResults = searchEngine.SearchProQuest(searchPQRequest, false, out isError);

                    Logger.Info(logPrefix + "Request Url : " + searchPQResults.RequestUrl);
                    Logger.Info(logPrefix + "Response : " + searchPQResults.ResponseXml);

                    foreach (ProQuestResult pqResult in searchPQResults.ProQuestResults)
                    {
                        HighlightedPQOutput highlightedPQOutput = new HighlightedPQOutput();
                        highlightedPQOutput.Highlights = pqResult.Highlights;

                        Logger.Info(logPrefix + "Generating xml for : " + pqResult.IQSeqID);

                        xDocPQ.Root.Add(new XElement("SubMedia",
                                                    new XElement("ArticleID", pqResult.IQSeqID),
                                                    new XElement("MediaDate", pqResult.MediaDate),
                                                    new XElement("Title", pqResult.Title),
                                                    new XElement("SearchTerm", searchTerm),
                                                    new XElement("Number_Hits", pqResult.Mentions),
                                                    new XElement("HighlightingText", SerializeToXml(highlightedPQOutput)),
                                                    pqResult.Sentiments != null ? new XElement("PositiveSentiment", pqResult.Sentiments.PositiveSentiment) : new XElement("PositiveSentiment", 0),
                                                    pqResult.Sentiments != null ? new XElement("NegativeSentiment", pqResult.Sentiments.NegativeSentiment) : new XElement("NegativeSentiment", 0),
                                                    new XElement("Publication", pqResult.Publication),
                                                    new XElement("MediaCategory", pqResult.MediaCategory),
                                                    new XElement("Content", pqResult.Content),
                                                    new XElement("ContentHTML", pqResult.ContentHTML),
                                                    new XElement("AvailableDate", pqResult.AvailableDate),
                                                    new XElement("LanguageNum", pqResult.LanguageNum),
                                                    new XElement("Copyright", pqResult.Copyright),
                                                    new XElement("MediaType", objSubMediaType.MediaType),
                                                    new XElement("SubMediaType", objSubMediaType.SubMediaType),
                                                    new XElement("DataModelType", objSubMediaType.DataModelType),
                                                    pqResult.Authors == null || pqResult.Authors.Count == 0 ? null : new XElement("authors",
                                                                                                                         from ele in pqResult.Authors
                                                                                                                         select new XElement("author", ele))
                                                    ));
                    }
                }

                return xDocPQ;
            }
            catch (Exception ex)
            {
                Logger.Fatal(logPrefix + "exception occured while fetching " + objSubMediaType.SubMediaType + " items from PMG", ex);
                throw;
            }
        }

        // Called by DiscoveryReportGenerate.ProcessBatches() via reflection.
        public XDocument GetNewsResult(List<Article> lstArticles, IQ_MediaTypeModel objSubMediaType, Dictionary<string, Tuple<float?, float?>> dictThresholds, bool useProminenceMediaValue, string logPrefix)
        {
            try
            {
                Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " information from their id and searchterm from PMG");
                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), null, null));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);

                List<string> listOfSearchTerm = lstArticles.Select(a => a.SearchTerm).Distinct().ToList();
                XDocument xDocNM = new XDocument(new XElement(objSubMediaType.DataModelType));
                float? lowThreshold = dictThresholds["NM"].Item1;
                float? highThreshold = dictThresholds["NM"].Item2;

                foreach (string searchTerm in listOfSearchTerm)
                {
                    Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " items for search term :" + searchTerm + "\n and article ids :" + string.Join(",", lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID).ToList()));

                    SearchNewsRequest searchNewsRequest = new SearchNewsRequest();

                    searchNewsRequest.Facet = false;
                    searchNewsRequest.SortFields = "date-";
                    searchNewsRequest.IDs = lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID).ToList();
                    searchNewsRequest.PageSize = lstArticles.Count();
                    searchNewsRequest.SearchTerm = searchTerm;
                    searchNewsRequest.IsSentiment = true;
                    searchNewsRequest.LowThreshold = lowThreshold;
                    searchNewsRequest.HighThreshold = highThreshold;
                    searchNewsRequest.IsOutRequest = true;
                    searchNewsRequest.IsTitleNContentSearch = true;
                    searchNewsRequest.IsReturnHighlight = true;
                    searchNewsRequest.IsHilightInLeadParagraph = true;
                    searchNewsRequest.LeadParagraphChars = 500;
                    SearchNewsResults searchNewsResults = searchEngine.SearchNews(searchNewsRequest);

                    Logger.Info(logPrefix + "Request Url : " + searchNewsResults.RequestUrl);

                    List<double> lstSentimentWeights;
                    double prominenceMultiplier;

                    foreach (NewsResult newsResult in searchNewsResults.newsResults)
                    {
                        HighlightedNewsOutput highlightedNewsOutput = new HighlightedNewsOutput();
                        if (newsResult.Highlights != null && newsResult.Highlights.Count > 0)
                        {
                            // Replace LexisNexis linebreak placeholder text with whitespace
                            highlightedNewsOutput.Highlights = newsResult.Highlights.Select(s => !String.IsNullOrWhiteSpace(s) ? s.Replace(ConfigurationManager.AppSettings["LexisNexisLineBreakPlaceholder"], " ") : s).ToList(); ;
                        }
                        else
                        {
                            highlightedNewsOutput.Highlights = newsResult.Highlights;
                        }

                        if (useProminenceMediaValue)
                        {
                            Logger.Info(logPrefix + "Calculating prominence for : " + newsResult.IQSeqID);
                            if (newsResult.Sentiments != null && newsResult.Sentiments.HighlightToWeightMap != null && newsResult.Sentiments.HighlightToWeightMap.Count > 0)
                            {
                                lstSentimentWeights = newsResult.Sentiments.HighlightToWeightMap.Select(s => s.Weight).ToList();
                            }
                            else
                            {
                                lstSentimentWeights = new List<double>();
                            }

                            prominenceMultiplier = Prominence.Prominence.CalculateOnlineNewsProminence(newsResult.IsSearchTermInHeadline, lstSentimentWeights, lowThreshold.Value, highThreshold.Value, newsResult.Mentions, 
                                                                                                        newsResult.IsLeadParagraph, ConfigurationManager.AppSettings["NMProminenceLogLocation"], Convert.ToBoolean(ConfigurationManager.AppSettings["IsLogNMProminence"]));

                            Logger.Info(logPrefix + "Prominence multiplier for id : " + newsResult.IQSeqID + " is : " + prominenceMultiplier);
                        }
                        else
                        {
                            prominenceMultiplier = 1;
                        }

                        xDocNM.Root.Add(new XElement("SubMedia",
                                                    new XElement("ArticleID", newsResult.IQSeqID.Replace("_", string.Empty)),
                                                    new XElement("Url", newsResult.Article + (newsResult.IQLicense == 3 ? "&source=library" : "")), // LexisNexis articles need to know if they were opened from Library
                                                    new XElement("harvest_time", newsResult.date),
                                                    new XElement("Title", newsResult.Title),
                                                    new XElement("CompeteUrl", newsResult.HomeurlDomain),
                                                    new XElement("MediaType", objSubMediaType.MediaType),
                                                    new XElement("SearchTerm", searchTerm),
                                                    new XElement("Number_Hits", newsResult.Mentions),
                                                    new XElement("HighlightingText", SerializeToXml(highlightedNewsOutput)),
                                                    newsResult.Sentiments != null ? new XElement("PositiveSentiment", newsResult.Sentiments.PositiveSentiment) : new XElement("PositiveSentiment", 0),
                                                    newsResult.Sentiments != null ? new XElement("NegativeSentiment", newsResult.Sentiments.NegativeSentiment) : new XElement("NegativeSentiment", 0),
                                                    new XElement("Publication", newsResult.publication),
                                                    new XElement("IQLicense", newsResult.IQLicense),
                                                    new XElement("SubMediaType", objSubMediaType.SubMediaType),
                                                    new XElement("ProminenceMultiplier", prominenceMultiplier),
                                                    new XElement("DataModelType", objSubMediaType.DataModelType)));
                    }
                }

                return xDocNM;

            }
            catch (Exception ex)
            {
                Logger.Fatal(logPrefix + "exception occured while fetching " + objSubMediaType.SubMediaType + " items from PMG", ex);
                throw;
            }
        }

        // Called by DiscoveryReportGenerate.ProcessBatches() via reflection.
        public XDocument GetSMResult(List<Article> lstArticles, IQ_MediaTypeModel objSubMediaType, Dictionary<string, Tuple<float?, float?>> dictThresholds, bool useProminenceMediaValue, string logPrefix)
        {
            try
            {
                Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " information from their id and searchterm from PMG");
                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), null, null));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);

                List<string> listOfSearchTerm = lstArticles.Select(a => a.SearchTerm).Distinct().ToList();
                XDocument xDocSM = new XDocument(new XElement(objSubMediaType.DataModelType));
                float? lowThreshold = dictThresholds["SM"].Item1;
                float? highThreshold = dictThresholds["SM"].Item2;

                foreach (string searchTerm in listOfSearchTerm)
                {
                    Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " items for search term :" + searchTerm + " \n and article ids :" + string.Join(",", lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID).ToList()));

                    SearchSMRequest searchSMRequest = new SearchSMRequest();

                    searchSMRequest.Facet = false;
                    searchSMRequest.SortFields = "date-";
                    searchSMRequest.SearchTerm = searchTerm;
                    searchSMRequest.IsSentiment = true;
                    searchSMRequest.LowThreshold = lowThreshold;
                    searchSMRequest.HighThreshold = highThreshold;
                    searchSMRequest.ids = lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID).ToList();
                    searchSMRequest.PageSize = lstArticles.Count();
                    searchSMRequest.IsTitleNContentSearch = true;
                    searchSMRequest.IsReturnHighlight = true;

                    SearchSMResult searchSMResult = searchEngine.SearchSocialMedia(searchSMRequest);
                    List<double> lstSentimentWeights;
                    double prominenceMultiplier;

                    foreach (SMResult smResult in searchSMResult.smResults)
                    {
                        if (useProminenceMediaValue && objSubMediaType.UseMediaValue)
                        {
                            Logger.Info(logPrefix + "Calculating prominence for : " + smResult.IQSeqID);
                            if (smResult.Sentiments != null && smResult.Sentiments.HighlightToWeightMap != null && smResult.Sentiments.HighlightToWeightMap.Count > 0)
                            {
                                lstSentimentWeights = smResult.Sentiments.HighlightToWeightMap.Select(s => s.Weight).ToList();
                            }
                            else
                            {
                                lstSentimentWeights = new List<double>();
                            }

                            prominenceMultiplier = Prominence.Prominence.CalculateSocialMediaProminence(lstSentimentWeights, lowThreshold.Value, highThreshold.Value, smResult.feedClass, ConfigurationManager.AppSettings["SMProminenceLogLocation"], Convert.ToBoolean(ConfigurationManager.AppSettings["IsLogSMProminence"]));
                            Logger.Info(logPrefix + "Prominence multiplier for id : " + smResult.IQSeqID + " is : " + prominenceMultiplier);
                        }
                        else
                        {
                            prominenceMultiplier = 1;
                        }

                        HighlightedSMOutput highlightedSMOutput = new HighlightedSMOutput();
                        highlightedSMOutput.Highlights = smResult.Highlights;

                        xDocSM.Root.Add(new XElement("SubMedia",
                                                    new XElement("ArticleID", smResult.IQSeqID),
                                                    new XElement("Url", smResult.link),
                                                    new XElement("harvest_time", smResult.itemHarvestDate_DT),
                                                    new XElement("Title", smResult.description),
                                                    new XElement("CompeteUrl", smResult.HomeurlDomain),
                                                    new XElement("MediaType", objSubMediaType.MediaType),
                                                    new XElement("SubMediaType", objSubMediaType.SubMediaType),
                                                    new XElement("SearchTerm", searchTerm),
                                                    new XElement("Number_Hits", smResult.Mentions),
                                                    new XElement("HighlightingText", SerializeToXml(highlightedSMOutput)),
                                                    smResult.Sentiments != null ? new XElement("PositiveSentiment", smResult.Sentiments.PositiveSentiment) : new XElement("PositiveSentiment", 0),
                                                    smResult.Sentiments != null ? new XElement("NegativeSentiment", smResult.Sentiments.NegativeSentiment) : new XElement("NegativeSentiment", 0),
                                                    new XElement("homeLink", smResult.homeLink),
                                                    new XElement("ProminenceMultiplier", prominenceMultiplier),
                                                    new XElement("DataModelType", objSubMediaType.DataModelType)
                                                    ));
                    }
                }

                return xDocSM;

            }
            catch (Exception ex)
            {
                Logger.Fatal(logPrefix + "exception occured while fetching " + objSubMediaType.SubMediaType + " items from PMG", ex);
                throw;
            }
        }

        // Called by DiscoveryReportGenerate.ProcessBatches() via reflection. Not all parameters are used, but are necessary to match the method signature.
        public XDocument GetTVResult(List<Article> lstArticles, IQ_MediaTypeModel objSubMediaType, Dictionary<string, Tuple<float?, float?>> dictThresholds, bool useProminenceMediaValue, string logPrefix)
        {
            try
            {
                Logger.Info(logPrefix + "fetch " + objSubMediaType.SubMediaType + " informations from their GUID and searchterm from PMG");

                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.TV.ToString(), null, null));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);

                List<string> listOfSearchTerm = lstArticles.Select(a => a.SearchTerm).Distinct().ToList();
                XDocument xDocTV = new XDocument(new XElement(objSubMediaType.DataModelType));

                foreach (string searchTerm in listOfSearchTerm)
                {
                    Logger.Info(logPrefix + "fetch TV items for search term :" + searchTerm + "\n and Guid List :" + string.Join(",", lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID)));

                    SearchRequest searchRequest = new SearchRequest();
                    searchRequest.Facet = false;
                    searchRequest.SortFields = "date-";
                    searchRequest.GuidList = string.Join(",", lstArticles.Where(a => a.SearchTerm.Equals(searchTerm)).Select(a => a.ArticleID));
                    searchRequest.PageSize = lstArticles.Select(s => s).Count();
                    searchRequest.Terms = searchTerm;
                    searchRequest.LowThreshold = dictThresholds["TV"].Item1;
                    searchRequest.HighThreshold = dictThresholds["TV"].Item2;
                    searchRequest.IsSentiment = true;
                    searchRequest.IsTitleNContentSearch = true;
                    SearchResult searchResult = searchEngine.Search(searchRequest);

                    foreach (Hit hit in searchResult.Hits)
                    {
                        HighlightedCCOutput highlightedCCOutput = new HighlightedCCOutput();
                        highlightedCCOutput.CC = hit.TermOccurrences.Select(a => new ClosedCaption
                        {
                            Offset = a.TimeOffset,
                            Text = a.SurroundingText
                        }).ToList();

                        xDocTV.Root.Add(new XElement("SubMedia",
                                                    new XElement("VideoGUID", hit.Guid),
                                                    (hit.TermOccurrences != null && hit.TermOccurrences.Count > 0) ? new XElement("StartOffset", hit.TermOccurrences.OrderBy(o => o.TimeOffset).FirstOrDefault().TimeOffset) :
                                                                new XElement("StartOffset", 0),
                                                    hit.Sentiments != null ? new XElement("PositiveSentiment", hit.Sentiments.PositiveSentiment) : new XElement("PositiveSentiment", 0),
                                                    hit.Sentiments != null ? new XElement("NegativeSentiment", hit.Sentiments.NegativeSentiment) : new XElement("NegativeSentiment", 0),
                                                    new XElement("Title", hit.Title120),
                                                    new XElement("SearchTerm", searchTerm),
                                                    new XElement("Number_Hits", hit.TotalNoOfOccurrence),
                                                    new XElement("HighlightingText", SerializeToXml(highlightedCCOutput)),
                                                    new XElement("IQ_CC_KEY", hit.Iqcckey),
                                                    new XElement("IQ_DMA", hit.IQDmaNum),
                                                    new XElement("MediaType", objSubMediaType.MediaType),
                                                    new XElement("SubMediaType", objSubMediaType.SubMediaType),
                                                    new XElement("DataModelType", objSubMediaType.DataModelType)
                                                    ));
                    }
                }

                return xDocTV;
            }
            catch (Exception ex)
            {
                Logger.Fatal(logPrefix + "exception occured while fetching " + objSubMediaType.SubMediaType + " items from PMG", ex);
                throw;
            }
        }


        private string GetFeedClass(string feedClass)
        {
            if (feedClass.Trim() == "Blog")
            {
                return "Blog";
            }
            else if (feedClass.Trim() == "Review" || feedClass.Trim() == "Forum")
            {
                return "Forum";
            }
            else
            {
                return "Social Media";
            }
        }

        public int? InsertDiscoveryReport(Int64 p_ReportID, string p_MediaIDXML)
        {
            try
            {
                return Context.InsertDiscoveryReport(p_ReportID, p_MediaIDXML).FirstOrDefault();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static string SerializeToXml(object p_SerializationObject)
        {
            try
            {
                string _XMLString = string.Empty;

                System.Text.UTF8Encoding _Encoding = new System.Text.UTF8Encoding();
                XmlWriterSettings _XmlWriterSettings = new XmlWriterSettings();
                // _XmlWriterSettings.Encoding=_Encoding;
                _XmlWriterSettings.OmitXmlDeclaration = true;

                XmlSerializer _XmlSerializer = new XmlSerializer(p_SerializationObject.GetType(), "");

                try
                {
                    StringWriter _StringWriter = new StringWriter();
                    using (XmlWriter _XmlWriter = XmlWriter.Create(_StringWriter,
                    _XmlWriterSettings))
                    {
                        XmlSerializerNamespaces _XmlSerializerNamespaces = new XmlSerializerNamespaces();
                        _XmlSerializerNamespaces.Add("", "");

                        _XmlSerializer.Serialize(_XmlWriter, p_SerializationObject, _XmlSerializerNamespaces);
                    }

                    _XMLString = _StringWriter.ToString();
                }
                catch (Exception _Exception)
                {
                    throw _Exception;
                }

                return _XMLString;
            }
            catch (Exception _Exception)
            {
                throw _Exception;
            }
        }


        public string ExportCSV(Int64 p_ID, bool p_IsSelectAll, string p_SearchCriteria, string p_ArticleIDXml, Guid p_CustomerGUID)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            ClientSettings clientSettings = Context.GetClientSettings(p_CustomerGUID);
            List<IQ_MediaTypeModel> lstMasterSubMediaTypes = IQCommon.CommonFunctions.GetMediaTypes(p_CustomerGUID).Where(w => w.TypeLevel == 2 && w.IsActiveDiscovery).ToList();
            string searchTerm = "";

            List<string> lstMediaIDs = null;
            SearchCriteria searchCriteria = new SearchCriteria();
            XDocument xDocArticleIDs = null;
            if (p_IsSelectAll)
            {
                searchCriteria = (SearchCriteria)CommonFunctions.DeserialiazeXml(p_SearchCriteria, searchCriteria);
                searchTerm = searchCriteria.SearchTerm;
                lstMasterSubMediaTypes = lstMasterSubMediaTypes.Where(w => searchCriteria.SubMediaTypes == null || searchCriteria.SubMediaTypes.Count == 0 || searchCriteria.SubMediaTypes.Contains(w.SubMediaType)).ToList();
            }
            else
            {
                xDocArticleIDs = XDocument.Parse(p_ArticleIDXml);
                List<XElement> searchTermNodes = xDocArticleIDs.Descendants("SearchTerm").ToList();
                if (searchTermNodes.Count == 1)
                {
                    searchTerm = searchTermNodes[0].Value;
                }
            }

            foreach (IQ_MediaTypeModel objSubMediaType in lstMasterSubMediaTypes)
            {
                // If the user did Select All and specified a submedia type, role access isn't required
                if (objSubMediaType.HasAccess || (searchCriteria.SubMediaTypes != null && searchCriteria.SubMediaTypes.Contains(objSubMediaType.SubMediaType)))
                {
                    bool hasSubMediaTypeItems = false;
                    if (!p_IsSelectAll)
                    {
                        if (xDocArticleIDs.Descendants(objSubMediaType.SubMediaType).Count() > 0)
                        {
                            hasSubMediaTypeItems = true;
                            lstMediaIDs = xDocArticleIDs.Descendants(objSubMediaType.SubMediaType).Descendants("ID").Select(s => s.Value).ToList();
                        }
                    }
                    else
                    {
                        hasSubMediaTypeItems = searchCriteria.SubMediaTypes == null || searchCriteria.SubMediaTypes.Count == 0 || searchCriteria.SubMediaTypes.Contains(objSubMediaType.SubMediaType);
                    }

                    if (hasSubMediaTypeItems)
                    {
                        /* Call the appropriate method in DiscoveryReportLogic based on the DiscExportSearchMethod field of the IQ_MediaTypes table
                         * The method must return a List of DiscoveryMediaResult objects and accept the following parameters in this order:
                         *      - ClientSettings object
                         *      - SearchCriteria object
                         *      - List of media IDs (List<string>)
                         *      - Search term
                         *      - IsSelectAll flag
                         *      - IQ_MediaTypeModel object
                         */
                        Type type = this.GetType();
                        MethodInfo methodInfo = type.GetMethod(objSubMediaType.DiscExportSearchMethod);
                        object classInstance = Activator.CreateInstance(type, null);
                        object[] parameters = new object[] { clientSettings, searchCriteria, lstMediaIDs, searchTerm, p_IsSelectAll, objSubMediaType };

                        List<DiscoveryMediaResult> lstResults = (List<DiscoveryMediaResult>)methodInfo.Invoke(classInstance, parameters);
                        if (lstResults != null && lstResults.Count > 0)
                        {
                            lstOfDiscoveryMediaResult.AddRange(lstResults);
                        }
                    }
                }
            }

            if (!p_IsSelectAll)
            {
                lstOfDiscoveryMediaResult = lstOfDiscoveryMediaResult.OrderByDescending(a => a.Date).ToList();
            }
            else if (searchCriteria.SubMediaTypes == null || searchCriteria.SubMediaTypes.Count != 1)
            {
                lstOfDiscoveryMediaResult = lstOfDiscoveryMediaResult.OrderByDescending(a => a.Date).Take(clientSettings.Exportlimit).ToList();
            }
            else
            {
                lstOfDiscoveryMediaResult = lstOfDiscoveryMediaResult.Take(clientSettings.Exportlimit).ToList();
            }

            return GetCSVData(lstOfDiscoveryMediaResult, clientSettings.IsNielsenData, clientSettings.IsCompeteData, clientSettings.ClientID, clientSettings.CustomerID, searchTerm, clientSettings.GMTHours, clientSettings.DSTHours, lstMasterSubMediaTypes);
        }

        public int UpdateDiscExpDownloadPath(Int64 p_ID, string p_DownloadPath)
        {
            return Context.UpdateDiscExpDownloadPath(p_ID, p_DownloadPath);
        }


        private string GetCSVData(List<DiscoveryMediaResult> lstDiscoveryMediaResult, bool IsNielsenData, bool IsCompeteData, Int64 ClientID, Int64 CustomerKey, string p_SearchTerm, double p_GMTHours, double p_DSTHours, List<IQ_MediaTypeModel> lstMasterSubMediaTypes)
        {
            string NotApplicable = "";
            string DQ = "\"";

            StringBuilder sb = new StringBuilder();

            if (lstDiscoveryMediaResult != null)
            {
                // Build media item header
                sb.Append("Media Date time,Time Zone,Search Term,Source,Title,Outlet,DMA,Affiliate,URL" + (IsNielsenData || IsCompeteData ? ",Audience,Audience Status,Media Value ($)" : string.Empty) + ",Positive Sentiment,Negative Sentiment,Hits,Text");
                sb.Append(Environment.NewLine);

                foreach (DiscoveryMediaResult item in lstDiscoveryMediaResult)
                {
                    string title = String.IsNullOrEmpty(item.Title) ? string.Empty : HttpUtility.HtmlDecode(item.Title.Replace("\"", "\"\""));
                    IQ_MediaTypeModel objSubMediaType = lstMasterSubMediaTypes.First(f => String.Compare(f.SubMediaType, item.SubMediaType, true) == 0);

                    // Media Date
                    sb.Append(item.Date.ToString());
                    sb.Append(",");

                    // Time Zone
                    sb.Append(item.TimeZone);
                    sb.Append(",");

                    // Search Term
                    sb.Append(DQ + p_SearchTerm.Replace("\"", "\"\"") + DQ);
                    sb.Append(",");

                    // Source
                    if (item.IQLicense == 3)
                    {
                        sb.Append("LexisNexis(R)");
                        sb.Append(",");
                    }
                    else
                    {
                        sb.Append(objSubMediaType.DisplayName);
                        sb.Append(",");
                    }

                    // Title
                    sb.Append(DQ + title + DQ);
                    sb.Append(",");

                    // Outlet
                    sb.Append(DQ + item.Outlet + DQ);
                    sb.Append(",");

                    // DMA
                    sb.Append(DQ + item.Market + DQ);
                    sb.Append(",");

                    // Affiliate
                    sb.Append(DQ + item.Affiliate + DQ);
                    sb.Append(",");

                    // URL
                    if (!String.IsNullOrEmpty(item.ArticleURL))
                    {
                        sb.Append(String.Format("=HYPERLINK(\"{0}\")", item.ArticleURL.Replace(",", "%2c")));
                    }
                    sb.Append(",");

                    if (IsNielsenData || IsCompeteData)
                    {
                        if (objSubMediaType.RequireNielsenAccess && IsNielsenData)
                        {
                            // Audience
                            if (objSubMediaType.UseAudience && item.Audience.HasValue && item.Audience > 0)
                            {
                                sb.Append(item.Audience);
                            }
                            sb.Append(",");

                            // Audience Status
                            sb.Append(",");

                            // Media Value
                            if (objSubMediaType.UseMediaValue && item.IQAdsharevalue.HasValue && item.IQAdsharevalue > 0)
                            {
                                sb.Append(item.IQAdsharevalue);
                            }
                            sb.Append(",");
                        }
                        else if (objSubMediaType.RequireCompeteAccess && IsCompeteData)
                        {
                            // Audience
                            if (objSubMediaType.UseAudience && item.Audience != -1)
                            {
                                if (item.Audience.HasValue)
                                {
                                    sb.Append(item.Audience.ToString());
                                }
                                else
                                {
                                    sb.Append(NotApplicable);
                                }
                            }
                            sb.Append(",");

                            // Audience Status
                            if (objSubMediaType.UseAudience && item.Audience != -1 && !string.IsNullOrWhiteSpace(item.CompeteImage))
                            {
                                sb.Append("A");
                            }
                            sb.Append(",");

                            // Media Value
                            if (objSubMediaType.UseMediaValue && Decimal.Compare(Convert.ToDecimal(item.IQAdsharevalue), -1M) != 0)
                            {
                                if (item.IQAdsharevalue.HasValue)
                                {
                                    sb.Append(item.IQAdsharevalue);
                                }
                                else
                                {
                                    sb.Append(NotApplicable);
                                }
                            }
                            sb.Append(",");
                        }
                        else
                        {
                            sb.Append(",,,");
                        }
                    }

                    // Positive Sentiment
                    sb.Append(item.PositiveSentiment);
                    sb.Append(",");

                    // Negative Sentiment
                    sb.Append(item.NegativeSentiment);
                    sb.Append(",");

                    // Hits
                    sb.Append(item.Hits);
                    sb.Append(",");

                    // Text
                    if (!string.IsNullOrEmpty(item.Body))
                    {
                        item.Body = item.Body.Replace("\"", "\"\"");
                    }
                    sb.Append(DQ + item.Body + DQ);

                    sb.Append(Environment.NewLine);
                }

                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        private ClientTVSearchSettings SetTVClientSettingsSearchRequest(SearchRequest p_SearchRequest, ClientSettings p_ClientSettings, SearchCriteria p_SearchCriteria)
        {
            bool IsAllDmaAllowed;
            bool IsAllClassAllowed;
            bool IsAllStationAllowed;

            var clientTVSearchSettings = Context.GetSSPData(p_ClientSettings.ClientGUID, out IsAllDmaAllowed, out IsAllClassAllowed, out IsAllStationAllowed);

            #region Search Class (Category) List

            bool isClassValid = IsAllClassAllowed;

            p_SearchRequest.IQClassNum = new List<string>();
            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.CategoryList != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.CategoryList.Count > 0)
            {
                if (!IsAllClassAllowed)
                {
                    List<string> lstclass = clientTVSearchSettings.ClassList.Join(p_SearchCriteria.AdvanceSearchSettings.TVSettings.CategoryList, a => a, b => b, (a, b) => b).ToList();
                    if (lstclass != null && lstclass.Count > 0)
                    {
                        p_SearchRequest.IQClassNum = lstclass;
                        isClassValid = true;
                    }
                    else
                    {
                        isClassValid = false;
                    }
                }
                else
                {
                    p_SearchRequest.IQClassNum = p_SearchCriteria.AdvanceSearchSettings.TVSettings.CategoryList;
                    isClassValid = true;
                }
            }
            else if (!IsAllClassAllowed)
            {
                p_SearchRequest.IQClassNum = clientTVSearchSettings.ClassList;
                isClassValid = true;
            }

            #endregion

            if (!isClassValid)
            {
                throw new Exception();
            }

            #region Search DMA List

            bool isDmaValid = false;
            isDmaValid = IsAllDmaAllowed;

            p_SearchRequest.IQDmaName = new List<string>();

            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.IQDmaList != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.IQDmaList.Count > 0)
            {
                if (!IsAllDmaAllowed)
                {
                    List<string> lstdam = clientTVSearchSettings.DmaList.Join(p_SearchCriteria.AdvanceSearchSettings.TVSettings.IQDmaList, a => a, b => b, (a, b) => b).ToList();

                    if (lstdam != null && lstdam.Count > 0)
                    {
                        p_SearchRequest.IQDmaName = lstdam;
                        isDmaValid = true;
                    }
                    else
                    {
                        isDmaValid = false;
                    }
                }
                else
                {
                    p_SearchRequest.IQDmaName = p_SearchCriteria.AdvanceSearchSettings.TVSettings.IQDmaList;
                    isDmaValid = true;
                }
            }
            else if (!IsAllDmaAllowed)
            {
                p_SearchRequest.IQDmaName = clientTVSearchSettings.DmaList.Select(s => s).ToList();
                isDmaValid = true;
            }

            #endregion

            if (!isDmaValid)
            {
                throw new Exception("Invalid Dma");
            }

            #region Search Station List

            bool isStationValid = false;
            isStationValid = IsAllStationAllowed;

            p_SearchRequest.Stations = new List<string>();

            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.StationList != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.StationList.Count > 0)
            {
                if (!IsAllStationAllowed)
                {
                    List<string> lststation = clientTVSearchSettings.StationList.Join(p_SearchCriteria.AdvanceSearchSettings.TVSettings.StationList, a => a.StationID, b => b, (a, b) => b).ToList();

                    if (lststation != null && lststation.Count > 0)
                    {
                        p_SearchRequest.Stations = lststation;
                        isStationValid = true;
                    }
                    else
                    {
                        isStationValid = false;
                    }
                }
                else
                {
                    p_SearchRequest.Stations = p_SearchCriteria.AdvanceSearchSettings.TVSettings.StationList;
                    isStationValid = true;
                }
            }
            else if (!IsAllStationAllowed)
            {
                p_SearchRequest.Stations = clientTVSearchSettings.StationList.Select(s => s.StationID).ToList();
                isStationValid = true;
            }

            #endregion

            if (!isStationValid)
            {
                throw new Exception();
            }

            #region Search Station Affiliate List

            bool isStationAffilValid = false;
            isStationAffilValid = IsAllStationAllowed;

            p_SearchRequest.StationAffil = new List<string>();

            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.AffiliateList != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.AffiliateList.Count > 0)
            {
                if (!IsAllStationAllowed)
                {
                    List<string> lstaffiliate = clientTVSearchSettings.AffiliateList.Join(p_SearchCriteria.AdvanceSearchSettings.TVSettings.AffiliateList, a => a, b => b, (a, b) => b).ToList();

                    if (lstaffiliate != null && lstaffiliate.Count > 0)
                    {
                        p_SearchRequest.StationAffil = lstaffiliate;
                        isStationAffilValid = true;
                    }
                    else
                    {
                        isStationAffilValid = false;
                    }
                }
                else
                {
                    p_SearchRequest.StationAffil = p_SearchCriteria.AdvanceSearchSettings.TVSettings.AffiliateList;
                    isStationAffilValid = true;
                }
            }
            else if (!IsAllStationAllowed)
            {
                p_SearchRequest.StationAffil = clientTVSearchSettings.AffiliateList;
                isStationAffilValid = true;
            }

            #endregion

            if (!isStationAffilValid)
            {
                throw new Exception();
            }

            #region Search Region List

            bool isRegionValid = false;
            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.RegionList != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.RegionList.Count > 0)
            {
                List<int> lstRegion = clientTVSearchSettings.RegionList.Join(p_SearchCriteria.AdvanceSearchSettings.TVSettings.RegionList, a => a, b => Convert.ToInt32(b), (a, b) => a).ToList();
                if (lstRegion != null && lstRegion.Count > 0)
                {
                    p_SearchRequest.IncludeRegionsNum = lstRegion;
                    isRegionValid = true;
                }
                else
                {
                    isRegionValid = false;
                }
            }
            else if (clientTVSearchSettings.RegionList != null && clientTVSearchSettings.RegionList.Count > 0)
            {
                p_SearchRequest.IncludeRegionsNum = clientTVSearchSettings.RegionList;
                isRegionValid = true;
            }

            #endregion

            if (!isRegionValid)
            {
                throw new Exception();
            }

            #region Search Country List

            bool isCountryValid = false;
            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.CountryList != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings.CountryList.Count > 0)
            {
                List<int> lstCountry = clientTVSearchSettings.CountryList.Join(p_SearchCriteria.AdvanceSearchSettings.TVSettings.CountryList, a => a, b => Convert.ToInt32(b), (a, b) => a).ToList();
                if (lstCountry != null && lstCountry.Count > 0)
                {
                    p_SearchRequest.CountryNums = lstCountry;
                    isCountryValid = true;
                }
                else
                {
                    isCountryValid = false;
                }
            }
            else if (clientTVSearchSettings.CountryList != null && clientTVSearchSettings.CountryList.Count > 0)
            {
                p_SearchRequest.CountryNums = clientTVSearchSettings.CountryList;
                isCountryValid = true;
            }

            #endregion

            if (!isCountryValid)
            {
                throw new Exception();
            }    

            #region Search Title

            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && !string.IsNullOrEmpty(p_SearchCriteria.AdvanceSearchSettings.TVSettings.ProgramTitle))
            {
                p_SearchRequest.Title120 = p_SearchCriteria.AdvanceSearchSettings.TVSettings.ProgramTitle.Trim();
            }

            #endregion

            #region Search Appearing

            if (p_SearchCriteria.AdvanceSearchSettings != null && p_SearchCriteria.AdvanceSearchSettings.TVSettings != null && !string.IsNullOrEmpty(p_SearchCriteria.AdvanceSearchSettings.TVSettings.Appearing))
            {
                p_SearchRequest.Appearing = p_SearchCriteria.AdvanceSearchSettings.TVSettings.Appearing.Trim();
            }

            #endregion

            return clientTVSearchSettings;
        }

        public List<DiscoveryMediaResult> GetTVResultsForExport(ClientSettings clientSettings, SearchCriteria searchCriteria, List<string> lstMediaIDs, string searchTerm, bool isSelectAll, IQ_MediaTypeModel objSubMediaType)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            DateTime? fromDate = isSelectAll ? searchCriteria.FromDate : (DateTime?)null;
            DateTime? toDate = isSelectAll ? searchCriteria.ToDate : (DateTime?)null; 

            System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.TV.ToString(), fromDate, toDate));
            SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
            SearchRequest searchRequest = new SearchRequest();
            
            searchRequest.Terms = searchTerm;
            searchRequest.IsTitleNContentSearch = true;
            searchRequest.SortFields = "date-";
            searchRequest.IsSentiment = true;
            searchRequest.HighThreshold = float.Parse(clientSettings.SentimentSettings.TVHighThreshold);
            searchRequest.LowThreshold = float.Parse(clientSettings.SentimentSettings.TVLowThreshold);
            searchRequest.IsShowCC = false;

            if (isSelectAll)
            {
                searchRequest.PageSize = clientSettings.Exportlimit;
                searchRequest.StartDate = searchCriteria.FromDate;
                searchRequest.EndDate = searchCriteria.ToDate;

                if (searchCriteria.AdvanceSearchSettings != null && searchCriteria.AdvanceSearchSettings.TVSettings != null && !String.IsNullOrWhiteSpace(searchCriteria.AdvanceSearchSettings.TVSettings.SearchTerm))
                {
                    searchRequest.Terms = searchCriteria.AdvanceSearchSettings.TVSettings.SearchTerm.Trim();
                }
            }
            else
            {
                searchRequest.GuidList = string.Join(",", lstMediaIDs);
                searchRequest.PageSize = lstMediaIDs.Count;
            }

            var clientTVSearchSettings = SetTVClientSettingsSearchRequest(searchRequest, clientSettings, searchCriteria);

            SearchResult searchResult = searchEngine.Search(searchRequest);

            XDocument xDoc = new XDocument(new XElement("list"));

            if (searchResult.Hits != null)
            {
                foreach (Hit hit in searchResult.Hits)
                {
                    xDoc.Root.Add(new XElement("item", new XAttribute("iq_cc_key", hit.Iqcckey), new XAttribute("iq_dma", hit.IQDmaNum)));
                    DiscoveryMediaResult discoveryMediaResult = new DiscoveryMediaResult();
                    discoveryMediaResult.Date = Convert.ToDateTime(hit.RLStationDateTime);
                    discoveryMediaResult.Title = hit.Title120;
                    discoveryMediaResult.Hits = hit.TermOccurrences != null ? hit.TermOccurrences.Count() : 0;

                    discoveryMediaResult.SubMediaType = objSubMediaType.SubMediaType;

                    Station station = clientTVSearchSettings.StationList.Where(s => string.Compare(s.StationID, hit.StationId, true) == 0).FirstOrDefault();
                    discoveryMediaResult.Outlet = station != null ? station.Station_Call_Sign : String.Empty;
                    discoveryMediaResult.IQ_CC_Key = hit.Iqcckey;

                    discoveryMediaResult.Affiliate = hit.Affiliate;
                    discoveryMediaResult.Market = hit.Market;
                    discoveryMediaResult.TimeZone = hit.ClipTimeZone;

                    discoveryMediaResult.PositiveSentiment = hit.Sentiments.PositiveSentiment;
                    discoveryMediaResult.NegativeSentiment = hit.Sentiments.NegativeSentiment;

                    string HighlightedText = string.Join(" ", hit.TermOccurrences.Select(s => s.SurroundingText).ToArray()).Replace("&lt;", "<").Replace("&gt;", ">");
                    if (HighlightedText.Length > 255)
                    {
                        int IndexAfter255Char = HighlightedText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                        HighlightedText = IndexAfter255Char == -1 ? HighlightedText.Substring(0, HighlightedText.Length) : HighlightedText.Substring(0, 255 + IndexAfter255Char) + "...";
                    }
                    discoveryMediaResult.Body = HighlightedText;

                    lstOfDiscoveryMediaResult.Add(discoveryMediaResult);
                }
            }

            if (Convert.ToString(xDoc).Length > 0)
            {
                lstOfDiscoveryMediaResult = Context.GetNielsenData(clientSettings.ClientGUID, xDoc, lstOfDiscoveryMediaResult);
            }

            return lstOfDiscoveryMediaResult;
        }

        public List<DiscoveryMediaResult> GetNewsResultsForExport(ClientSettings clientSettings, SearchCriteria searchCriteria, List<string> lstMediaIDs, string searchTerm, bool isSelectAll, IQ_MediaTypeModel objSubMediaType)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            DateTime? fromDate = isSelectAll ? searchCriteria.FromDate : (DateTime?)null;
            DateTime? toDate = isSelectAll ? searchCriteria.ToDate : (DateTime?)null; 

            System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), fromDate, toDate));
            SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
            SearchNewsRequest searchNewsRequest = new SearchNewsRequest();

            searchNewsRequest.SearchTerm = searchTerm;
            searchNewsRequest.IsTitleNContentSearch = true;
            searchNewsRequest.IsReturnHighlight = true;
            searchNewsRequest.Facet = false;
            searchNewsRequest.SortFields = "date-";
            searchNewsRequest.IsSentiment = true;
            searchNewsRequest.HighThreshold = float.Parse(clientSettings.SentimentSettings.NMHighThreshold);
            searchNewsRequest.LowThreshold = float.Parse(clientSettings.SentimentSettings.NMLowThreshold);
            searchNewsRequest.SourceType = new List<int>() { (int)PMGSearch.SourceType.OnlineNews };

            foreach (Int16 iqlicense in clientSettings.LicenseList)
            {
                searchNewsRequest.IQLicense.Add(iqlicense);
            }

            if (isSelectAll)
            {
                searchNewsRequest.PageSize = clientSettings.Exportlimit;
                searchNewsRequest.StartDate = searchCriteria.FromDate;
                searchNewsRequest.EndDate = searchCriteria.ToDate;

                if (searchCriteria.AdvanceSearchSettings != null && searchCriteria.AdvanceSearchSettings.NewsSettings != null)
                {
                    if (!String.IsNullOrWhiteSpace(searchCriteria.AdvanceSearchSettings.NewsSettings.SearchTerm))
                    {
                        searchNewsRequest.SearchTerm = searchCriteria.AdvanceSearchSettings.NewsSettings.SearchTerm.Trim();
                    }
                    searchNewsRequest.NewsRegion = searchCriteria.AdvanceSearchSettings.NewsSettings.RegionList;
                    searchNewsRequest.Language = searchCriteria.AdvanceSearchSettings.NewsSettings.LanguageList;
                    searchNewsRequest.Country = searchCriteria.AdvanceSearchSettings.NewsSettings.CountryList;
                    searchNewsRequest.Publications = searchCriteria.AdvanceSearchSettings.NewsSettings.PublicationList;
                    searchNewsRequest.ExcludeDomains = searchCriteria.AdvanceSearchSettings.NewsSettings.ExcludeDomainList;
                    searchNewsRequest.NewsCategory = searchCriteria.AdvanceSearchSettings.NewsSettings.CategoryList;
                    searchNewsRequest.PublicationCategory = searchCriteria.AdvanceSearchSettings.NewsSettings.PublicationCategoryList;
                    searchNewsRequest.Genre = searchCriteria.AdvanceSearchSettings.NewsSettings.GenreList;
                    searchNewsRequest.Market = searchCriteria.AdvanceSearchSettings.NewsSettings.MarketList;
                }
            }
            else
            {
                searchNewsRequest.PageSize = lstMediaIDs.Count;
                searchNewsRequest.IDs = lstMediaIDs;
            }

            SearchNewsResults searchNewsResults = searchEngine.SearchNews(searchNewsRequest);

            int wordsBeforeSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsBeforeSpan"]);
            int wordsAfterSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsAfterSpan"]);
            string seprator = "...&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;...";
            if (searchNewsResults != null && searchNewsResults.newsResults != null)
            {
                foreach (NewsResult newsResult in searchNewsResults.newsResults)
                {
                    DiscoveryMediaResult discoveryMediaResult = new DiscoveryMediaResult();
                    discoveryMediaResult.Date = Convert.ToDateTime(newsResult.date);
                    discoveryMediaResult.Title = newsResult.Title;
                    string body = string.Empty;
                    if (newsResult.Highlights != null && newsResult.Highlights.Count > 0)
                    {
                        List<string> highlights = newsResult.Highlights.Select(s => !String.IsNullOrWhiteSpace(s) ? s.Replace(ConfigurationManager.AppSettings["LexisNexisLineBreakPlaceholder"], " ") : s).ToList();
                        body = CommonFunctions.GetWordsAround(string.Join(" ", highlights), "span", wordsBeforeSpan, wordsAfterSpan + 4, seprator);
                        discoveryMediaResult.Hits = newsResult.Highlights.Count();
                    }
                    discoveryMediaResult.Market = newsResult.IQDmaName;
                    discoveryMediaResult.ArticleURL = newsResult.Article;
                    discoveryMediaResult.Outlet = newsResult.HomeurlDomain;
                    discoveryMediaResult.TimeZone = clientSettings.TimeZone;

                    discoveryMediaResult.SubMediaType = objSubMediaType.SubMediaType;
                    discoveryMediaResult.IQLicense = newsResult.IQLicense;

                    discoveryMediaResult.PositiveSentiment = newsResult.Sentiments.PositiveSentiment;
                    discoveryMediaResult.NegativeSentiment = newsResult.Sentiments.NegativeSentiment;

                    string HighlightedText = body;
                    if (HighlightedText.Length > 255)
                    {
                        int IndexAfter255Char = HighlightedText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                        HighlightedText = IndexAfter255Char == -1 ? HighlightedText.Substring(0, HighlightedText.Length) : HighlightedText.Substring(0, 255 + IndexAfter255Char) + "...";
                    }
                    discoveryMediaResult.Body = HighlightedText;

                    lstOfDiscoveryMediaResult.Add(discoveryMediaResult);
                }

                CommonFunctions.ConvertGMTDateToLocalDate(lstOfDiscoveryMediaResult, clientSettings.GMTHours, clientSettings.DSTHours, "Date");

                //Uri aPublicationUri;
                var distinctDisplayUrl = searchNewsResults.newsResults.Select(a => a.HomeurlDomain).Distinct().ToList();

                var displyUrlXml = new XElement("list",
                                        from string websiteurl in distinctDisplayUrl
                                        select new XElement("item", new XAttribute("url", websiteurl)));

                List<IQCompeteAll> _ListOfIQ_CompeteAll = Context.GetCompeteData(clientSettings.ClientGUID, displyUrlXml, "NM");

                foreach (DiscoveryMediaResult discoveryMediaResult in lstOfDiscoveryMediaResult)
                {
                    //Uri aUri;
                    string href = discoveryMediaResult.Outlet;
                    IQCompeteAll _IQCompeteAll = _ListOfIQ_CompeteAll.Find(a => a.CompeteURL.Equals(href));



                    discoveryMediaResult.Audience = (_IQCompeteAll == null || (_IQCompeteAll.c_uniq_visitor == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.c_uniq_visitor;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.c_uniq_visitor == -1)))
                    {
                        discoveryMediaResult.Audience = null;
                    }

                    discoveryMediaResult.CompeteImage = (_IQCompeteAll.IsCompeteAll ? "<img src=\"../Images/compete.jpg\" style=\"width:14px\"  title=\"Powered by Compete\" />" : "");

                    discoveryMediaResult.IQAdsharevalue = (_IQCompeteAll == null || (_IQCompeteAll.IQ_AdShare_Value == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.IQ_AdShare_Value;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.IQ_AdShare_Value == -1)))
                    {
                        discoveryMediaResult.IQAdsharevalue = null;
                    }
                }
            }

            return lstOfDiscoveryMediaResult;
        }

        public List<DiscoveryMediaResult> GetBlogResultsForExport(ClientSettings clientSettings, SearchCriteria searchCriteria, List<string> lstMediaIDs, string searchTerm, bool isSelectAll, IQ_MediaTypeModel objSubMediaType)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            DateTime? fromDate = isSelectAll ? searchCriteria.FromDate : (DateTime?)null;
            DateTime? toDate = isSelectAll ? searchCriteria.ToDate : (DateTime?)null; 

            System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), fromDate, toDate));
            SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
            SearchSMRequest searchSMRequest = new SearchSMRequest();

            searchSMRequest.SearchTerm = searchTerm;
            searchSMRequest.IsTitleNContentSearch = true;
            searchSMRequest.IsReturnHighlight = true;
            searchSMRequest.Facet = true;
            searchSMRequest.FacetRangeOther = "all";
            searchSMRequest.SortFields = "date-";
            searchSMRequest.IsTaggingExcluded = true;
            searchSMRequest.SourceType = new List<string>() { CommonFunctions.GetEnumDescription(PMGSearch.SourceType.Blog) };
            searchSMRequest.IsSentiment = true;
            searchSMRequest.HighThreshold = float.Parse(clientSettings.SentimentSettings.SMHighThreshold);
            searchSMRequest.LowThreshold = float.Parse(clientSettings.SentimentSettings.SMLowThreshold);

            if (isSelectAll)
            {
                searchSMRequest.PageSize = clientSettings.Exportlimit;
                searchSMRequest.StartDate = searchCriteria.FromDate;
                searchSMRequest.EndDate = searchCriteria.ToDate;

                if (searchCriteria.AdvanceSearchSettings != null && searchCriteria.AdvanceSearchSettings.BlogSettings != null)
                {
                    if (!String.IsNullOrWhiteSpace(searchCriteria.AdvanceSearchSettings.BlogSettings.SearchTerm))
                    {
                        searchSMRequest.SearchTerm = searchCriteria.AdvanceSearchSettings.BlogSettings.SearchTerm.Trim();
                    }
                    searchSMRequest.Author = searchCriteria.AdvanceSearchSettings.BlogSettings.Author;
                    searchSMRequest.SocialMediaSources = searchCriteria.AdvanceSearchSettings.BlogSettings.SourceList;
                    searchSMRequest.Title = searchCriteria.AdvanceSearchSettings.BlogSettings.Title;
                    searchSMRequest.ExcludeDomains = searchCriteria.AdvanceSearchSettings.BlogSettings.ExcludeDomainList;
                }
            }
            else
            {
                searchSMRequest.PageSize = lstMediaIDs.Count;
                searchSMRequest.ids = lstMediaIDs;
            }

            SearchSMResult searchSMResult = searchEngine.SearchSocialMedia(searchSMRequest);

            int wordsBeforeSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsBeforeSpan"]);
            int wordsAfterSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsAfterSpan"]);
            string seprator = "...&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;...";
            if (searchSMResult != null && searchSMResult.smResults != null)
            {
                foreach (SMResult smResult in searchSMResult.smResults)
                {
                    DiscoveryMediaResult discoveryMediaResult = new DiscoveryMediaResult();
                    discoveryMediaResult.Date = Convert.ToDateTime(smResult.itemHarvestDate_DT);
                    discoveryMediaResult.Title = smResult.description;
                    string body = string.Empty;

                    if (smResult.Highlights != null && smResult.Highlights.Count > 0)
                    {
                        body = CommonFunctions.GetWordsAround(string.Join(" ", smResult.Highlights), "span", wordsBeforeSpan, wordsAfterSpan + 4, seprator);
                        discoveryMediaResult.Hits = smResult.Highlights.Count();
                    }

                    discoveryMediaResult.ArticleURL = smResult.link;
                    discoveryMediaResult.Outlet = smResult.HomeurlDomain;
                    discoveryMediaResult.TimeZone = clientSettings.TimeZone;
                    discoveryMediaResult.SubMediaType = objSubMediaType.SubMediaType;

                    discoveryMediaResult.PositiveSentiment = smResult.Sentiments.PositiveSentiment;
                    discoveryMediaResult.NegativeSentiment = smResult.Sentiments.NegativeSentiment;

                    string HighlightedText = body;
                    if (HighlightedText.Length > 255)
                    {
                        int IndexAfter255Char = HighlightedText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                        HighlightedText = IndexAfter255Char == -1 ? HighlightedText.Substring(0, HighlightedText.Length) : HighlightedText.Substring(0, 255 + IndexAfter255Char) + "...";
                    }
                    discoveryMediaResult.Body = HighlightedText;

                    lstOfDiscoveryMediaResult.Add(discoveryMediaResult);
                }

                CommonFunctions.ConvertGMTDateToLocalDate(lstOfDiscoveryMediaResult, clientSettings.GMTHours, clientSettings.DSTHours, "Date");

                List<SMResult> lstSMResults = new List<SMResult>();


                lstSMResults = searchSMResult.smResults.Select(a => new SMResult()
                {
                    HomeurlDomain = a.HomeurlDomain,
                    feedClass = !string.IsNullOrWhiteSpace(a.feedClass) ? a.feedClass : string.Empty
                }
                                                                                ).GroupBy(h => h.HomeurlDomain)
                                                                                    .Select(s => s.First()).ToList();

                var displyUrlXml = new XElement("list",
                                        from SMResult smres in lstSMResults
                                        select new XElement("item", new XAttribute("url", smres.HomeurlDomain), new XAttribute("sourceCategory", GetFeedClass(smres.feedClass))));

                List<IQCompeteAll> _ListOfIQ_CompeteAll = Context.GetCompeteData(clientSettings.ClientGUID, displyUrlXml, "SM");


                foreach (DiscoveryMediaResult discoveryMediaResult in lstOfDiscoveryMediaResult)
                {
                    string href = discoveryMediaResult.Outlet;
                    IQCompeteAll _IQCompeteAll = _ListOfIQ_CompeteAll.Find(a => a.CompeteURL.Equals(href));



                    discoveryMediaResult.Audience = (_IQCompeteAll == null || (_IQCompeteAll.c_uniq_visitor == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.c_uniq_visitor;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.c_uniq_visitor == -1)))
                    {
                        discoveryMediaResult.Audience = null;
                    }

                    discoveryMediaResult.CompeteImage = (_IQCompeteAll.IsCompeteAll ? "<img src=\"../Images/compete.jpg\" style=\"width:14px\"  title=\"Powered by Compete\" />" : "");

                    discoveryMediaResult.IQAdsharevalue = (_IQCompeteAll == null || (_IQCompeteAll.IQ_AdShare_Value == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.IQ_AdShare_Value;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.IQ_AdShare_Value == -1)))
                    {
                        discoveryMediaResult.IQAdsharevalue = null;
                    }
                }
            }

            return lstOfDiscoveryMediaResult;
        }

        public List<DiscoveryMediaResult> GetForumResultsForExport(ClientSettings clientSettings, SearchCriteria searchCriteria, List<string> lstMediaIDs, string searchTerm, bool isSelectAll, IQ_MediaTypeModel objSubMediaType)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            DateTime? fromDate = isSelectAll ? searchCriteria.FromDate : (DateTime?)null;
            DateTime? toDate = isSelectAll ? searchCriteria.ToDate : (DateTime?)null;

            System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), fromDate, toDate));
            SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
            SearchSMRequest searchSMRequest = new SearchSMRequest();

            searchSMRequest.SearchTerm = searchTerm;
            searchSMRequest.IsTitleNContentSearch = true;
            searchSMRequest.IsReturnHighlight = true;
            searchSMRequest.Facet = true;
            searchSMRequest.FacetRangeOther = "all";
            searchSMRequest.SortFields = "date-";
            searchSMRequest.IsTaggingExcluded = true;
            searchSMRequest.SourceType = new List<string>() { CommonFunctions.GetEnumDescription(PMGSearch.SourceType.Forum) };
            searchSMRequest.IsSentiment = true;
            searchSMRequest.HighThreshold = float.Parse(clientSettings.SentimentSettings.SMHighThreshold);
            searchSMRequest.LowThreshold = float.Parse(clientSettings.SentimentSettings.SMLowThreshold);

            if (isSelectAll)
            {
                searchSMRequest.PageSize = clientSettings.Exportlimit;
                searchSMRequest.StartDate = searchCriteria.FromDate;
                searchSMRequest.EndDate = searchCriteria.ToDate;

                if (searchCriteria.AdvanceSearchSettings != null && searchCriteria.AdvanceSearchSettings.ForumSettings != null)
                {
                    if (!String.IsNullOrWhiteSpace(searchCriteria.AdvanceSearchSettings.ForumSettings.SearchTerm))
                    {
                        searchSMRequest.SearchTerm = searchCriteria.AdvanceSearchSettings.ForumSettings.SearchTerm.Trim();
                    }
                    searchSMRequest.Author = searchCriteria.AdvanceSearchSettings.ForumSettings.Author;
                    searchSMRequest.SocialMediaSources = searchCriteria.AdvanceSearchSettings.ForumSettings.SourceList;
                    searchSMRequest.Title = searchCriteria.AdvanceSearchSettings.ForumSettings.Title;
                    searchSMRequest.ExcludeDomains = searchCriteria.AdvanceSearchSettings.ForumSettings.ExcludeDomainList;
                    if (searchCriteria.AdvanceSearchSettings.ForumSettings.SourceTypeList != null && searchCriteria.AdvanceSearchSettings.ForumSettings.SourceTypeList.Count > 0)
                    {
                        searchSMRequest.SourceType = searchCriteria.AdvanceSearchSettings.ForumSettings.SourceTypeList;
                    }
                }
            }
            else
            {
                searchSMRequest.PageSize = lstMediaIDs.Count;
                searchSMRequest.ids = lstMediaIDs;
            }

            SearchSMResult searchSMResult = searchEngine.SearchSocialMedia(searchSMRequest);

            int wordsBeforeSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsBeforeSpan"]);
            int wordsAfterSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsAfterSpan"]);
            string seprator = "...&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;...";
            if (searchSMResult != null && searchSMResult.smResults != null)
            {
                foreach (SMResult smResult in searchSMResult.smResults)
                {
                    DiscoveryMediaResult discoveryMediaResult = new DiscoveryMediaResult();
                    discoveryMediaResult.Date = Convert.ToDateTime(smResult.itemHarvestDate_DT);
                    discoveryMediaResult.Title = smResult.description;
                    string body = string.Empty;

                    if (smResult.Highlights != null && smResult.Highlights.Count > 0)
                    {
                        body = CommonFunctions.GetWordsAround(string.Join(" ", smResult.Highlights), "span", wordsBeforeSpan, wordsAfterSpan + 4, seprator);
                        discoveryMediaResult.Hits = smResult.Highlights.Count();
                    }

                    discoveryMediaResult.Body = body;
                    discoveryMediaResult.ArticleURL = smResult.link;
                    discoveryMediaResult.Outlet = smResult.HomeurlDomain;
                    discoveryMediaResult.TimeZone = clientSettings.TimeZone;
                    discoveryMediaResult.SubMediaType = objSubMediaType.SubMediaType;

                    discoveryMediaResult.PositiveSentiment = smResult.Sentiments.PositiveSentiment;
                    discoveryMediaResult.NegativeSentiment = smResult.Sentiments.NegativeSentiment;

                    string HighlightedText = body;
                    if (HighlightedText.Length > 255)
                    {
                        int IndexAfter255Char = HighlightedText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                        HighlightedText = IndexAfter255Char == -1 ? HighlightedText.Substring(0, HighlightedText.Length) : HighlightedText.Substring(0, 255 + IndexAfter255Char) + "...";
                    }
                    discoveryMediaResult.Body = HighlightedText;

                    lstOfDiscoveryMediaResult.Add(discoveryMediaResult);
                }

                CommonFunctions.ConvertGMTDateToLocalDate(lstOfDiscoveryMediaResult, clientSettings.GMTHours, clientSettings.DSTHours, "Date");

                List<SMResult> lstSMResults = new List<SMResult>();


                lstSMResults = searchSMResult.smResults.Select(a => new SMResult()
                {
                    HomeurlDomain = a.HomeurlDomain,
                    feedClass = !string.IsNullOrWhiteSpace(a.feedClass) ? a.feedClass : string.Empty
                }
                                                                                ).GroupBy(h => h.HomeurlDomain)
                                                                                    .Select(s => s.First()).ToList();

                var displyUrlXml = new XElement("list",
                                        from SMResult smres in lstSMResults
                                        select new XElement("item", new XAttribute("url", smres.HomeurlDomain), new XAttribute("sourceCategory", GetFeedClass(smres.feedClass))));

                List<IQCompeteAll> _ListOfIQ_CompeteAll = Context.GetCompeteData(clientSettings.ClientGUID, displyUrlXml, "SM");


                foreach (DiscoveryMediaResult discoveryMediaResult in lstOfDiscoveryMediaResult)
                {
                    string href = discoveryMediaResult.Outlet;
                    IQCompeteAll _IQCompeteAll = _ListOfIQ_CompeteAll.Find(a => a.CompeteURL.Equals(href));



                    discoveryMediaResult.Audience = (_IQCompeteAll == null || (_IQCompeteAll.c_uniq_visitor == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.c_uniq_visitor;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.c_uniq_visitor == -1)))
                    {
                        discoveryMediaResult.Audience = null;
                    }

                    discoveryMediaResult.CompeteImage = (_IQCompeteAll.IsCompeteAll ? "<img src=\"../Images/compete.jpg\" style=\"width:14px\"  title=\"Powered by Compete\" />" : "");

                    discoveryMediaResult.IQAdsharevalue = (_IQCompeteAll == null || (_IQCompeteAll.IQ_AdShare_Value == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.IQ_AdShare_Value;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.IQ_AdShare_Value == -1)))
                    {
                        discoveryMediaResult.IQAdsharevalue = null;
                    }
                }
            }

            return lstOfDiscoveryMediaResult;
        }

        public List<DiscoveryMediaResult> GetProQuestResultsForExport(ClientSettings clientSettings, SearchCriteria searchCriteria, List<string> lstMediaIDs, string searchTerm, bool isSelectAll, IQ_MediaTypeModel objSubMediaType)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            DateTime? fromDate = isSelectAll ? searchCriteria.FromDate : (DateTime?)null;
            DateTime? toDate = isSelectAll ? searchCriteria.ToDate : (DateTime?)null;

            System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.PQ.ToString(), fromDate, toDate));
            SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
            SearchProQuestRequest searchPQRequest = new SearchProQuestRequest();

            searchPQRequest.SearchTerm = searchTerm;
            searchPQRequest.IsReturnHighlight = true;
            searchPQRequest.Facet = false;
            searchPQRequest.SortFields = "date-";
            searchPQRequest.IsSentiment = true;
            searchPQRequest.HighThreshold = float.Parse(clientSettings.SentimentSettings.PQHighThreshold);
            searchPQRequest.LowThreshold = float.Parse(clientSettings.SentimentSettings.PQLowThreshold);

            if (isSelectAll)
            {
                searchPQRequest.PageSize = clientSettings.Exportlimit;
                searchPQRequest.StartDate = searchCriteria.FromDate;
                searchPQRequest.EndDate = searchCriteria.ToDate;

                if (searchCriteria.AdvanceSearchSettings != null && searchCriteria.AdvanceSearchSettings.ProQuestSettings != null)
                {
                    if (!String.IsNullOrWhiteSpace(searchCriteria.AdvanceSearchSettings.ProQuestSettings.SearchTerm))
                    {
                        searchPQRequest.SearchTerm = searchCriteria.AdvanceSearchSettings.ProQuestSettings.SearchTerm.Trim();
                    }
                    searchPQRequest.Publications = searchCriteria.AdvanceSearchSettings.ProQuestSettings.PublicationList;
                    searchPQRequest.Authors = searchCriteria.AdvanceSearchSettings.ProQuestSettings.AuthorList;
                    searchPQRequest.Languages = searchCriteria.AdvanceSearchSettings.ProQuestSettings.LanguageList;
                }
            }
            else
            {
                searchPQRequest.PageSize = lstMediaIDs.Count;
                searchPQRequest.IDs = lstMediaIDs;
            }
            
            bool isError;
            SearchProQuestResult searchPQResults = searchEngine.SearchProQuest(searchPQRequest, false, out isError);

            int wordsBeforeSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsBeforeSpan"]);
            int wordsAfterSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsAfterSpan"]);
            string seprator = "...&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;...";
            if (searchPQResults != null && searchPQResults.ProQuestResults != null)
            {
                foreach (ProQuestResult pqResult in searchPQResults.ProQuestResults)
                {
                    DiscoveryMediaResult discoveryMediaResult = new DiscoveryMediaResult();
                    discoveryMediaResult.Date = Convert.ToDateTime(pqResult.MediaDate);
                    discoveryMediaResult.Title = pqResult.Title;
                    string body = string.Empty;
                    if (pqResult.Highlights != null && pqResult.Highlights.Count > 0)
                    {
                        body = CommonFunctions.GetWordsAround(string.Join(" ", pqResult.Highlights), "span", wordsBeforeSpan, wordsAfterSpan + 4, seprator);
                        discoveryMediaResult.Hits = pqResult.Highlights.Count();
                    }

                    discoveryMediaResult.Body = body.Replace("\r\n", " ");
                    discoveryMediaResult.Outlet = pqResult.Publication;
                    discoveryMediaResult.TimeZone = clientSettings.TimeZone;
                    discoveryMediaResult.ArticleURL = ConfigurationManager.AppSettings["ProQuestUrl"] + pqResult.IQSeqID.ToString();

                    discoveryMediaResult.SubMediaType = objSubMediaType.SubMediaType;

                    discoveryMediaResult.PositiveSentiment = pqResult.Sentiments.PositiveSentiment;
                    discoveryMediaResult.NegativeSentiment = pqResult.Sentiments.NegativeSentiment;
                    
                    lstOfDiscoveryMediaResult.Add(discoveryMediaResult);
                }
            }

            return lstOfDiscoveryMediaResult;
        }

        public List<DiscoveryMediaResult> GetLexisNexisResultsForExport(ClientSettings clientSettings, SearchCriteria searchCriteria, List<string> lstMediaIDs, string searchTerm, bool isSelectAll, IQ_MediaTypeModel objSubMediaType)
        {
            List<DiscoveryMediaResult> lstOfDiscoveryMediaResult = new List<DiscoveryMediaResult>();
            DateTime? fromDate = isSelectAll ? searchCriteria.FromDate : (DateTime?)null;
            DateTime? toDate = isSelectAll ? searchCriteria.ToDate : (DateTime?)null;

            System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), fromDate, toDate));
            SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
            SearchNewsRequest searchNewsRequest = new SearchNewsRequest();

            searchNewsRequest.SearchTerm = searchTerm;
            searchNewsRequest.IsTitleNContentSearch = true;
            searchNewsRequest.IsReturnHighlight = true;
            searchNewsRequest.Facet = false;
            searchNewsRequest.SortFields = "date-";
            searchNewsRequest.IsSentiment = true;
            searchNewsRequest.HighThreshold = float.Parse(clientSettings.SentimentSettings.NMHighThreshold);
            searchNewsRequest.LowThreshold = float.Parse(clientSettings.SentimentSettings.NMLowThreshold);
            searchNewsRequest.SourceType = new List<int>() { (int)PMGSearch.SourceType.Print };

            foreach (Int16 iqlicense in clientSettings.LicenseList)
            {
                searchNewsRequest.IQLicense.Add(iqlicense);
            }

            if (isSelectAll)
            {
                searchNewsRequest.PageSize = clientSettings.Exportlimit;
                searchNewsRequest.StartDate = searchCriteria.FromDate;
                searchNewsRequest.EndDate = searchCriteria.ToDate;

                if (searchCriteria.AdvanceSearchSettings != null && searchCriteria.AdvanceSearchSettings.LexisNexisSettings != null)
                {
                    if (!String.IsNullOrWhiteSpace(searchCriteria.AdvanceSearchSettings.LexisNexisSettings.SearchTerm))
                    {
                        searchNewsRequest.SearchTerm = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.SearchTerm.Trim();
                    }
                    searchNewsRequest.NewsRegion = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.RegionList;
                    searchNewsRequest.Language = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.LanguageList;
                    searchNewsRequest.Country = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.CountryList;
                    searchNewsRequest.Publications = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.PublicationList;
                    searchNewsRequest.ExcludeDomains = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.ExcludeDomainList;
                    searchNewsRequest.NewsCategory = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.CategoryList;
                    searchNewsRequest.PublicationCategory = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.PublicationCategoryList;
                    searchNewsRequest.Genre = searchCriteria.AdvanceSearchSettings.LexisNexisSettings.GenreList;
                }
            }
            else
            {
                searchNewsRequest.PageSize = lstMediaIDs.Count;
                searchNewsRequest.IDs = lstMediaIDs;
            }

            SearchNewsResults searchNewsResults = searchEngine.SearchNews(searchNewsRequest);

            int wordsBeforeSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsBeforeSpan"]);
            int wordsAfterSpan = Convert.ToInt32(ConfigurationManager.AppSettings["HighlightWordsAfterSpan"]);
            string seprator = "...&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;...";
            if (searchNewsResults != null && searchNewsResults.newsResults != null)
            {
                foreach (NewsResult newsResult in searchNewsResults.newsResults)
                {
                    DiscoveryMediaResult discoveryMediaResult = new DiscoveryMediaResult();
                    discoveryMediaResult.Date = Convert.ToDateTime(newsResult.date);
                    discoveryMediaResult.Title = newsResult.Title;
                    string body = string.Empty;
                    if (newsResult.Highlights != null && newsResult.Highlights.Count > 0)
                    {
                        List<string> highlights = newsResult.Highlights.Select(s => !String.IsNullOrWhiteSpace(s) ? s.Replace(ConfigurationManager.AppSettings["LexisNexisLineBreakPlaceholder"], " ") : s).ToList();
                        body = CommonFunctions.GetWordsAround(string.Join(" ", highlights), "span", wordsBeforeSpan, wordsAfterSpan + 4, seprator);
                        discoveryMediaResult.Hits = newsResult.Highlights.Count();
                    }
                    discoveryMediaResult.Market = newsResult.IQDmaName;
                    discoveryMediaResult.ArticleURL = newsResult.Article;
                    discoveryMediaResult.Outlet = newsResult.HomeurlDomain;
                    discoveryMediaResult.TimeZone = clientSettings.TimeZone;

                    discoveryMediaResult.SubMediaType = objSubMediaType.SubMediaType;
                    discoveryMediaResult.IQLicense = newsResult.IQLicense;

                    discoveryMediaResult.PositiveSentiment = newsResult.Sentiments.PositiveSentiment;
                    discoveryMediaResult.NegativeSentiment = newsResult.Sentiments.NegativeSentiment;

                    string HighlightedText = body;
                    if (HighlightedText.Length > 255)
                    {
                        int IndexAfter255Char = HighlightedText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                        HighlightedText = IndexAfter255Char == -1 ? HighlightedText.Substring(0, HighlightedText.Length) : HighlightedText.Substring(0, 255 + IndexAfter255Char) + "...";
                    }
                    discoveryMediaResult.Body = HighlightedText;

                    lstOfDiscoveryMediaResult.Add(discoveryMediaResult);
                }

                CommonFunctions.ConvertGMTDateToLocalDate(lstOfDiscoveryMediaResult, clientSettings.GMTHours, clientSettings.DSTHours, "Date");

                var distinctDisplayUrl = searchNewsResults.newsResults.Select(a => a.HomeurlDomain).Distinct().ToList();

                var displyUrlXml = new XElement("list",
                                        from string websiteurl in distinctDisplayUrl
                                        select new XElement("item", new XAttribute("url", websiteurl)));

                List<IQCompeteAll> _ListOfIQ_CompeteAll = Context.GetCompeteData(clientSettings.ClientGUID, displyUrlXml, "NM");

                foreach (DiscoveryMediaResult discoveryMediaResult in lstOfDiscoveryMediaResult)
                {
                    string href = discoveryMediaResult.Outlet;
                    IQCompeteAll _IQCompeteAll = _ListOfIQ_CompeteAll.Find(a => a.CompeteURL.Equals(href));



                    discoveryMediaResult.Audience = (_IQCompeteAll == null || (_IQCompeteAll.c_uniq_visitor == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.c_uniq_visitor;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.c_uniq_visitor == -1)))
                    {
                        discoveryMediaResult.Audience = null;
                    }

                    discoveryMediaResult.CompeteImage = (_IQCompeteAll.IsCompeteAll ? "<img src=\"../Images/compete.jpg\" style=\"width:14px\"  title=\"Powered by Compete\" />" : "");

                    discoveryMediaResult.IQAdsharevalue = (_IQCompeteAll == null || (_IQCompeteAll.IQ_AdShare_Value == null || !_IQCompeteAll.IsUrlFound)) ? null : _IQCompeteAll.IQ_AdShare_Value;
                    if ((_IQCompeteAll != null && (_IQCompeteAll.IQ_AdShare_Value == -1)))
                    {
                        discoveryMediaResult.IQAdsharevalue = null;
                    }
                }
            }

            return lstOfDiscoveryMediaResult;
        }
    }
}
