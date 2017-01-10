using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using IQMedia.Service.Common.Util;
using IQMedia.Service.DiscoveryArchiveMetaDataUpdate.Config;
using IQMedia.Service.Logic;

namespace IQMedia.Service.DiscoveryArchiveMetaDataUpdate
{
    public partial class DiscoveryArchiveMetaDataUpdate : ServiceBase
    {
        private Thread _workerThread;
        private ServiceHost _host;
        private Queue<DiscoveryArchiveMetaDataUpdateTask> _queueOfMetaDataUpdateTask;
        private static readonly string _connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
        private static List<double> _pollIntervals;

        public DiscoveryArchiveMetaDataUpdate()
        {
            InitializeComponent();
            _workerThread = new Thread(Run);
            _workerThread.IsBackground = true;
        }

        protected override void OnStart(string[] args)
        {
            _workerThread.Start();
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            Quit();
            base.OnStop();
        }


        /// <summary>
        /// Initializes the service and verifies config data.
        /// </summary>
        protected static void InitializeService()
        {
            LogMessage("INFO", "Initializing settings and parameters");

            //Re-fetch the config settings...
            ConfigurationManager.RefreshSection("DiscoveryArchiveMetaDataUpdateSettings");
            //ConfigurationManager.RefreshSection("FFmpegSettings");
            //Test our config file to make sure we have everything we need...
            if (ConfigSettings.Settings == null)
                throw new XmlException("App.config is missing <DiscoveryArchiveMetaDataUpdateSettings> node.");

            SolrEngineLogic.SolrRequestor = SolrEngineLogic.SolrReqestorType.DiscoveryToLibrary;
            _pollIntervals = ConfigSettings.Settings.PollIntervals.Split(',').Select(s => Convert.ToDouble(s)).ToList();
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        public void Run()
        {
            try
            {
                LogMessage("INFO", "DiscoveryArchiveMetaDataUpdate Service started at: " + DateTime.Now);
                //var baseAddress = new Uri("http://localhost:" + ConfigSettings.Settings.WCFServicePort + "/DiscoveryMetaDataUpdateWebService");

                //_host = new ServiceHost(typeof(Service.DiscoveryMetaDataUpdateWebService), baseAddress);
                //// Enable metadata publishing.
                //var smb = new ServiceMetadataBehavior();
                //smb.HttpGetEnabled = true;
                //smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
                //_host.Description.Behaviors.Add(smb);

                //// Open the ServiceHost to start listening for messages. Since
                //// no endpoints are explicitly configured, the runtime will create
                //// one endpoint per base address for each service contract implemented
                //// by the service.
                //_host.Open();

                StartTasks();
            }
            catch (AddressAccessDeniedException ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                LogMessage("FATAL", "You must start this service with administrative rights.", ex);
                Stop();
            }
            catch (AddressAlreadyInUseException ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                LogMessage("FATAL", "The WCF Service Port you have specified for this service is already in use. Please specify another.", ex);
                Stop();
            }
            catch (Exception ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                LogMessage("FATAL", String.Empty, ex);
                Stop();
            }
            finally
            {
                if (_host != null && _host.State == CommunicationState.Faulted)
                    _host.Abort();
                else if (_host != null)
                    _host.Close();
            }
        }

        private void StartTasks()
        {
            while (true)
            {
                InitializeService();

                GetRecordsFromDB();

                while (_queueOfMetaDataUpdateTask.Count > 0)
                {
                    var totalNoOfTsk = _queueOfMetaDataUpdateTask.Count > Config.ConfigSettings.Settings.NoOfTasks ? Config.ConfigSettings.Settings.NoOfTasks : _queueOfMetaDataUpdateTask.Count;

                    List<Task> listOfTask = new List<Task>(totalNoOfTsk);

                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;

                    for (int index = 0; index < totalNoOfTsk; index++)
                    {
                        var tmpExportTask = _queueOfMetaDataUpdateTask.Dequeue();
                        LogMessage("INFO", "Assign " + tmpExportTask.ID + " to task " + index);
                        listOfTask.Add(Task.Factory.StartNew((object obj) => ProcessDBRecords(tmpExportTask, token), tmpExportTask, token));
                    }

                    try
                    {
                        Task.WaitAll(listOfTask.ToArray(), Convert.ToInt32(Config.ConfigSettings.Settings.MaxTimeOut), token);
                        tokenSource.Cancel();
                    }
                    catch (AggregateException ex)
                    {
                        foreach (var item in ex.InnerExceptions)
                        {
                            CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                            LogMessage("ERROR", "AggregateException", item);
                        }

                        CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                        LogMessage("ERROR", "AggregateException ", ex);
                    }
                    catch (Exception ex)
                    {
                        CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                        LogMessage("ERROR", "Exception in waitall ", ex);
                    }

                    LogMessage("INFO", "Waitall complete");

                    foreach (var tsk in listOfTask)
                    {
                        DiscoveryArchiveMetaDataUpdateTask metadataUpdateTask = (DiscoveryArchiveMetaDataUpdateTask)tsk.AsyncState;

                        LogMessage("DEBUG", "Tsk Status - " + tsk.Status, metadataUpdateTask.ID);
                        LogMessage("DEBUG", "DiscoveryArchiveMetaDataUpdateTsk Status - " + metadataUpdateTask.Status, metadataUpdateTask.ID);
                    }

                    listOfTask.Clear();
                }

                double currentMinutes = DateTime.Now.Minute + (DateTime.Now.Second / 60D);
                double pollInterval = _pollIntervals.Where(s => s > currentMinutes).DefaultIfEmpty(_pollIntervals.Min() + 60).Min();
                int sleepTime = Convert.ToInt32((pollInterval - currentMinutes) * 60);

                LogMessage("INFO", "Discovery Archive MetaData Update Service Enqueuer sleeping until " + DateTime.Now.AddSeconds(sleepTime).ToString("hh:mm:ss tt"));
                Thread.Sleep(Convert.ToInt32(TimeSpan.FromSeconds(sleepTime).TotalMilliseconds));
            }
        }

        private void GetRecordsFromDB()
        {
            try
            {
                _queueOfMetaDataUpdateTask = new Queue<DiscoveryArchiveMetaDataUpdateTask>();

                using (var conn = new SqlConnection(_connStr))
                {
                    LogMessage("DEBUG", "Fetching queued items from database.");
                    conn.Open();

                    var cmdStr = "usp_metadataupd_IQService_DiscoveryArchiveMetaDataUpdate_SelectQueued";

                    using (var cmd = conn.GetCommand(cmdStr, CommandType.StoredProcedure))
                    {
                        cmd.Parameters.Add("@TopRows", SqlDbType.Int).Value = Config.ConfigSettings.Settings.QueueLimit;
                        cmd.Parameters.Add("@MachineName", SqlDbType.VarChar).Value = System.Environment.MachineName;

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _queueOfMetaDataUpdateTask.Enqueue(new DiscoveryArchiveMetaDataUpdateTask
                            {
                                ID = reader.GetInt64(0),
                                ArchiveTracking = reader.GetString(1)
                            });
                        }
                        LogMessage("INFO", _queueOfMetaDataUpdateTask.Count + " new items enqueued.");
                    }
                }
            }
            catch (Exception ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex);
                LogMessage("ERROR", "An error occurred while attempting to retrieve new tasks from the database.", ex);
            }
        }

