using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using IQCommon.Model;
using IQMedia.Service.Common.Util;
using IQMedia.Service.Domain;
using PMGSearch;

namespace IQMedia.Service.Logic
{
    public class FeedReportLogic : BaseLogic, ILogic
    {
        HttpWebRequest _objWebRequestAsync = null;

        public int? InsertFeedReport(Int64 p_ReportID, string p_MediaIDXML)
        {
            try
            {
                return Context.InsertFeedsReport(p_ReportID, p_MediaIDXML).FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public ClientSettings GetClientSettings(Guid customerGuid)
        {
            return Context.GetClientSettings(customerGuid);
        }

        public string GetNewsContent(string p_articleID, string logPrefix = null)
        {
            try
            {
                Logger.Info(logPrefix + "fetch content for news media article id " + p_articleID);

                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), null, null, true));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
                SearchNewsRequest searchNewsRequest = new SearchNewsRequest();
                searchNewsRequest.IDs = new List<string>() { p_articleID };

                SearchNewsResults searchNewsResults = searchEngine.SearchNewsByID(searchNewsRequest);
                string content = searchNewsResults.newsResults.Where(w => string.Compare(w.IQSeqID, p_articleID, true) == 0).Select(s => s.Content).FirstOrDefault();
                return content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public string GetSocialMediaContent(string p_articleID, string logPrefix = null)
        {
            try
            {
                Logger.Info(logPrefix + "fetch content for social media article id " + p_articleID);

                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.MO.ToString(), null, null, true));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
                SearchSMRequest searchSMRequest = new SearchSMRequest();
                searchSMRequest.ids = new List<string>() { p_articleID };

                SearchSMResult searchSMResult = searchEngine.SearchSocialMediaByID(searchSMRequest);
                string content = searchSMResult.smResults.Where(w => string.Compare(w.IQSeqID, p_articleID, true) == 0).Select(s => s.content).FirstOrDefault();
                return content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public string GetProQuestContent(string articleID, string logPrefix = null)
        {
            try
            {
                Logger.Info(logPrefix + "fetch content for ProQuest article id " + articleID);

                System.Uri PMGSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.PQ.ToString(), null, null, true));
                SearchEngine searchEngine = new SearchEngine(PMGSearchRequestUrl);
                SearchProQuestRequest searchPQRequest = new SearchProQuestRequest();
                searchPQRequest.IDs = new List<string>() { articleID };

                SearchProQuestResult searchPQResult = searchEngine.SearchProQuestByID(searchPQRequest);
                string content = searchPQResult.ProQuestResults.Where(w => string.Compare(w.IQSeqID.ToString(), articleID, true) == 0).Select(s => s.Content).FirstOrDefault();
                return content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private List<FeedsSearch.Hit> GetExportMediaResultsBySearchCriteria(string searchCriteria, string sortType, ClientSettings clientSettings, List<IQ_MediaTypeModel> lstMasterMediaTypes, int batchSize, string logPrefix)
        {
            List<FeedsSearch.Hit> totalHits = new List<FeedsSearch.Hit>();
            System.Uri SolrSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.FE.ToString(), null, null));
            
            Logger.Info(String.Format("{0}Deserializing search criteria", logPrefix));

            FeedsSearchCriteria objSearchCriteria = new FeedsSearchCriteria();
            objSearchCriteria = (FeedsSearchCriteria)CommonFunctions.DeserialiazeXml(searchCriteria, objSearchCriteria);

            Logger.Info(String.Format("{0}Populating search request object", logPrefix));

            FeedsSearch.SearchRequest searchRequest = BuildFeedsSearchRequest(objSearchCriteria, sortType, clientSettings.ClientGUID, lstMasterMediaTypes, logPrefix);

            searchRequest.PageSize = batchSize;

            Logger.Info(String.Format("{0}Executing solr query tasks", logPrefix));

            // Perform first query normally to get the total number of hits
            FeedsSearch.SearchEngine searchEngine = new FeedsSearch.SearchEngine(SolrSearchRequestUrl);
            Dictionary<string, FeedsSearch.SearchResult> dictSearchResults = searchEngine.Search(searchRequest);

            FeedsSearch.SearchResult searchResult = dictSearchResults["Results"];
            totalHits.AddRange(searchResult.Hits);
            int exportLimit = searchResult.TotalHitCount < clientSettings.FeedsExportLimit ? searchResult.TotalHitCount : clientSettings.FeedsExportLimit;

            // Run all subsequent queries in parallel
            List<Task> lstTask = new List<Task>();
            for (int i = batchSize; i < exportLimit; i += batchSize)
            {
                // Clone the search request object before passing it to the task, to avoid conflicts with the changing FromRecordID property
                int index = i;
                FeedsSearch.SearchRequest tempRequest = searchRequest.DeepCopy();
                tempRequest.FromRecordID = index;
                if (i + batchSize > exportLimit)
                {
                    tempRequest.PageSize = exportLimit - i;
                }
                lstTask.Add(Task.Factory.StartNew((object obj) => GetExportMediaResults_Task(tempRequest, SolrSearchRequestUrl), TaskCreationOptions.AttachedToParent));
            }

            try
            {
                Task.WaitAll(lstTask.ToArray(), 90000);
            }
            catch (AggregateException ex)
            {
                Logger.Error(logPrefix + "Error encountered while querying solr", ex);
            }
            catch (Exception ex)
            {
                Logger.Error(logPrefix + "Error encountered while querying solr", ex);
            }

            foreach (var tsk in lstTask)
            {
                List<FeedsSearch.Hit> hits = ((Task<List<FeedsSearch.Hit>>)tsk).Result;
                totalHits.AddRange(hits);
            }

            return totalHits;
        }

        private List<FeedsSearch.Hit> GetExportMediaResults_Task(FeedsSearch.SearchRequest searchRequest, Uri searchRequestUrl)
        {
            FeedsSearch.SearchEngine searchEngine = new FeedsSearch.SearchEngine(searchRequestUrl);
            Dictionary<string, FeedsSearch.SearchResult> dictSearchResults = searchEngine.Search(searchRequest);

            return dictSearchResults["Results"].Hits;
        }

