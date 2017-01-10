using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using IQCommon.Model;
using IQMedia.Service.Common.Util;
using IQMedia.Service.Domain;
using IQMedia.Service.FeedsExport.Config;
using IQMedia.Service.Logic;
using FeedsSearch;

namespace IQMedia.Service.FeedsExport
{
    partial class FeedsExport : ServiceBase
    {
        private Thread _workerThread;
        private ServiceHost _host;
        private Queue<FeedsExportTask> _queueOfFeedsExportTsk;
        private static readonly string _connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
        private static List<double> _pollIntervals;

        public FeedsExport()
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
            base.OnStop();
        }

        public void Run()
        {
            try
            {
                Logger.Info("FeedsExport Service started at: " + DateTime.Now);
                /*var baseAddress = new Uri("http://localhost:" + ConfigSettings.Settings.WCFServicePort + "/NewsGeneratePDFWebService");

                _host = new ServiceHost(typeof(Service.NewsGeneratePDFWebService), baseAddress);
                // Enable metadata publishing.
                var smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
                _host.Description.Behaviors.Add(smb);

                // Open the ServiceHost to start listening for messages. Since
                // no endpoints are explicitly configured, the runtime will create
                // one endpoint per base address for each service contract implemented
                // by the service.
                _host.Open();*/


                StartTasks();
            }
            catch (AddressAccessDeniedException ex)
            {
                Logger.Fatal("You must start this service with administrative rights.", ex);
                Stop();
            }
            catch (AddressAlreadyInUseException ex)
            {
                Logger.Fatal("The WCF Service Port you have specified for this service is already in use. Please specify another.", ex);
                Stop();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex);
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

                while (_queueOfFeedsExportTsk.Count > 0)
                {
                    var totalNoOfTsk = _queueOfFeedsExportTsk.Count > Config.ConfigSettings.Settings.NoOfTasks ? Config.ConfigSettings.Settings.NoOfTasks : _queueOfFeedsExportTsk.Count;

                    List<Task> listOfTask = new List<Task>(totalNoOfTsk);

                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;

                    for (int index = 0; index < totalNoOfTsk; index++)
                    {
                        var tmpExportTask = _queueOfFeedsExportTsk.Dequeue();
                        Logger.Info("Assign " + tmpExportTask.ID + "to Task " + index);
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
                            Logger.Error("AggregateException", item);
                        }

                        Logger.Error("AggregateException ", ex);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Exception in waitall ", ex);
                    }

                    Logger.Info("Waitall complete");

                    foreach (var tsk in listOfTask)
                    {
                        FeedsExportTask feedsExportTsk = (FeedsExportTask)tsk.AsyncState;

                        Logger.Debug(feedsExportTsk.ID + ": Tsk Status- " + tsk.Status);
                        Logger.Debug(feedsExportTsk.ID + " FeedsExportTsk Status- " + feedsExportTsk.Status);
                    }

                    listOfTask.Clear();
                }

                double currentMinutes = DateTime.Now.Minute + (DateTime.Now.Second / 60D);
                double pollInterval = _pollIntervals.Where(s => s > currentMinutes).DefaultIfEmpty(_pollIntervals.Min() + 60).Min();
                int sleepTime = Convert.ToInt32((pollInterval - currentMinutes) * 60);

