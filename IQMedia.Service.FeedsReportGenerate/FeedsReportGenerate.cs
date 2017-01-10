using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.ServiceModel;
using IQMedia.Service.Common.Util;
using System.Configuration;
using IQMedia.Service.FeedsReportGenerate.Config;
using System.Xml;
using System.ServiceModel.Description;
using System.Data.SqlClient;
using System.Threading.Tasks;
using IQMedia.Service.Logic;
using System.Xml.Linq;

namespace IQMedia.Service.FeedsReportGenerate
{
    public partial class FeedsReportGenerate : ServiceBase
    {
        private Thread _workerThread;
        private ServiceHost _host;
        private Queue<FeedReportGenerateTask> _queueOfFeedReportGenTask;
        private static readonly string _connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
        private static List<double> _pollIntervals;

        public FeedsReportGenerate()
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

        /// <summary>
        /// Initializes the service and verifies config data.
        /// </summary>
        protected static void InitializeService()
        {
            LogMessage("INFO", "Initializing settings and parameters");

            //Re-fetch the config settings...
            ConfigurationManager.RefreshSection("FeedsReportGenerateSettings");
            //ConfigurationManager.RefreshSection("FFmpegSettings");
            //Test our config file to make sure we have everything we need...
            if (ConfigSettings.Settings == null)
                throw new XmlException("App.config is missing <FeedsReportGenerateSettings> node.");

            IQCommon.CommonFunctions.ConnString = _connStr;
            SolrEngineLogic.SolrRequestor = SolrEngineLogic.SolrReqestorType.FeedsToLibrary;
            _pollIntervals = ConfigSettings.Settings.PollIntervals.Split(',').Select(s => Convert.ToDouble(s)).ToList();
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        public void Run()
        {
            try
            {
                LogMessage("INFO", "FeedsReportGenerate Service started at: " + DateTime.Now);
                //var baseAddress = new Uri("http://localhost:" + ConfigSettings.Settings.WCFServicePort + "/FeedsReportGenerateWebService");

                //_host = new ServiceHost(typeof(Service.FeedsReportGenerateWeb), baseAddress);
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
                CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
                LogMessage("FATAL", "You must start this service with administrative rights.", ex);
                Stop();
            }
            catch (AddressAlreadyInUseException ex)
            {
                CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
                LogMessage("FATAL", "The WCF Service Port you have specified for this service is already in use. Please specify another.", ex);
                Stop();
            }
            catch (Exception ex)
            {
                CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
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

                while (_queueOfFeedReportGenTask.Count > 0)
                {
                    var totalNoOfTsk = _queueOfFeedReportGenTask.Count > Config.ConfigSettings.Settings.NoOfTasks ? Config.ConfigSettings.Settings.NoOfTasks : _queueOfFeedReportGenTask.Count;

                    List<Task> listOfTask = new List<Task>(totalNoOfTsk);

                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;

                    for (int index = 0; index < totalNoOfTsk; index++)
                    {
                        var tmpExportTask = _queueOfFeedReportGenTask.Dequeue();
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
                            CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
                            LogMessage("ERROR", "AggregateException", item);
                        }

                        CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
                        LogMessage("ERROR", "AggregateException ", ex);
                    }
                    catch (Exception ex)
                    {
                        CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
                        LogMessage("ERROR", "Exception in waitall ", ex);
                    }

                    LogMessage("INFO", "Waitall complete");

                    foreach (var tsk in listOfTask)
                    {
                        FeedReportGenerateTask feedReportGenerateTask = (FeedReportGenerateTask)tsk.AsyncState;

                        LogMessage("DEBUG", "Tsk Status - " + tsk.Status, feedReportGenerateTask.ID);
                        LogMessage("DEBUG", "FeedReportGenerateTsk Status - " + feedReportGenerateTask.Status, feedReportGenerateTask.ID);
                    }

                    listOfTask.Clear();
                }

                double currentMinutes = DateTime.Now.Minute + (DateTime.Now.Second / 60D);
                double pollInterval = _pollIntervals.Where(s => s > currentMinutes).DefaultIfEmpty(_pollIntervals.Min() + 60).Min();
                int sleepTime = Convert.ToInt32((pollInterval - currentMinutes) * 60);

                LogMessage("INFO", "Feed Report Generate Service Enqueuer sleeping until " + DateTime.Now.AddSeconds(sleepTime).ToString("hh:mm:ss tt"));
                Thread.Sleep(Convert.ToInt32(TimeSpan.FromSeconds(sleepTime).TotalMilliseconds));
            }
        }