        private List<FeedsSearch.Hit> GetExportMediaResultsByID(List<string> mediaIDs, int batchSize, string logPrefix)
        {
            System.Uri SolrSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.FE.ToString(), null, null));
            FeedsSearch.SearchEngine searchEngine = new FeedsSearch.SearchEngine(SolrSearchRequestUrl);

            List<FeedsSearch.Hit> totalHits = new List<FeedsSearch.Hit>();

            for (int i = 0; i < mediaIDs.Count; i += batchSize)
            {
                FeedsSearch.SearchRequest searchRequest = new FeedsSearch.SearchRequest();
                searchRequest.IncludeDeleted = true;
                searchRequest.IsOnlyParents = false;
                searchRequest.PageSize = batchSize;

                if (i + batchSize > mediaIDs.Count)
                {
                    searchRequest.MediaIDs = mediaIDs.GetRange(i, mediaIDs.Count - i);
                }
                else
                {
                    searchRequest.MediaIDs = mediaIDs.GetRange(i, batchSize);
                }

                Logger.Debug(String.Format("{0}Fetch content for media items {1}-{2}.", logPrefix, i, i + searchRequest.MediaIDs.Count));

                Dictionary<string, FeedsSearch.SearchResult> dictResults = searchEngine.Search(searchRequest);
                totalHits.AddRange(dictResults["Results"].Hits);
            }

            return totalHits;
        }

        private List<FeedsMediaResult> FillMediaResults(List<FeedsSearch.Hit> hits, string sortType, ClientSettings clientSettings, List<IQ_MediaTypeModel> lstMasterMediaTypes, bool getTVUrl, XDocument xDocPlayerUrls, string logPrefix)
        {
            string highlightingText = String.Empty;
            int IndexAfter255Char;
            Uri publisherUri;

            // Sort the results in the same order as they were displayed in the web
            switch (sortType)
            {
                case "ArticleWeight-":
                    hits = hits.OrderByDescending(o => o.IQProminence).ThenByDescending(o => o.MediaDate).ToList();
                    break;
                case "OutletWeight-":
                    hits = hits.OrderByDescending(o => o.IQProminenceMultiplier).ThenByDescending(o => o.MediaDate).ToList();
                    break;
                case "Date-":
                    hits = hits.OrderByDescending(o => o.MediaDate).ToList();
                    break;
                case "Date+":
                    hits = hits.OrderBy(o => o.MediaDate).ToList();
                    break;
                default:
                    Logger.Error(String.Format("{0}Encountered unsupported sort type: {1}", logPrefix, sortType));
                    break;
            }

            CommonFunctions.ConvertGMTDateToLocalDate(hits, clientSettings.GMTHours, clientSettings.DSTHours, "MediaDate");

            List<FeedsMediaResult> lstMediaResults = new List<FeedsMediaResult>();

            Dictionary<long, string> dictPlayerUrls = new Dictionary<long, string>();
            if (xDocPlayerUrls != null && xDocPlayerUrls.Descendants("TVUrl").Count() > 0)
            {
                dictPlayerUrls = xDocPlayerUrls.Descendants("TVUrl").ToDictionary(d => Int64.Parse(d.Descendants("MediaID").First().Value), d => d.Descendants("Url").First().Value);
            }

            Logger.Info(String.Format("{0}Create FeedsMediaResult objects.", logPrefix));

            bool isMissingTVUrls = false;
            foreach (FeedsSearch.Hit hit in hits)
            {
                IQ_MediaTypeModel masterMediaType = lstMasterMediaTypes.FirstOrDefault(s => s.SubMediaType == hit.v5MediaCategory);

                if (masterMediaType != null)
                {
                    FeedsMediaResult mediaResult = new FeedsMediaResult();
                    mediaResult.TimeZone = clientSettings.TimeZone;
                    mediaResult.Source = masterMediaType.DisplayName;
                    mediaResult.Title = hit.Title;
                    mediaResult.AgentName = hit.SearchAgentName;
                    mediaResult.NumHits = hit.NumberOfHits;
                    mediaResult.PositiveSentiment = hit.PositiveSentiment;
                    mediaResult.NegativeSentiment = hit.NegativeSentiment;

                    switch (masterMediaType.DataModelType)
                    {
                        case "TV":
                            mediaResult.TimeZone = hit.TimeZone;
                            mediaResult.MediaDate = hit.LocalDate;
                            mediaResult.Outlet = hit.Outlet;
                            mediaResult.Market = hit.Market;
                            if (clientSettings.IsNielsenData)
                            {
                                mediaResult.Audience = hit.Audience;
                                mediaResult.MediaValue = clientSettings.UseProminenceMediaValue == true ? hit.MediaValue * hit.IQProminenceMultiplier : hit.MediaValue;
                                mediaResult.NationalAudience = hit.NationalAudience;
                                mediaResult.NationalMediaValue = hit.NationalMediaValue;
                            }

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                HighlightedCCOutput highlightedCCOutput = new HighlightedCCOutput();
                                highlightedCCOutput = (HighlightedCCOutput)CommonFunctions.DeserialiazeXml(hit.HighlightingText, highlightedCCOutput);

                                if (highlightedCCOutput.CC != null)
                                {
                                    highlightingText = string.Join(" ", highlightedCCOutput.CC.Select(c => c.Text)).Replace("&lt;", "<").Replace("&gt;", ">");
                                    if (highlightingText.Length > 255)
                                    {
                                        IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                        highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                    }
                                    mediaResult.HighlightingText = highlightingText;
                                }
                            }

                            if (getTVUrl)
                            {
                                if (dictPlayerUrls != null)
                                {
                                    if (dictPlayerUrls.ContainsKey(hit.MediaID))
                                    {
                                        mediaResult.Url = dictPlayerUrls[hit.MediaID];
                                    }
                                    else
                                    {
                                        isMissingTVUrls = true;
                                        Logger.Warning(String.Format("{0}No TV url xml node was found for media ID {1}.", logPrefix, hit.MediaID));
                                    }
                                }
                                else
                                {
                                    Logger.Warning(String.Format("{0}User chose to generate TV urls but no xml was generated.", logPrefix));
                                }
                            }
                            else
                            {
                                mediaResult.Url = "N/A";
                            }

                            break;
                        case "NM":
                            mediaResult.MediaDate = hit.MediaDate;
                            if (hit.IQLicense == 3)
                            {
                                mediaResult.Source = "LexisNexis(R)";
                            }
                            mediaResult.Outlet = Uri.TryCreate(hit.Publication, UriKind.Absolute, out publisherUri) ? publisherUri.Host.Replace("www.", string.Empty) : hit.Publication;
                            mediaResult.Url = hit.Url;
                            mediaResult.Market = String.Compare(hit.Market, "Unknown", true) == 0 || String.IsNullOrWhiteSpace(hit.Market) ? "Global" : hit.Market;
                            if (clientSettings.IsCompeteData)
                            {
                                mediaResult.Audience = hit.Audience;
                                mediaResult.AudienceSource = !String.IsNullOrEmpty(hit.AudienceType) && hit.AudienceType.ToUpper() == "A" ? "(c)" : String.Empty;
                                mediaResult.MediaValue = clientSettings.UseProminenceMediaValue == true ? hit.MediaValue * hit.IQProminenceMultiplier : hit.MediaValue;
                            }

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                HighlightedNewsOutput highlightedNewsOutput = new HighlightedNewsOutput();
                                highlightedNewsOutput = (HighlightedNewsOutput)CommonFunctions.DeserialiazeXml(hit.HighlightingText, highlightedNewsOutput);

                                if (highlightedNewsOutput.Highlights != null)
                                {
                                    highlightingText = string.Join(" ", highlightedNewsOutput.Highlights.Select(c => c));
                                    if (highlightingText.Length > 255)
                                    {
                                        IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                        highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                    }
                                    mediaResult.HighlightingText = highlightingText;
                                }
                            }
                            break;
                        case "SM":
                            mediaResult.MediaDate = hit.MediaDate;
                            mediaResult.Outlet = Uri.TryCreate(hit.Publication, UriKind.Absolute, out publisherUri) ? publisherUri.Host.Replace("www.", string.Empty) : hit.Publication;
                            mediaResult.Url = hit.Url;
                            if (clientSettings.IsCompeteData)
                            {
                                if (masterMediaType.UseAudience)
                                {
                                    mediaResult.Audience = hit.Audience;
                                    mediaResult.AudienceSource = !String.IsNullOrEmpty(hit.AudienceType) && hit.AudienceType.ToUpper() == "A" ? "(c)" : String.Empty;
                                }
                                if (masterMediaType.UseMediaValue)
                                {
                                    mediaResult.MediaValue = clientSettings.UseProminenceMediaValue == true ? hit.MediaValue * hit.IQProminenceMultiplier : hit.MediaValue;
                                }
                            }

                            if (!String.IsNullOrEmpty(hit.ArticleStats))
                            {
                                ArticleStatsModel articleStatsModel = new ArticleStatsModel();
                                mediaResult.ArticleStats = (ArticleStatsModel)CommonFunctions.DeserialiazeXml(hit.ArticleStats, articleStatsModel);
                            }

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                HighlightedSMOutput highlightedSMOutput = new HighlightedSMOutput();
                                highlightedSMOutput = (HighlightedSMOutput)CommonFunctions.DeserialiazeXml(hit.HighlightingText, highlightedSMOutput);

                                if (highlightedSMOutput.Highlights != null)
                                {
                                    highlightingText = string.Join(" ", highlightedSMOutput.Highlights.Select(c => c));
                                    if (highlightingText.Length > 255)
                                    {
                                        IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                        highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                    }
                                    mediaResult.HighlightingText = highlightingText;
                                }
                            }

                            break;
                        case "TW":
                            mediaResult.MediaDate = hit.MediaDate;
                            mediaResult.Title = String.Empty;
                            mediaResult.Outlet = hit.Title;
                            if (!String.IsNullOrEmpty(hit.Url) && !String.IsNullOrEmpty(hit.ArticleID))
                            {
                                mediaResult.Url = String.Format("{0}/status/{1}", hit.Url, hit.ArticleID);
                            }
                            mediaResult.TwitterFollowers = hit.Audience;
                            mediaResult.TwitterFriends = hit.ActorFriendsCount;
                            mediaResult.KloutScore = Convert.ToInt64(hit.MediaValue);

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                HighlightedTWOutput highlightedTWOutput = new HighlightedTWOutput();
                                highlightedTWOutput = (HighlightedTWOutput)CommonFunctions.DeserialiazeXml(hit.HighlightingText, highlightedTWOutput);

                                if (highlightedTWOutput.Highlights != null)
                                {
                                    mediaResult.HighlightingText = highlightedTWOutput.Highlights.Replace("\r\n", " ");
                                }
                            }
                            break;
                        case "TM":
                            mediaResult.MediaDate = hit.LocalDate;
                            mediaResult.Outlet = hit.StationID;
                            mediaResult.Market = hit.Market;
                            mediaResult.Url = String.Format(ConfigurationManager.AppSettings["RadioRawPlayerURL"], HttpUtility.UrlEncode(CommonFunctions.GenerateRandomString() + IQCommon.CommonFunctions.EncryptStringAES(hit.ID.ToString(), IQCommon.CommonFunctions.AesKeyFeedsRadioPlayer, IQCommon.CommonFunctions.AesIVFeedsRadioPlayer)));

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                highlightingText = hit.HighlightingText.Replace("&lt;", "<").Replace("&gt;", ">");
                                if (highlightingText.Length > 255)
                                {
                                    IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                    highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                }
                                mediaResult.HighlightingText = highlightingText;
                            }
                            break;
                        case "PM":
                            mediaResult.MediaDate = hit.LocalDate;
                            mediaResult.Outlet = hit.Publication;
                            mediaResult.Url = !String.IsNullOrEmpty(hit.FileLocation) ? ConfigurationManager.AppSettings["PMBaseUrl"] + hit.FileLocation.Replace(@"\", @"/") : String.Empty;
                            mediaResult.Audience = hit.Audience;

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                XDocument xDocHL = XDocument.Parse(hit.HighlightingText);
                                highlightingText = string.Join(" ", xDocHL.Descendants("text").Select(c => c.Value)).Replace("&lt;", "<").Replace("&gt;", ">");
                                if (highlightingText.Length > 255)
                                {
                                    IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                    highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                }
                                mediaResult.HighlightingText = highlightingText;
                            }
                            break;
                        case "PQ":
                            mediaResult.MediaDate = hit.LocalDate;
                            mediaResult.Outlet = hit.Publication;
                            mediaResult.Url = String.Format(ConfigurationManager.AppSettings["ProQuestURL"], "feeds", hit.ID);

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                HighlightedPQOutput highlightedPQOutput = new HighlightedPQOutput();
                                highlightedPQOutput = (HighlightedPQOutput)CommonFunctions.DeserialiazeXml(hit.HighlightingText, highlightedPQOutput);

                                if (highlightedPQOutput.Highlights != null)
                                {
                                    highlightingText = string.Join(" ", highlightedPQOutput.Highlights.Select(c => c)).Replace("&lt;", "<").Replace("&gt;", ">");
                                    if (highlightingText.Length > 255)
                                    {
                                        IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                        highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                    }
                                    mediaResult.HighlightingText = highlightingText;
                                }
                            }
                            break;
                        case "IQR":
                            mediaResult.Title = hit.Title;
                            mediaResult.TimeZone = hit.TimeZone;
                            mediaResult.MediaDate = hit.LocalDate;
                            mediaResult.Outlet = hit.StationID;
                            mediaResult.Market = hit.Market;

                            if (getTVUrl)
                            {
                                if (dictPlayerUrls != null)
                                {
                                    if (dictPlayerUrls.ContainsKey(hit.MediaID))
                                    {
                                        mediaResult.Url = dictPlayerUrls[hit.MediaID];
                                    }
                                    else
                                    {
                                        isMissingTVUrls = true;
                                        Logger.Warning(String.Format("{0}No Radio url xml node was found for media ID {1}.", logPrefix, hit.MediaID));
                                    }
                                }
                                else
                                {
                                    Logger.Warning(String.Format("{0}User chose to generate Radio urls but no xml was generated.", logPrefix));
                                }
                            }
                            else
                            {
                                mediaResult.Url = "N/A";
                            }

                            if (!String.IsNullOrEmpty(hit.HighlightingText))
                            {
                                HighlightedCCOutput highlightedCCOutput = new HighlightedCCOutput();
                                highlightedCCOutput = (HighlightedCCOutput)CommonFunctions.DeserialiazeXml(hit.HighlightingText, highlightedCCOutput);

                                if (highlightedCCOutput.CC != null)
                                {
                                    highlightingText = string.Join(" ", highlightedCCOutput.CC.Select(c => c.Text)).Replace("&lt;", "<").Replace("&gt;", ">");
                                    if (highlightingText.Length > 255)
                                    {
                                        IndexAfter255Char = highlightingText.Substring(255).IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                                        highlightingText = IndexAfter255Char == -1 ? highlightingText.Substring(0, highlightingText.Length) : highlightingText.Substring(0, 255 + IndexAfter255Char) + "...";
                                    }
                                    mediaResult.HighlightingText = highlightingText;
                                }
                            }
                            break;
                        default:
                            Logger.Error(String.Format("{0}Encountered unsupported IQ_MediaType.AgentModelType: {1}", logPrefix, masterMediaType.DataModelType));
                            break;
                    }

                    lstMediaResults.Add(mediaResult);
                }
                else
                {
                    Logger.Error(String.Format("{0}Record found with invalid SubMediaType. ID: {1}", logPrefix, hit.ID));
                }
            }

            if (isMissingTVUrls)
            {
                Logger.Debug(String.Format("{0}Player url xml: {1}", logPrefix, xDocPlayerUrls));
            }

            return lstMediaResults;
        }

        public List<Tuple<XDocument, List<Int64>>> GetMediaResults(List<string> mediaIDs, Guid customerGUID, int batchSize, string logPrefix = null)
        {
            ClientSettings clientSettings = GetClientSettings(customerGUID);
            List<IQ_MediaTypeModel> lstSubMediaTypes = IQCommon.CommonFunctions.GetMediaTypes(customerGUID).Where(w => w.TypeLevel == 2).ToList();
            System.Uri SolrSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.FE.ToString(), null, null));
            FeedsSearch.SearchEngine searchEngine = new FeedsSearch.SearchEngine(SolrSearchRequestUrl);

            List<Tuple<XDocument, List<Int64>>> lstXmlInputs = new List<Tuple<XDocument, List<Int64>>>();

            for (int i = 0; i < mediaIDs.Count; i += batchSize)
            {
                FeedsSearch.SearchRequest searchRequest = new FeedsSearch.SearchRequest();
                searchRequest.IncludeDeleted = true;
                searchRequest.IsOnlyParents = false;
                searchRequest.PageSize = batchSize;

                if (i + batchSize > mediaIDs.Count)
                {
                    searchRequest.MediaIDs = mediaIDs.GetRange(i, mediaIDs.Count - i);
                }
                else
                {
                    searchRequest.MediaIDs = mediaIDs.GetRange(i, batchSize);
                }

                Logger.Debug(String.Format("{0}Fetch content for media items {1}-{2}.", logPrefix, i, i + searchRequest.MediaIDs.Count));

                Dictionary<string, FeedsSearch.SearchResult> dictResults = searchEngine.Search(searchRequest);
                if (dictResults.ContainsKey("Results"))
                {
                    List<FeedsSearch.Hit> hits = dictResults["Results"].Hits;
                    if (hits != null && hits.Count > 0)
                    {
                        Logger.Debug(String.Format("{0}Build SP input xml for media items {1}-{2}.", logPrefix, i, i + searchRequest.MediaIDs.Count));

                        XDocument xDoc = new XDocument(new XElement("MediaResults"));
                        List<Int64> parentIDs = new List<long>();
                        Dictionary<string, XElement> dictDataTypeNodes = new Dictionary<string, XElement>();

                        // Construct data type nodes
                        List<string> dataTypes = lstSubMediaTypes.Select(s => s.DataModelType).Distinct().ToList();
                        foreach (string dataType in dataTypes)
                        {
                            XElement element = new XElement(dataType);
                            xDoc.Root.Add(element);
                            dictDataTypeNodes.Add(dataType, element);
                        }

                        foreach (FeedsSearch.Hit mediaResult in hits)
                        {
                            parentIDs.Add(mediaResult.ParentID == 0 ? mediaResult.ID : mediaResult.ParentID);
                            IQ_MediaTypeModel subMediaTypeModel = lstSubMediaTypes.First(s => s.SubMediaType == mediaResult.MediaCategory);

                            XElement eleResult = new XElement("MediaResult",
                                                        new XElement("ID", mediaResult.ID),
                                                        new XElement("MediaID", mediaResult.MediaID),
                                                        new XElement("SearchRequestID", mediaResult.SearchRequestID),
                                                        new XElement("SearchTerm", mediaResult.SearchTerm),
                                                        new XElement("NumberOfHits", mediaResult.NumberOfHits),
                                                        new XElement("Title", mediaResult.Title),
                                                        new XElement("HighlightingText", mediaResult.HighlightingText),
                                                        new XElement("PositiveSentiment", mediaResult.PositiveSentiment),
                                                        new XElement("NegativeSentiment", mediaResult.NegativeSentiment),
                                                        new XElement("MediaType", mediaResult.MediaType),
                                                        new XElement("SubMediaType", mediaResult.MediaCategory),
                                                        new XElement("v5MediaType", mediaResult.v5MediaType),
                                                        new XElement("v5SubMediaType", mediaResult.v5MediaCategory),
                                                        new XElement("DataModelType", subMediaTypeModel.DataModelType));

                            switch (subMediaTypeModel.DataModelType)
                            {
                                case "TV":
                                    eleResult.Add(new XElement("IQ_CC_Key", mediaResult.IQ_CC_Key),
                                                    new XElement("StationID", mediaResult.StationID),
                                                    new XElement("VideoGuid", mediaResult.VideoGUID),
                                                    String.IsNullOrEmpty(mediaResult.HighlightingText) ? null : XDocument.Parse(mediaResult.HighlightingText).Root);
                                    break;
                                case "NM":
                                    decimal nmMediaValue = clientSettings.UseProminenceMediaValue ? (mediaResult.MediaValue * mediaResult.IQProminenceMultiplier) : mediaResult.MediaValue;

                                    eleResult.Add(new XElement("MediaDate", mediaResult.MediaDate),
                                                    new XElement("ArticleID", mediaResult.ArticleID),
                                                    new XElement("Url", mediaResult.Url + (mediaResult.IQLicense == 3 ? "&source=library" : "")), // LexisNexis articles need to know if they were opened from Library
                                                    new XElement("Publication", mediaResult.Publication),
                                                    new XElement("CompeteUrl", mediaResult.Outlet),
                                                    new XElement("Audience", mediaResult.Audience),
                                                    new XElement("MediaValue", nmMediaValue),
                                                    new XElement("AudienceType", mediaResult.AudienceType),
                                                    new XElement("IQLicense", mediaResult.IQLicense));
                                    break;
                                case "SM":
                                    // If highlighting text isn't used, the full content is stored in the HighlightingText field. 
                                    // Since Library tables store content and highlighting text separately, the content needs to be passed in as a separate field.
                                    string content = null;
                                    if (!subMediaTypeModel.UseHighlightingText && !String.IsNullOrWhiteSpace(mediaResult.HighlightingText))
                                    {
                                        HighlightedSMOutput highlightedSMOutput = new HighlightedSMOutput();
                                        highlightedSMOutput = (HighlightedSMOutput)CommonFunctions.DeserialiazeXml(mediaResult.HighlightingText, highlightedSMOutput);

                                        if (highlightedSMOutput.Highlights != null)
                                        {
                                            content = highlightedSMOutput.Highlights[0];
                                        }
                                    }

                                    decimal smMediaValue = clientSettings.UseProminenceMediaValue ? (mediaResult.MediaValue * mediaResult.IQProminenceMultiplier) : mediaResult.MediaValue;

                                    eleResult.Add(new XElement("MediaDate", mediaResult.MediaDate),
                                                    new XElement("ArticleID", mediaResult.ArticleID),
                                                    new XElement("Url", mediaResult.Url),
                                                    new XElement("Publication", mediaResult.Publication),
                                                    new XElement("CompeteUrl", mediaResult.Outlet),
                                                    new XElement("Audience", mediaResult.Audience),
                                                    new XElement("MediaValue", smMediaValue),
                                                    new XElement("AudienceType", mediaResult.AudienceType),
                                                    new XElement("ThumbUrl", mediaResult.ThumbnailUrl),
                                                    String.IsNullOrEmpty(mediaResult.ArticleStats) ? null : XDocument.Parse(mediaResult.ArticleStats).Root,
                                                    subMediaTypeModel.UseHighlightingText ? null : new XElement("Content", content));
                                    break;
                                case "TW":
                                    eleResult.Add(new XElement("ArticleID", mediaResult.ArticleID),
                                                    new XElement("ActorPreferredName", mediaResult.ActorPreferredName),
                                                    new XElement("Summary", mediaResult.Content),
                                                    new XElement("Audience", mediaResult.Audience),
                                                    new XElement("ActorFriendsCount", mediaResult.ActorFriendsCount),
                                                    new XElement("ActorImage", mediaResult.ThumbnailUrl),
                                                    new XElement("Url", mediaResult.Url),
                                                    new XElement("MediaValue", Int32.Parse(mediaResult.MediaValue.ToString(), NumberStyles.Any)),
                                                    new XElement("MediaDate", mediaResult.MediaDate));
                                    break;
                                case "TM":
                                    eleResult.Add(new XElement("StationID", mediaResult.StationID),
                                                    new XElement("Market", mediaResult.Market),
                                                    new XElement("DmaID", mediaResult.DmaID),
                                                    new XElement("StationIDNum", mediaResult.StationIDNum),
                                                    new XElement("Duration", mediaResult.Duration),
                                                    new XElement("MediaDate", mediaResult.MediaDate),
                                                    new XElement("LocalDate", mediaResult.LocalDate),
                                                    new XElement("TimeZone", mediaResult.TimeZone));
                                    break;
                                case "PQ":
                                    XElement eleAuthors = null;
                                    if (mediaResult.Authors != null && mediaResult.Authors.Count > 0)
                                    {
                                        eleAuthors = new XElement("authors",
                                                        from ele in mediaResult.Authors
                                                        select new XElement("author", ele));
                                    }

                                    eleResult.Add(new XElement("ArticleID", mediaResult.ArticleID),
                                                    new XElement("Publication", mediaResult.Publication),
                                                    new XElement("MediaCategory", mediaResult.MediaCategory),
                                                    new XElement("ContentHTML", mediaResult.Content),
                                                    new XElement("AvailableDate", mediaResult.AvailableDate),
                                                    new XElement("MediaDate", mediaResult.MediaDate),
                                                    new XElement("LanguageNum", mediaResult.LanguageNum),
                                                    new XElement("Copyright", mediaResult.Copyright),
                                                    eleAuthors);
                                    break;
                                case "IQR":
                                    eleResult.Add(new XElement("Guid", mediaResult.VideoGUID), 
                                                    new XElement("StationID", mediaResult.StationID),
                                                    String.IsNullOrEmpty(mediaResult.HighlightingText) ? null : XDocument.Parse(mediaResult.HighlightingText).Root);
                                    break;
                            }

                            dictDataTypeNodes[subMediaTypeModel.DataModelType].Add(eleResult);
                        }

                        lstXmlInputs.Add(new Tuple<XDocument, List<Int64>>(xDoc, parentIDs));
                    }
                }
            }

            return lstXmlInputs;
        }

        public string ExportCSV(bool isSelectAll, string searchCriteria, string articleIDXml, string sortType, int batchSize, bool getTVUrl, ClientSettings clientSettings, List<IQ_MediaTypeModel> lstMasterMediaTypes, XDocument xDocTVUrls, List<FeedsSearch.Hit> lstHits, string logPrefix = null)
        {
            string DQ = "\"";
            StringBuilder sb = new StringBuilder();

            // If TV urls were generated, the hits were already retrieved
            if (lstHits == null)
            {
                if (isSelectAll)
                {
                    if (!String.IsNullOrEmpty(searchCriteria))
                    {
                        lstHits = GetExportMediaResultsBySearchCriteria(searchCriteria, sortType, clientSettings, lstMasterMediaTypes, batchSize, logPrefix);
                    }
                    else
                    {
                        Logger.Error(String.Format("{0}IsSelectAll is true, but SearchCriteria is empty", logPrefix));
                    }
                }
                else
                {
                    if (!String.IsNullOrEmpty(articleIDXml))
                    {
                        XDocument xDoc = XDocument.Parse(articleIDXml);
                        List<string> mediaIDs = xDoc.Descendants("ID").Select(s => s.Value).ToList();

                        lstHits = GetExportMediaResultsByID(mediaIDs, batchSize, logPrefix);
                    }
                    else
                    {
                        Logger.Error(String.Format("{0}IsSelectAll is false, but ArticleIDXml is empty", logPrefix));
                    }
                }
            }

            List<FeedsMediaResult> mediaResults = FillMediaResults(lstHits, sortType, clientSettings, lstMasterMediaTypes, getTVUrl, xDocTVUrls, logPrefix);

            bool useArticleStatsColumns = mediaResults.Where(w => w.ArticleStats != null).ToList().Count > 0;

            // Build media item header
            sb.Append("Media Date Time,Time Zone,Agent,Source,Title,Outlet,DMA,URL" + (clientSettings.IsNielsenData || clientSettings.IsCompeteData ? ",Audience,Audience Source,Media Value ($)" : string.Empty) + (clientSettings.IsNielsenData ? ",National Audience,National Media Value ($)" : string.Empty) + ",Twitter Followers,Twitter Following,Twitter Klout Score," + (useArticleStatsColumns ? "Likes,Comments,Shares," : string.Empty) + "Positive Sentiment,Negative Sentiment,Number of Hits,Text");
            sb.Append(Environment.NewLine);

            foreach (FeedsMediaResult mediaResult in mediaResults)
            {
                // Media Date
                sb.Append(mediaResult.MediaDate.ToString());
                sb.Append(",");

                // Time Zone
                sb.Append(mediaResult.TimeZone);
                sb.Append(",");

                // Agent
                sb.Append(DQ + mediaResult.AgentName.Replace("\"", "\"\"") + DQ);
                sb.Append(",");

                // Source
                sb.Append(mediaResult.Source);
                sb.Append(",");

                //Title
                if (!String.IsNullOrEmpty(mediaResult.Title))
                {
                    if (mediaResult.Title.StartsWith("-"))
                    {
                        mediaResult.Title = " " + mediaResult.Title;
                    }
                    sb.Append(DQ + mediaResult.Title.Replace("\"", "\"\"") + DQ);
                }
                sb.Append(",");

                // Outlet
                sb.Append(DQ + mediaResult.Outlet + DQ);
                sb.Append(",");

                // Market
                sb.Append(DQ + mediaResult.Market + DQ);
                sb.Append(",");

                // Url
                if (!String.IsNullOrEmpty(mediaResult.Url))
                {
                    if (mediaResult.Url == "N/A")
                        sb.Append(mediaResult.Url);
                    else
                        sb.Append("=HYPERLINK(" + DQ + mediaResult.Url.Replace(",", "%2c") + DQ + ")");
                }
                sb.Append(",");

                if (clientSettings.IsNielsenData || clientSettings.IsCompeteData)
                {
                    // Audience
                    sb.Append(mediaResult.Audience.HasValue ? mediaResult.Audience.Value.ToString() : String.Empty);
                    sb.Append(",");

                    // Audience Source
                    sb.Append(mediaResult.AudienceSource);
                    sb.Append(",");

                    // Media Value
                    sb.Append(mediaResult.MediaValue.HasValue ? mediaResult.MediaValue.Value.ToString() : String.Empty);
                    sb.Append(",");
                }

                if (clientSettings.IsNielsenData)
                {
                    // National Audience
                    sb.Append(mediaResult.NationalAudience.HasValue ? mediaResult.NationalAudience.Value.ToString() : String.Empty);
                    sb.Append(",");

                    // National Media Value
                    sb.Append(mediaResult.NationalMediaValue.HasValue ? mediaResult.NationalMediaValue.Value.ToString() : String.Empty);
                    sb.Append(",");
                }

                // Twitter Followers
                sb.Append(mediaResult.TwitterFollowers.HasValue ? mediaResult.TwitterFollowers.Value.ToString() : String.Empty);
                sb.Append(",");

                // Twitter Friends
                sb.Append(mediaResult.TwitterFriends.HasValue ? mediaResult.TwitterFriends.Value.ToString() : String.Empty);
                sb.Append(",");

                // Klout Score
                sb.Append(mediaResult.KloutScore.HasValue ? mediaResult.KloutScore.Value.ToString() : String.Empty);
                sb.Append(",");

                if (useArticleStatsColumns)
                {
                    if (mediaResult.ArticleStats != null)
                    {
                        // Likes
                        sb.Append(mediaResult.ArticleStats.Likes);
                        sb.Append(",");

                        // Comments
                        sb.Append(mediaResult.ArticleStats.Comments);
                        sb.Append(",");

                        // Shares
                        sb.Append(mediaResult.ArticleStats.Shares);
                        sb.Append(",");
                    }
                    else
                    {
                        sb.Append(",,,"); 
                    }                    
                }

                // Positive Sentiment
                sb.Append(mediaResult.PositiveSentiment);
                sb.Append(",");

                // Negative Sentiment
                sb.Append(mediaResult.NegativeSentiment);
                sb.Append(",");

                // Number of Hits
                sb.Append(mediaResult.NumHits);
                sb.Append(",");

                // Highlighting Text
                if (!String.IsNullOrEmpty(mediaResult.HighlightingText))
                {
                    if (mediaResult.HighlightingText.StartsWith("-"))
                    {
                        mediaResult.HighlightingText = " " + mediaResult.HighlightingText;
                    }
                    sb.Append(DQ + mediaResult.HighlightingText.Replace("\"", "\"\"") + DQ);
                }

                sb.Append(Environment.NewLine);
            }

            return sb.ToString();
        }

        public int UpdateFeedsExportDownloadPath(Int64 p_ID, string p_DownloadPath)
        {
            return Context.UpdateFeedsExpDownloadPath(p_ID, p_DownloadPath);
        }

        public XDocument BuildTVUrlXml(bool isSelectAll, string searchCriteria, string articleIDXml, string sortType, int batchSize, ClientSettings clientSettings, List<IQ_MediaTypeModel> lstMasterMediaTypes, string logPrefix, out List<FeedsSearch.Hit> lstHits)
        {
            lstHits = new List<FeedsSearch.Hit>();
            List<string> subMediaTypes = lstMasterMediaTypes.Where(f => !String.IsNullOrEmpty(f.ExternalPlayerUrlSvc)).Select(s => s.SubMediaType).ToList();
            if (isSelectAll)
            {
                if (!String.IsNullOrEmpty(searchCriteria))
                {
                    lstHits = GetExportMediaResultsBySearchCriteria(searchCriteria, sortType, clientSettings, lstMasterMediaTypes, batchSize, logPrefix);
                }
                else
                {
                    Logger.Error(String.Format("{0}IsSelectAll is true, but SearchCriteria is empty", logPrefix));
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(articleIDXml))
                {
                    XDocument xDoc = XDocument.Parse(articleIDXml);
                    List<string> mediaIDs = xDoc.Descendants("ID").Select(s => s.Value).ToList();

                    lstHits = GetExportMediaResultsByID(mediaIDs, batchSize, logPrefix);
                }
                else
                {
                    Logger.Error(String.Format("{0}IsSelectAll is false, but ArticleIDXml is empty", logPrefix));
                }
            }
            
            XDocument xDocUrls = new XDocument(new XElement("TVUrls",
                                            from hit in lstHits.Where(w => subMediaTypes.Contains(w.v5MediaCategory))
                                            select new XElement("TVUrl", 
                                                    new XElement("SubMediaType", hit.v5MediaCategory),
                                                    new XElement("MediaID", hit.MediaID),
                                                    new XElement("Processed", 0),
                                                    new XElement("Url", "")
                                                   )
                                                ));
            return xDocUrls;
        }

        public void MarkAsRead(List<Int64> parentIDs, Guid clientGuid, string logPrefix)
        {
            System.Uri SolrSearchRequestUrl = new Uri(SolrEngineLogic.GeneratePMGUrl(SolrEngineLogic.PMGUrlType.FE.ToString(), null, null));
            FeedsSearch.SearchEngine searchEngine = new FeedsSearch.SearchEngine(SolrSearchRequestUrl);

            FeedsSearch.SearchRequest searchRequest = new FeedsSearch.SearchRequest();
            searchRequest.MediaIDs = parentIDs.Select(s => s.ToString()).ToList();
            searchRequest.SearchOnParentID = true;
            searchRequest.IsOnlyParents = false;
            searchRequest.PageSize = 99999; // Don't know how many records will be returned

            Dictionary<string, FeedsSearch.SearchResult> dictResults = searchEngine.Search(searchRequest);
            if (dictResults.ContainsKey("Results"))
            {
                List<FeedsSearch.Hit> hits = dictResults["Results"].Hits;
                if (hits != null && hits.Count > 0)
                {
                    List<string> mediaIDs = hits.Select(s => s.ID.ToString()).ToList();
                    Logger.Info(String.Format("{0}Marking {1} record(s) as read.", logPrefix, mediaIDs.Count));

                    int retVal = IQCommon.CommonFunctions.UpdateIsRead(clientGuid, mediaIDs, true);

                    if (retVal != 1)
                    {
                        Logger.Error(String.Format("{0}Error occurred while marking records as read.", logPrefix));
                    }
                }
            }
        }

        private void CheckForQueuedSolrUpdates(FeedsSearch.SearchRequest searchRequest, Guid clientGuid, bool? isRead)
        {
            Dictionary<string, List<string>> dictExclude = IQCommon.CommonFunctions.GetQueuedDeleteMediaResults(clientGuid);
            if (dictExclude.ContainsKey("ExcludeIDs"))
            {
                searchRequest.ExcludeIDs = dictExclude["ExcludeIDs"];
            }
            if (dictExclude.ContainsKey("ExcludeSearchRequestIDs"))
            {
                searchRequest.ExcludeSearchRequestIDs = dictExclude["ExcludeSearchRequestIDs"];
            }

            int maxDeleted = Convert.ToInt32(ConfigurationManager.AppSettings["MaxExcludedSeqIDs"]);
            if (searchRequest.ExcludeIDs.Count > maxDeleted)
            {
                // Do not include "excluded" or soon to be deleted items in query, there is no reason to save and pass this list
                searchRequest.ExcludeIDs = new List<string>();
            }

            Dictionary<string, bool> dictIsRead = IQCommon.CommonFunctions.GetQueuedIsRead(clientGuid);
            List<string> readIDs = dictIsRead.Where(s => s.Value).Select(s => s.Key).ToList();
            List<string> unreadIDs = dictIsRead.Where(s => !s.Value).Select(s => s.Key).ToList();
            bool isReadLimitExceeded = readIDs.Count > 1000 || unreadIDs.Count > 1000;

            // Solr has trouble handling more than 1000 queued records at once, so if the limit is exceeded ignore them
            if (readIDs.Count < 1000 && unreadIDs.Count < 1000)
            {
                // Set faceting lists
                if (readIDs != null && readIDs.Count > 0)
                {
                    searchRequest.QueuedAsReadIDs = readIDs;
                }
                if (unreadIDs != null && unreadIDs.Count > 0)
                {
                    searchRequest.QueuedAsUnreadIDs = unreadIDs;
                }

                if (isRead.HasValue)
                {
                    List<string> includeIDs = dictIsRead.Where(s => s.Value == isRead.Value).Select(s => s.Key).ToList();
                    List<string> excludeIDs = dictIsRead.Where(s => s.Value != isRead.Value).Select(s => s.Key).ToList();

                    // Set filtering lists
                    if (includeIDs != null && includeIDs.Count > 0)
                    {
                        // Manually include any records that match the criteria, but haven't been updated in solr. Remove records that are queued for deletion.
                        if (searchRequest.ExcludeIDs != null)
                        {
                            searchRequest.IsReadIncludeIDs = includeIDs.Except(searchRequest.ExcludeIDs).ToList();
                        }
                        else
                        {
                            searchRequest.IsReadIncludeIDs = includeIDs;
                        }
                    }
                    if (excludeIDs != null && excludeIDs.Count > 0)
                    {
                        // Manually exclude any records that don't match the criteria, but haven't been updated in solr.
                        if (searchRequest.ExcludeIDs != null)
                        {
                            searchRequest.ExcludeIDs = searchRequest.ExcludeIDs.Union(excludeIDs).ToList();
                            if (searchRequest.ExcludeIDs.Count > maxDeleted)
                            {
                                // If read excluded and to be deleted excludes are together more than the number allowed in the query only include read excludes
                                searchRequest.ExcludeIDs = excludeIDs;
                            }
                        }
                        else
                        {
                            searchRequest.ExcludeIDs = excludeIDs;
                        }
                    }
                }
            }
        }

        private FeedsSearch.SearchRequest BuildFeedsSearchRequest(FeedsSearchCriteria objSearchCriteria, string sortType, Guid clientGuid, List<IQ_MediaTypeModel> lstMasterMediaTypes, string logPrefix)
        {
            FeedsSearch.SearchRequest searchRequest = new FeedsSearch.SearchRequest();
            searchRequest.ClientGUID = clientGuid;
            searchRequest.Keyword = objSearchCriteria.Keyword;
            searchRequest.SearchRequestIDs = objSearchCriteria.SearchRequestIDs;
            searchRequest.MediaCategories = objSearchCriteria.SubMediaTypes;
            searchRequest.IQProminence = objSearchCriteria.ProminenceValue;
            searchRequest.IsProminenceAudience = objSearchCriteria.IsProminenceAudience;
            searchRequest.SentimentFlag = objSearchCriteria.Sentiment;
            searchRequest.Dma = objSearchCriteria.Dma;
            searchRequest.DmaIDs = objSearchCriteria.DmaIDs;
            searchRequest.Station = objSearchCriteria.Station;
            searchRequest.Outlet = objSearchCriteria.CompeteUrl;
            searchRequest.TwitterHandle = objSearchCriteria.TwitterHandle;
            searchRequest.Publication = objSearchCriteria.Publication;
            searchRequest.Author = objSearchCriteria.Author;
            searchRequest.FromDate = objSearchCriteria.FromDate;
            searchRequest.ToDate = objSearchCriteria.ToDate;
            searchRequest.IsOnlyParents = objSearchCriteria.IsOnlyParents;
            searchRequest.IsRead = objSearchCriteria.IsRead;
            searchRequest.usePESHFilters = objSearchCriteria.IsHeard || objSearchCriteria.IsSeen || objSearchCriteria.IsPaid || objSearchCriteria.IsEarned;
            searchRequest.IsHeardFilter = objSearchCriteria.IsHeard;
            searchRequest.isSeenFilter = objSearchCriteria.IsSeen;
            searchRequest.isPaidFilter = objSearchCriteria.IsPaid;
            searchRequest.isEarnedFilter = objSearchCriteria.IsEarned;
            searchRequest.ShowTitle = objSearchCriteria.ShowTitle;
            searchRequest.TimeOfDay = objSearchCriteria.TimesOfDay;
            searchRequest.DayOfWeek = objSearchCriteria.DaysOfWeek;
            searchRequest.SinceID = objSearchCriteria.SinceID;
            searchRequest.useGMT = objSearchCriteria.useGMT;

            CheckForQueuedSolrUpdates(searchRequest, clientGuid, objSearchCriteria.IsRead);

            if ((objSearchCriteria.SubMediaTypes == null || objSearchCriteria.SubMediaTypes.Count == 0) && lstMasterMediaTypes != null && lstMasterMediaTypes.Count > 0)
            {
                searchRequest.ExcludeMediaCategories = lstMasterMediaTypes.Where(w => w.TypeLevel == 2 && !w.HasAccess).Select(s => s.SubMediaType).ToList();
            }

            switch (sortType)
            {
                case "ArticleWeight-":
                    searchRequest.SortType = FeedsSearch.SortType.ARTICLE_WEIGHT;
                    break;
                case "OutletWeight-":
                    searchRequest.SortType = FeedsSearch.SortType.OUTLET_WEIGHT;
                    break;
                case "Date-":
                    searchRequest.SortType = FeedsSearch.SortType.DATE;
                    searchRequest.IsSortAsc = false;
                    break;
                case "Date+":
                    searchRequest.SortType = FeedsSearch.SortType.DATE;
                    searchRequest.IsSortAsc = true;
                    break;
                case null:
                    break;
                default:
                    Logger.Error(String.Format("{0}Encountered unsupported sort type: {1}", logPrefix, sortType));
                    break;
            }

            return searchRequest;
        }

        public string DoHttpGetRequest(string p_URL)
        {
            try
            {
                Uri _Uri = new Uri(p_URL);
                HttpWebRequest _objWebRequest = (HttpWebRequest)WebRequest.Create(_Uri);

                _objWebRequest.Timeout = 100000000;
                _objWebRequest.Method = "GET";

                StreamReader _StreamReader = null;

                string _ResponseRawMedia = string.Empty;

                if ((_objWebRequest.GetResponse().ContentLength > 0))
                {
                    _StreamReader = new StreamReader(_objWebRequest.GetResponse().GetResponseStream());
                    _ResponseRawMedia = _StreamReader.ReadToEnd();
                    _StreamReader.Dispose();
                }

                return _ResponseRawMedia;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public void DoAsyncHttpGetRequest(string p_URL)
        {

            try
            {
                Uri _Uri = new Uri(p_URL);
                _objWebRequestAsync = (HttpWebRequest)WebRequest.Create(_Uri);

                _objWebRequestAsync.Timeout = 100000000;
                _objWebRequestAsync.Method = "GET";

                StreamReader _StreamReader = null;

                string _ResponseRawMedia = string.Empty;

                if ((_objWebRequestAsync.GetResponse().ContentLength > 0))
                {
                    _objWebRequestAsync.BeginGetResponse(new AsyncCallback(FinishWebRequest), null);

                    _StreamReader = new StreamReader(_objWebRequestAsync.GetResponse().GetResponseStream());
                    _ResponseRawMedia = _StreamReader.ReadToEnd();
                    _StreamReader.Dispose();
                }

            }
            catch (Exception)
            {

                throw;
            }
        }

        void FinishWebRequest(IAsyncResult result)
        {
            _objWebRequestAsync.EndGetResponse(result);
        }
    }
}