        private void ProcessDBRecords(DiscoveryArchiveMetaDataUpdateTask p_ArchiveMetaDataUpdateTask, CancellationToken p_CToken)
        {
            try
            {
                string logPrefix = Environment.MachineName + " - Task " + p_ArchiveMetaDataUpdateTask.ID + " - ";

                LogMessage("INFO", "Task is processing.", p_ArchiveMetaDataUpdateTask.ID);

                p_ArchiveMetaDataUpdateTask.Status = DiscoveryArchiveMetaDataUpdateTask.TskStatus.METADATA_IN_PROCESS;
                UpdateTaskStatus(DiscoveryArchiveMetaDataUpdateTask.TskStatus.METADATA_IN_PROCESS.ToString(), p_ArchiveMetaDataUpdateTask.ID);

                FeedReportLogic feedReportLogic = (FeedReportLogic)LogicFactory.GetLogic(LogicType.FeedsReport);
                XDocument xDoc = XDocument.Parse(p_ArchiveMetaDataUpdateTask.ArchiveTracking);

                LogMessage("INFO", "parse all media from archivetracking xml and update content task", p_ArchiveMetaDataUpdateTask.ID);
                LogMessage("INFO", "input archivetracking xml : " + p_ArchiveMetaDataUpdateTask.ArchiveTracking, p_ArchiveMetaDataUpdateTask.ID);

                List<string> lstTVThumbSuccessStatuses = ConfigurationManager.AppSettings["TVThumbSuccessStatuses"].Split(',').ToList();
                List<string> lstExportSuccessStatuses = ConfigurationManager.AppSettings["ExportSuccessStatuses"].Split(',').ToList();

                bool isCompleted = true;
                foreach (XElement xElem in xDoc.Descendants("SubMedia"))
                {
                    // If the task has timed out, save the progress made and requeue it
                    if (p_CToken.IsCancellationRequested)
                    {
                        isCompleted = false;
                        LogMessage("INFO", "Task is cancelled.", p_ArchiveMetaDataUpdateTask.ID);
                        p_ArchiveMetaDataUpdateTask.Status = DiscoveryArchiveMetaDataUpdateTask.TskStatus.TIMEOUT_METADATA;
                        UpdateArchiveTracking(p_ArchiveMetaDataUpdateTask.ID, xDoc.ToString(), p_ArchiveMetaDataUpdateTask.Status.ToString());
                        break;
                    }

                    if (xElem.Parent.Name == "NM" && xElem.Descendants("Content").FirstOrDefault().Value == "1")
                    {
                        string newsContent = feedReportLogic.GetNewsContent(xElem.Descendants("ArticleID").FirstOrDefault().Value, logPrefix);

                        if (string.IsNullOrWhiteSpace(newsContent))
                        {
                            newsContent = string.Empty;
                            xElem.Descendants("Content").FirstOrDefault().Value = "1";
                            LogMessage("INFO", "content is empty for news media _ArchiveMediaID:" + xElem.Descendants("ID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                        }
                        else
                        {
                            // Replace LexisNexis linebreak placeholder text with whitespace
                            newsContent = newsContent.Replace(ConfigurationManager.AppSettings["LexisNexisLineBreakPlaceholder"], " ");

                            xElem.Descendants("Content").FirstOrDefault().Value = "0";
                            LogMessage("INFO", "News media ArticleContent :" + (newsContent.Length > 300 ? newsContent.Substring(0, 300) + "...." : newsContent), p_ArchiveMetaDataUpdateTask.ID);
                            LogMessage("INFO", "update archive metadata content for News Media _ArchiveMediaID: " + xElem.Descendants("ID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            UpdateArchiveMediaRecord(xElem.Descendants("ID").FirstOrDefault().Value, "NM", newsContent);
                        }
                    }
                    else if (xElem.Parent.Name == "SM" && xElem.Descendants("Content").FirstOrDefault().Value == "1")
                    {
                        string smContent = feedReportLogic.GetSocialMediaContent(xElem.Descendants("ArticleID").FirstOrDefault().Value, logPrefix);

                        if (string.IsNullOrWhiteSpace(smContent))
                        {
                            smContent = string.Empty;
                            xElem.Descendants("Content").FirstOrDefault().Value = "1";
                            LogMessage("INFO", "content is empty for social media _ArchiveMediaID:" + xElem.Descendants("ID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                        }
                        else
                        {
                            xElem.Descendants("Content").FirstOrDefault().Value = "0";
                            LogMessage("INFO", "Social media ArticleContent :" + (smContent.Length > 300 ? smContent.Substring(0, 300) + "...." : smContent), p_ArchiveMetaDataUpdateTask.ID);
                            LogMessage("INFO", "update archive metadata content for social Media _ArchiveMediaID: " + xElem.Descendants("ID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            UpdateArchiveMediaRecord(xElem.Descendants("ID").FirstOrDefault().Value, "SM", smContent);
                        }
                    }
                    else if (xElem.Parent.Name == "TV")
                    {
                        if (xElem.Descendants("CC").FirstOrDefault().Value == "1")
                        {
                            LogMessage("INFO", "fetch content for TV media guid :  " + xElem.Descendants("ArticleID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            string tvContent = feedReportLogic.DoHttpGetRequest(ConfigurationManager.AppSettings["TVContentURL"] + xElem.Descendants("ArticleID").FirstOrDefault().Value);
                            if (string.IsNullOrWhiteSpace(tvContent))
                            {
                                tvContent = string.Empty;
                                xElem.Descendants("CC").FirstOrDefault().Value = "1";
                                LogMessage("INFO", "content is empty for TV media _ArchiveMediaID:" + xElem.Descendants("ID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            }
                            else
                            {
                                xElem.Descendants("CC").FirstOrDefault().Value = "0";
                                tvContent = new System.Data.SqlTypes.SqlXml(System.Xml.XmlReader.Create(new System.IO.StringReader(tvContent))).Value;
                                LogMessage("INFO", "TV closed caption :" + (tvContent.Length > 300 ? tvContent.Substring(0, 300) + "...." : tvContent), p_ArchiveMetaDataUpdateTask.ID);
                                LogMessage("INFO", "update archive metadata content for TV _ArchiveMediaID: " + xElem.Descendants("ID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                                UpdateArchiveMediaRecord(xElem.Descendants("ID").FirstOrDefault().Value, "TV", tvContent);
                            }
                        }

                        if (xElem.Descendants("Export").FirstOrDefault().Value == "1")
                        {
                            LogMessage("INFO", "call export service for TV media guid : " + xElem.Descendants("ArticleID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            string exportResponse = feedReportLogic.DoHttpGetRequest(ConfigurationManager.AppSettings["TVExportURL"] + xElem.Descendants("ArticleID").FirstOrDefault().Value);
                            string statusCode = String.Empty;
                            string responseMessage = "No response";

                            if (!string.IsNullOrWhiteSpace(exportResponse))
                            {
                                Dictionary<string, object> dictResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(exportResponse);
                                statusCode = dictResponse["Status"].ToString();
                                responseMessage = dictResponse["Message"].ToString();
                            }

                            if (lstExportSuccessStatuses.Contains(statusCode))
                            {
                                xElem.Descendants("Export").FirstOrDefault().Value = "0";
                            }
                            else
                            {
                                LogMessage("INFO", "failed export service , response : " + responseMessage, p_ArchiveMetaDataUpdateTask.ID);
                                xElem.Descendants("Export").FirstOrDefault().Value = "-1"; // If failed, mark the record as attempted but don't allow it to be tried again.
                            }
                        }

                        if (xElem.Descendants("ThumbGen").FirstOrDefault().Value == "1")
                        {
                            LogMessage("INFO", "call ThumbGen service for TV media guid : " + xElem.Descendants("ArticleID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            string thumbGenResponse = feedReportLogic.DoHttpGetRequest(ConfigurationManager.AppSettings["TVThumbGenURL"] + xElem.Descendants("ArticleID").FirstOrDefault().Value);
                            string statusCode = String.Empty;
                            string responseMessage = "No response";

                            if (!string.IsNullOrWhiteSpace(thumbGenResponse))
                            {
                                Dictionary<string, object> dictResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(thumbGenResponse);
                                statusCode = dictResponse["Status"].ToString();
                                responseMessage = dictResponse["Message"].ToString();
                            }

                            if (lstTVThumbSuccessStatuses.Contains(statusCode))
                            {
                                xElem.Descendants("ThumbGen").FirstOrDefault().Value = "0";
                            }
                            else
                            {
                                LogMessage("INFO", "failed ThumbGen service , response : " + responseMessage, p_ArchiveMetaDataUpdateTask.ID);
                                xElem.Descendants("ThumbGen").FirstOrDefault().Value = "-1"; // If failed, mark the record as attempted but don't allow it to be tried again.
                            }
                        }

                        if (xElem.Descendants("IOSExport").FirstOrDefault().Value == "1")
                        {
                            LogMessage("INFO", "call IOSExport service for TV media guid : " + xElem.Descendants("ArticleID").FirstOrDefault().Value, p_ArchiveMetaDataUpdateTask.ID);
                            string iosExportResponse = feedReportLogic.DoHttpGetRequest(ConfigurationManager.AppSettings["TVIOSExportURL"] + xElem.Descendants("ArticleID").FirstOrDefault().Value);
                            if (iosExportResponse.Contains(ConfigurationManager.AppSettings["TVIOSExportSuccessMessage"]) || iosExportResponse.Contains(ConfigurationManager.AppSettings["TVIOSExportAlreadyQueuedMessage"]))
                            {
                                xElem.Descendants("IOSExport").FirstOrDefault().Value = "0";
                            }
                            else
                            {
                                LogMessage("INFO", "failed IOSExport service , response : " + iosExportResponse, p_ArchiveMetaDataUpdateTask.ID);
                                xElem.Descendants("IOSExport").FirstOrDefault().Value = "-1"; // If failed, mark the record as attempted but don't allow it to be tried again.
                            }
                        }
                    }
                }

                if (isCompleted)
                {
                    p_ArchiveMetaDataUpdateTask.Status = DiscoveryArchiveMetaDataUpdateTask.TskStatus.COMPLETED;
                    UpdateArchiveTracking(p_ArchiveMetaDataUpdateTask.ID, xDoc.ToString(), p_ArchiveMetaDataUpdateTask.Status.ToString());
                }
            }
            catch (Exception ex)
            {
                p_ArchiveMetaDataUpdateTask.Status = DiscoveryArchiveMetaDataUpdateTask.TskStatus.EXCEPTION_METADATA;
                UpdateTaskStatus(DiscoveryArchiveMetaDataUpdateTask.TskStatus.EXCEPTION_METADATA.ToString(), p_ArchiveMetaDataUpdateTask.ID, true);

                CommonFunctions.WriteException(_connStr, "DiscoveryArchiveMetaDataUpdate", ex, p_ArchiveMetaDataUpdateTask.ID);
                LogMessage("ERROR", "An error occurred while processing discovery metadata update task.", p_ArchiveMetaDataUpdateTask.ID, ex);
            }
        }

        private void UpdateArchiveTracking(long taskID, string archiveTrackingXml, string status)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_metadataupd_IQService_DiscoveryArchiveMetaDataUpdate_UpdateArchiveTracking", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", taskID);
                    cmd.Parameters.AddWithValue("@ArchiveTracking", archiveTrackingXml);
                    cmd.Parameters.AddWithValue("@Status", status);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateArchiveMediaRecord(string mediaID, string mediaType, string mediaContent)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_metadataupd_IQService_ArchiveMetaDataUpdate_UpdateArchiveMedia", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@MediaID", mediaID);
                    cmd.Parameters.AddWithValue("@MediaType", mediaType);
                    cmd.Parameters.AddWithValue("@MediaContent", mediaContent);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateTaskStatus(string p_Status, Int64 p_ID, bool resetMachineName = false)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_metadataupd_IQService_DiscoveryArchiveMetaDataUpdate_UpdateStatus", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", p_ID);
                    cmd.Parameters.AddWithValue("@Status", p_Status);
                    cmd.Parameters.AddWithValue("@ResetMachineName", resetMachineName);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void LogMessage(string messageType, string message, Int64 taskID)
        {
            LogMessage(messageType, message, taskID, null);
        }

        private static void LogMessage(string messageType, string message, Exception ex)
        {
            LogMessage(messageType, message, null, ex);
        }

        private static void LogMessage(string messageType, string message, Int64? taskID = null, Exception ex = null)
        {
            string strTaskId = String.Empty;
            if (taskID.HasValue)
                strTaskId = "Task " + taskID.Value + " - ";

            switch (messageType)
            {
                case "INFO":
                    Logger.Info(Environment.MachineName + " - " + strTaskId + message);
                    break;
                case "DEBUG":
                    Logger.Debug(Environment.MachineName + " - " + strTaskId + message);
                    break;
                case "ERROR":
                    if (ex != null)
                        Logger.Error(Environment.MachineName + " - " + strTaskId + message, ex);
                    else
                        Logger.Error(Environment.MachineName + " - " + strTaskId + message);
                    break;
                case "WARNING":
                    if (ex != null)
                        Logger.Warning(Environment.MachineName + " - " + strTaskId + message, ex);
                    else
                        Logger.Warning(Environment.MachineName + " - " + strTaskId + message);
                    break;
                case "FATAL":
                    if (ex != null)
                        Logger.Fatal(Environment.MachineName + " - " + strTaskId + message, ex);
                    else
                        Logger.Fatal(Environment.MachineName + " - " + strTaskId + message);
                    break;
            }
        }

        public void Quit()
        {
            LogMessage("INFO", "DiscoveryArchiveMetaDataUpdate Service stopped at: " + DateTime.Now);
        }
    }
}