        private void GetRecordsFromDB()
        {
            try
            {
                _queueOfFeedReportGenTask = new Queue<FeedReportGenerateTask>();

                using (var conn = new SqlConnection(_connStr))
                {
                    LogMessage("DEBUG", "Fetching queued items from database.");
                    conn.Open();

                    var cmdStr = "usp_feedrptgen_IQService_FeedReportGenerate_SelectQueued";

                    using (var cmd = conn.GetCommand(cmdStr, CommandType.StoredProcedure))
                    {
                        cmd.Parameters.Add("@TopRows", SqlDbType.Int).Value = Config.ConfigSettings.Settings.QueueLimit;
                        cmd.Parameters.Add("@MachineName", SqlDbType.VarChar).Value = System.Environment.MachineName;

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _queueOfFeedReportGenTask.Enqueue(new FeedReportGenerateTask
                            {
                                ID = reader.GetInt64(0),
                                MediaID = reader.GetString(1),
                                customerGUID = reader.GetGuid(2),
                                clientGUID = reader.GetGuid(3)
                            });
                        }
                        LogMessage("INFO", _queueOfFeedReportGenTask.Count + " new items enqueued.");
                    }
                }
            }
            catch (Exception ex)
            {
                CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex);
                LogMessage("ERROR", "An error occurred while attempting to retrieve new tasks from the database.", ex);
            }
        }

        private void ProcessDBRecords(FeedReportGenerateTask p_FeedReportGenerateTask, CancellationToken p_CToken)
        {
            try
            {
                string logPrefix = Environment.MachineName + " - Task " + p_FeedReportGenerateTask.ID + " - ";

                if (p_CToken.IsCancellationRequested)
                {
                    LogMessage("INFO", "Task is cancelled.", p_FeedReportGenerateTask.ID);
                    p_CToken.ThrowIfCancellationRequested();
                }

                LogMessage("INFO", "Task is processing.", p_FeedReportGenerateTask.ID);
                
                p_FeedReportGenerateTask.Status = FeedReportGenerateTask.TskStatus.IN_PROCESS;
                UpdateTaskStatus(FeedReportGenerateTask.TskStatus.IN_PROCESS.ToString(), p_FeedReportGenerateTask.ID);
                
                LogMessage("INFO", "Get media info from solr.", p_FeedReportGenerateTask.ID);
                LogMessage("INFO", "List of media IDs:" + p_FeedReportGenerateTask.MediaID, p_FeedReportGenerateTask.ID);

                // Split the processing into small batches and initialize the job as successful. If any of the batches fail, set the job as failed.
                FeedReportLogic feedReportLogic = (FeedReportLogic)LogicFactory.GetLogic(LogicType.FeedsReport);
                XDocument xDoc = XDocument.Parse(p_FeedReportGenerateTask.MediaID);
                List<Tuple<XDocument, List<Int64>>> lstXmlInputs = feedReportLogic.GetMediaResults(xDoc.Descendants("ID").Select(x => x.Value).ToList(), p_FeedReportGenerateTask.customerGUID, ConfigSettings.Settings.ProcessBatchSize, logPrefix);

                p_FeedReportGenerateTask.Status = FeedReportGenerateTask.TskStatus.READY_FOR_METADATA;

                for (int i = 0; i < lstXmlInputs.Count; i++)
                {
                    LogMessage("INFO", "Call feeds insert sp for batch " + i, p_FeedReportGenerateTask.ID);

                    if (ConfigurationManager.AppSettings.Get("LogXmlInput").ToLower() == "true")
                    {
                        Logger.Debug(logPrefix + lstXmlInputs[i].Item1.ToString());
                    }

                    int? spResult = feedReportLogic.InsertFeedReport(p_FeedReportGenerateTask.ID, lstXmlInputs[i].Item1.ToString());
                    if (!spResult.HasValue || spResult.Value != 1)
                    {
                        p_FeedReportGenerateTask.Status = FeedReportGenerateTask.TskStatus.FAILED;
                        break;
                    }
                    else
                    {
                        // If the records were successfully added to Library, mark them as read.
                        Logger.Info(logPrefix + "Mark batch " + i + " records as read.");
                        feedReportLogic.MarkAsRead(lstXmlInputs[i].Item2.Distinct().ToList(), p_FeedReportGenerateTask.clientGUID, logPrefix);
                    }
                }

                UpdateTaskStatus(p_FeedReportGenerateTask.Status.ToString(), p_FeedReportGenerateTask.ID);

                RequestDownloadClient requestDownloadClient = new RequestDownloadClient();
                requestDownloadClient.WakeupService();
            }
            catch (Exception ex)
            {
                p_FeedReportGenerateTask.Status = FeedReportGenerateTask.TskStatus.EXCEPTION;
                UpdateTaskStatus(FeedReportGenerateTask.TskStatus.EXCEPTION.ToString(), p_FeedReportGenerateTask.ID);

                CommonFunctions.WriteException(_connStr, "FeedsReportGenerate", ex, p_FeedReportGenerateTask.ID);
                LogMessage("ERROR", "An error occurred while processing FeedsReportGenerate task.", p_FeedReportGenerateTask.ID, ex);
            }
        }

        private void UpdateTaskStatus(string p_Status, Int64 p_ID)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_feedrptgen_IQService_FeedReportGenerate_UpdateStatus", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", p_ID);
                    cmd.Parameters.AddWithValue("@Status", p_Status);

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
            LogMessage("INFO", "FeedsReportGenerate Service stopped at: " + DateTime.Now);
        }
    }
}