                Logger.Info("FeedsExport Service Enqueuer sleeping until " + DateTime.Now.AddSeconds(sleepTime).ToString("hh:mm:ss tt"));
                Thread.Sleep(Convert.ToInt32(TimeSpan.FromSeconds(sleepTime).TotalMilliseconds));
            }
        }

        private void ProcessDBRecords(FeedsExportTask p_FeedsExportTask, CancellationToken p_CToken)
        {
            try
            {
                string logPrefix = Environment.MachineName + " - Task " + p_FeedsExportTask.ID + " - ";

                var feedsLogic = (FeedReportLogic)LogicFactory.GetLogic(LogicType.FeedsReport);

                ClientSettings clientSettings = feedsLogic.GetClientSettings(p_FeedsExportTask.CustomerGUID);
                List<IQ_MediaTypeModel> lstMasterMediaTypes = IQCommon.CommonFunctions.GetMediaTypes(p_FeedsExportTask.CustomerGUID);

                if (p_CToken.IsCancellationRequested)
                {
                    Logger.Info("ID: " + p_FeedsExportTask.ID + " is cancelled.");
                    p_CToken.ThrowIfCancellationRequested();
                }

                Logger.Info("ID: " + p_FeedsExportTask.ID + " is processing.");

                p_FeedsExportTask.Status = FeedsExportTask.TskStatus.IN_PROCESS;
                UpdateTaskStatus(FeedsExportTask.TskStatus.IN_PROCESS.ToString(), p_FeedsExportTask.ID);

                List<Hit> lstHits = null;
                XDocument xDoc = null;
                bool isCompleted = true;
                if (p_FeedsExportTask.GetTVUrl)
                {
                    Logger.Info(String.Format("{0}Building TV url xml", logPrefix));

                    // Generating TV/Radio urls takes an extremely long time, whereas generating the CSV file is very fast. 
                    // So create them all up front, requeuing the job if it runs over the TIMEOUT limit. Once they're all done, create the CSV file.
                    if (String.IsNullOrEmpty(p_FeedsExportTask.TVUrlXml))
                    {
                        // Initialize the xml on the first run
                        xDoc = feedsLogic.BuildTVUrlXml(p_FeedsExportTask.IsSelectAll, p_FeedsExportTask.SearchCriteria, p_FeedsExportTask.ArticleXml, p_FeedsExportTask.SortType, Config.ConfigSettings.Settings.ProcessBatchSize, clientSettings, lstMasterMediaTypes, logPrefix, out lstHits);
                    }
                    else
                    {
                        xDoc = XDocument.Parse(p_FeedsExportTask.TVUrlXml);
                    }

                    Logger.Info(String.Format("{0}Generating TV urls", logPrefix));

                    foreach (XElement element in xDoc.Descendants("TVUrl"))
                    {
                        // If the task has timed out, save the progress made and requeue it
                        if (p_CToken.IsCancellationRequested)
                        {
                            isCompleted = false;
                            Logger.Info(String.Format("{0}Task is cancelled.", logPrefix));
                            p_FeedsExportTask.Status = FeedsExportTask.TskStatus.TIMEOUT_URL_GENERATION;
                            UpdateTVUrlXml(p_FeedsExportTask.ID, xDoc.ToString(), p_FeedsExportTask.Status.ToString());
                            break;
                        }

                        string mediaID = element.Descendants("MediaID").First().Value;
                        if (element.Descendants("Processed").First().Value == "0")
                        {
                            // Get encrypted url key. If the key already exists, the expiration date will be updated.
                            string svcUrl = lstMasterMediaTypes.First(f => f.SubMediaType == element.Descendants("SubMediaType").First().Value).ExternalPlayerUrlSvc;
                            bool isRetry = true;
                            int numTries = 0;
                            string response;
                            XDocument xDocResponse = null;
                            while (isRetry && numTries < 5)
                            {
                                // If a database connection couldn't be established by the web service, keep trying up to 5 times.
                                response = feedsLogic.DoHttpGetRequest(svcUrl.Replace("{DATE}", DateTime.Now.AddDays(clientSettings.RawMediaExpiration.Value).ToShortDateString())
                                    .Replace("{RESULTID}", mediaID)
                                    .Replace("{GUID}", String.Empty));

                                xDocResponse = XDocument.Parse(response);
                                XElement messageNode = xDocResponse.Descendants("Message").FirstOrDefault();

                                isRetry = messageNode != null && messageNode.Value == "The connection was not closed.";
                                numTries++;
                            }

                            // To get an idea of how many times this service is called, log the number of attempts if > 1
                            if (numTries > 1)
                            {
                                Logger.Info(String.Format("{0}Result ID: {1} - {2} attempts to get raw media url.", logPrefix, mediaID, numTries));
                            }

                            XElement keyNode = xDocResponse.Descendants("IQAgentFrameURL").FirstOrDefault();
                            if (keyNode != null)
                            {
                                element.Descendants("Url").First().Value = String.Format(ConfigurationManager.AppSettings["RawMediaPlayerUrl"], keyNode.Value);
                            }

                            element.Descendants("Processed").First().Value = "1";
                        }
                    }
                }

                // If the job timed out while building the TV urls, don't run the CSV generation
                if (isCompleted)
                {
                    var csvData = feedsLogic.ExportCSV(p_FeedsExportTask.IsSelectAll, p_FeedsExportTask.SearchCriteria, p_FeedsExportTask.ArticleXml, p_FeedsExportTask.SortType, Config.ConfigSettings.Settings.ProcessBatchSize, p_FeedsExportTask.GetTVUrl, clientSettings, lstMasterMediaTypes, xDoc, lstHits, logPrefix);

                    var rootPathLgc = (RootPathLogic)LogicFactory.GetLogic(LogicType.RootPath);

                    var rootPath = rootPathLgc.GetRootPathByID(p_FeedsExportTask.RootPathID);

                    var filename = p_FeedsExportTask.Title;
                    if (!String.IsNullOrEmpty(filename))
                    {
                        string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
                        string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

                        filename = System.Text.RegularExpressions.Regex.Replace(filename, invalidRegStr, "_") + "_";
                    }
                    filename += p_FeedsExportTask.CreatedDate.ToString("yyyyMMddHHmmssfff") + CommonFunctions.GenerateRandomString(6);

                    var subPath = "\\" + p_FeedsExportTask.CreatedDate.Year + "\\" + p_FeedsExportTask.CreatedDate.Month + "\\" + p_FeedsExportTask.CreatedDate.Day + "\\" + filename + ".csv";

                    var csvPath = rootPath.StoragePath + subPath;

                    var destinationPath = Path.GetDirectoryName(csvPath);

                    try
                    {
                        if (!Directory.Exists(destinationPath))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        p_FeedsExportTask.Status = FeedsExportTask.TskStatus.FAILED_DIRECTORY_CREATION;
                        Logger.Error("ID: " + p_FeedsExportTask.ID + " Directory Creation failed." + destinationPath, ex);
                        UpdateTaskStatus(FeedsExportTask.TskStatus.FAILED_DIRECTORY_CREATION.ToString(), p_FeedsExportTask.ID);
                        return;
                    }

                    try
                    {
                        using (FileStream fs = new FileStream(csvPath, FileMode.Create))
                        {
                            using (StreamWriter w = new StreamWriter(fs, Encoding.UTF8))
                            {
                                w.Write(csvData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        p_FeedsExportTask.Status = FeedsExportTask.TskStatus.FAILED_FILE_CREATION;
                        Logger.Error("ID: " + p_FeedsExportTask.ID + " FileCreation failed.", ex);
                        UpdateTaskStatus(FeedsExportTask.TskStatus.FAILED_FILE_CREATION.ToString(), p_FeedsExportTask.ID);
                        return;
                    }

                    try
                    {
                        feedsLogic.UpdateFeedsExportDownloadPath(p_FeedsExportTask.ID, subPath);
                    }
                    catch (Exception ex)
                    {
                        p_FeedsExportTask.Status = FeedsExportTask.TskStatus.FAILED_UPDATE_PATH;
                        Logger.Error("ID: " + p_FeedsExportTask.ID + " Status update failed.", ex);
                        UpdateTaskStatus(FeedsExportTask.TskStatus.FAILED_UPDATE_PATH.ToString(), p_FeedsExportTask.ID);
                        return;
                    }

                    p_FeedsExportTask.Status = FeedsExportTask.TskStatus.COMPLETED;
                    UpdateTaskStatus(FeedsExportTask.TskStatus.COMPLETED.ToString(), p_FeedsExportTask.ID);

                    // When the job is complete, clear out the xml in the IQService_FeedsExport table to save space
                    UpdateTVUrlXml(p_FeedsExportTask.ID, string.Empty, p_FeedsExportTask.Status.ToString());
                }
            }
            catch (OperationCanceledException ex)
            {
                p_FeedsExportTask.Status = FeedsExportTask.TskStatus.FAILED_TSK_CANCELLED;
                Logger.Error("ID: " + p_FeedsExportTask.ID + " task cancelled.", ex);
                UpdateTaskStatus(FeedsExportTask.TskStatus.FAILED_TSK_CANCELLED.ToString(), p_FeedsExportTask.ID);

                throw ex;
            }
            catch (Exception ex)
            {
                p_FeedsExportTask.Status = FeedsExportTask.TskStatus.FAILED;
                Logger.Error("ID: " + p_FeedsExportTask.ID + " failed.", ex);
                UpdateTaskStatus(FeedsExportTask.TskStatus.FAILED.ToString(), p_FeedsExportTask.ID);
            }
        }

        private void GetRecordsFromDB()
        {
            try
            {
                var connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
                _queueOfFeedsExportTsk = new Queue<FeedsExportTask>();

                using (var conn = new SqlConnection(connStr))
                {
                    Logger.Debug("Fetching queued items from database.");
                    conn.Open();

                    var cmdStr = "usp_feedsexport_IQService_FeedsExport_SelectQueued";

                    using (var cmd = conn.GetCommand(cmdStr, CommandType.StoredProcedure))
                    {
                        cmd.Parameters.Add("@TopRows", SqlDbType.Int).Value = Config.ConfigSettings.Settings.QueueLimit;
                        cmd.Parameters.Add("@MachineName", SqlDbType.VarChar).Value = System.Environment.MachineName;

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _queueOfFeedsExportTsk.Enqueue(new FeedsExportTask(Convert.ToInt64(reader["ID"]),
                                                                                 new Guid(Convert.ToString(reader["CustomerGUID"])),
                                                                                 Convert.ToString(reader["SearchCriteria"]),
                                                                                 Convert.ToString(reader["ArticleXml"]),
                                                                                 Convert.ToString(reader["SortType"]),
                                                                                 Convert.ToInt32(reader["_RootPathID"]),
                                                                                 Convert.ToBoolean(reader["IsSelectAll"]),
                                                                                 Convert.ToDateTime(reader["CreatedDate"]),
                                                                                 Convert.ToString(reader["Title"]),
                                                                                 Convert.ToBoolean(reader["GetTVUrl"]),
                                                                                 Convert.ToString(reader["TVUrlXml"])
                                                                     )

                                             );
                        }

                        Logger.Info(_queueOfFeedsExportTsk.Count + " new items enqueued.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred while attempting to retrieve new tasks from the database.", ex);
            }
        }

        private void UpdateTVUrlXml(long taskID, string tvUrlXml, string status)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_feedsexport_IQService_FeedsExport_UpdateTVUrlXml", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", taskID);
                    cmd.Parameters.AddWithValue("@TVUrlXml", tvUrlXml);
                    cmd.Parameters.AddWithValue("@Status", status);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateTaskStatus(string p_Status, Int64 p_ID)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_feedsexport_IQService_FeedsExport_UpdateStatus", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", p_ID);
                    cmd.Parameters.AddWithValue("@Status", p_Status);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Initializes the service and verifies config data.
        /// </summary>
        protected static void InitializeService()
        {
            Logger.Info("Initializing settings and parameters");

            //Re-fetch the config settings...
            ConfigurationManager.RefreshSection("FeedsExportSettings");
            SolrEngineLogic.SolrRequestor = SolrEngineLogic.SolrReqestorType.FeedsExport;
            IQCommon.CommonFunctions.ConnString = _connStr;

            //Test our config file to make sure we have everything we need...
            if (ConfigSettings.Settings == null)
                throw new XmlException("App.config is missing <FeedsExportSettings> node.");

            _pollIntervals = ConfigSettings.Settings.PollIntervals.Split(',').Select(s => Convert.ToDouble(s)).ToList();
        }

        /// <summary>
        /// Need to handle later all tasks, to either cancel or completed. 
        /// </summary>
        public void Quit()
        {
            Logger.Info("FeedsExport Service stopped at: " + DateTime.Now);
        }
    }
}
