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
using IQMedia.Service.DiscoveryReportGenerate.Config;
using IQMedia.Service.Logic;
using IQCommon.Model;
using System.Reflection;

namespace IQMedia.Service.DiscoveryReportGenerate
{
    public partial class DiscoveryReportGenerate : ServiceBase
    {
        private Thread _workerThread;
        private ServiceHost _host;
        private Queue<DiscoveryReportGenerateTask> _queueOfDiscReportGenTask;
        private static readonly string _connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
        private static List<double> _pollIntervals;

        public DiscoveryReportGenerate()
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
            Logger.Info("Initializing settings and parameters");

            //Re-fetch the config settings...
            ConfigurationManager.RefreshSection("DiscoveryReportGenerateSettings");
            //ConfigurationManager.RefreshSection("FFmpegSettings");
            //Test our config file to make sure we have everything we need...
            if (ConfigSettings.Settings == null)
                throw new XmlException("App.config is missing <DiscoveryReportGenerateSettings> node.");

            IQCommon.CommonFunctions.ConnString = _connStr;
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
                LogMessage("INFO", "DiscoveryReportGenerate Service started at: " + DateTime.Now);
                //var baseAddress = new Uri("http://localhost:" + ConfigSettings.Settings.WCFServicePort + "/DiscoveryReportGenerateWebService");

                //_host = new ServiceHost(typeof(Service.DiscoveryReportGenerateWebService), baseAddress);
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
                CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
                LogMessage("FATAL", "You must start this service with administrative rights.", ex);
                Stop();
            }
            catch (AddressAlreadyInUseException ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
                LogMessage("FATAL", "The WCF Service Port you have specified for this service is already in use. Please specify another.", ex);
                Stop();
            }
            catch (Exception ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
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

                while (_queueOfDiscReportGenTask.Count > 0)
                {
                    var totalNoOfTsk = _queueOfDiscReportGenTask.Count > Config.ConfigSettings.Settings.NoOfTasks ? Config.ConfigSettings.Settings.NoOfTasks : _queueOfDiscReportGenTask.Count;

                    List<Task> listOfTask = new List<Task>(totalNoOfTsk);

                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;

                    for (int index = 0; index < totalNoOfTsk; index++)
                    {
                        var tmpExportTask = _queueOfDiscReportGenTask.Dequeue();
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
                            CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
                            LogMessage("ERROR", "AggregateException", item);
                        }

                        CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
                        LogMessage("ERROR", "AggregateException ", ex);
                    }
                    catch (Exception ex)
                    {
                        CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
                        LogMessage("ERROR", "Exception in waitall ", ex);
                    }

                    LogMessage("INFO", "Waitall complete");

                    foreach (var tsk in listOfTask)
                    {
                        DiscoveryReportGenerateTask discReportGenerateTask = (DiscoveryReportGenerateTask)tsk.AsyncState;

                        LogMessage("DEBUG", "Tsk Status - " + tsk.Status, discReportGenerateTask.ID);
                        LogMessage("DEBUG", "DiscoveryReportGenerateTsk Status - " + discReportGenerateTask.Status, discReportGenerateTask.ID);
                    }

                    listOfTask.Clear();
                }

                double currentMinutes = DateTime.Now.Minute + (DateTime.Now.Second / 60D);
                double pollInterval = _pollIntervals.Where(s => s > currentMinutes).DefaultIfEmpty(_pollIntervals.Min() + 60).Min();
                int sleepTime = Convert.ToInt32((pollInterval - currentMinutes) * 60);

                LogMessage("INFO", "Discovery Report Generate Service Enqueuer sleeping until " + DateTime.Now.AddSeconds(sleepTime).ToString("hh:mm:ss tt"));
                Thread.Sleep(Convert.ToInt32(TimeSpan.FromSeconds(sleepTime).TotalMilliseconds));
            }
        }

        private void GetRecordsFromDB()
        {
            try
            {
                _queueOfDiscReportGenTask = new Queue<DiscoveryReportGenerateTask>();

                using (var conn = new SqlConnection(_connStr))
                {
                    LogMessage("DEBUG", "Fetching queued items from database.");
                    conn.Open();

                    var cmdStr = "usp_discrptgen_IQService_DiscoveryReportGenerate_SelectQueued";

                    using (var cmd = conn.GetCommand(cmdStr, CommandType.StoredProcedure))
                    {
                        cmd.Parameters.Add("@TopRows", SqlDbType.Int).Value = Config.ConfigSettings.Settings.QueueLimit;
                        cmd.Parameters.Add("@MachineName", SqlDbType.VarChar).Value = System.Environment.MachineName;

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _queueOfDiscReportGenTask.Enqueue(new DiscoveryReportGenerateTask
                            {
                                ID = reader.GetInt64(0),
                                MediaID = reader.GetString(1),
                                ClientGuid = reader.GetGuid(2),
                                CustomerGuid = reader.GetGuid(3)
                            });
                        }
                        LogMessage("INFO", _queueOfDiscReportGenTask.Count + " new items enqueued.");
                    }
                }
            }
            catch (Exception ex)
            {
                CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex);
                LogMessage("ERROR", "An error occurred while attempting to retrieve new tasks from the database.", ex);
            }
        }

        private void ProcessDBRecords(DiscoveryReportGenerateTask p_DiscReportGenerateTask, CancellationToken p_CToken)
        {
            try
            {
                string logPrefix = Environment.MachineName + " - Task " + p_DiscReportGenerateTask.ID + " - ";

                if (p_CToken.IsCancellationRequested)
                {
                    LogMessage("INFO", "Task is cancelled.", p_DiscReportGenerateTask.ID);
                    p_CToken.ThrowIfCancellationRequested();
                }

                LogMessage("INFO", "Task is processing.", p_DiscReportGenerateTask.ID);

                p_DiscReportGenerateTask.Status = DiscoveryReportGenerateTask.TskStatus.IN_PROCESS;
                UpdateTaskStatus(DiscoveryReportGenerateTask.TskStatus.IN_PROCESS.ToString(), p_DiscReportGenerateTask.ID);
                
                List<IQ_MediaTypeModel> lstMasterSubMediaTypes = IQCommon.CommonFunctions.GetMediaTypes(p_DiscReportGenerateTask.CustomerGuid).Where(w => w.TypeLevel == 2 && w.IsActiveDiscovery).ToList();
                DiscoveryReportLogic discoveryReportLogic = (DiscoveryReportLogic)LogicFactory.GetLogic(LogicType.DiscoveryReport);

                Dictionary<string, Tuple<float?, float?>> dictThresholds = new Dictionary<string, Tuple<float?, float?>>();
                float? TVLowThresholdValue = null;
                float? TVHighThresholdValue = null;
                float? NMLowThresholdValue = null;
                float? NMHighThresholdValue = null;
                float? SMLowThresholdValue = null;
                float? SMHighThresholdValue = null;
                float? PQLowThresholdValue = null;
                float? PQHighThresholdValue = null;

                LogMessage("INFO", "Fetching sentiment settings by client : " + p_DiscReportGenerateTask.ClientGuid, p_DiscReportGenerateTask.ID);

                discoveryReportLogic.GetSentimentSettingsByClientGuid(p_DiscReportGenerateTask.ClientGuid, out TVLowThresholdValue, out TVHighThresholdValue, out NMLowThresholdValue, out NMHighThresholdValue, out SMLowThresholdValue, out SMHighThresholdValue, out PQLowThresholdValue, out PQHighThresholdValue);
                bool useProminenceMediaValue = discoveryReportLogic.GetUseProminenceMediaValue(p_DiscReportGenerateTask.CustomerGuid);

                dictThresholds.Add("TV", new Tuple<float?, float?>(TVLowThresholdValue, TVHighThresholdValue));
                dictThresholds.Add("NM", new Tuple<float?, float?>(NMLowThresholdValue, NMHighThresholdValue));
                dictThresholds.Add("SM", new Tuple<float?, float?>(SMLowThresholdValue, SMHighThresholdValue));
                dictThresholds.Add("PQ", new Tuple<float?, float?>(PQLowThresholdValue, PQHighThresholdValue));

                LogMessage("INFO", "Fetching list of medias from discovery xml : " + p_DiscReportGenerateTask.MediaID, p_DiscReportGenerateTask.ID);
                XDocument xdocMediaID = XDocument.Parse(p_DiscReportGenerateTask.MediaID);

                // Split the processing into small batches. If any of the batches fail, set the job as failed.
                bool isSuccess = true;
                foreach (IQ_MediaTypeModel objSubMediaType in lstMasterSubMediaTypes)
                {
                    List<Article> lstArticles = xdocMediaID.Descendants("MediaIds").Descendants(objSubMediaType.SubMediaType).Elements("Media").Select(s => new Article { ArticleID = s.Element("ID").Value, SearchTerm = s.Element("SearchTerm").Value }).ToList(); 
                    if (lstArticles.Count > 0 && isSuccess)
                    {
                        isSuccess = ProcessBatches(lstArticles, objSubMediaType, dictThresholds, useProminenceMediaValue, p_DiscReportGenerateTask.ID);
                    }
                }
                
                p_DiscReportGenerateTask.Status = isSuccess ? DiscoveryReportGenerateTask.TskStatus.READY_FOR_METADATA : DiscoveryReportGenerateTask.TskStatus.FAILED;
                UpdateTaskStatus(p_DiscReportGenerateTask.Status.ToString(), p_DiscReportGenerateTask.ID);
            }
            catch (Exception ex)
            {
                p_DiscReportGenerateTask.Status = DiscoveryReportGenerateTask.TskStatus.EXCEPTION;
                UpdateTaskStatus(DiscoveryReportGenerateTask.TskStatus.EXCEPTION.ToString(), p_DiscReportGenerateTask.ID);

                CommonFunctions.WriteException(_connStr, "DiscoveryReportGenerate", ex, p_DiscReportGenerateTask.ID);
                LogMessage("ERROR", "An error occurred while processing DiscoveryReportGenerate task.", p_DiscReportGenerateTask.ID, ex);
            }
        }

        private bool ProcessBatches(List<Article> lstArticles, IQ_MediaTypeModel objSubMediaType, Dictionary<string, Tuple<float?, float?>> dictThresholds, bool useProminenceMediaValue, long taskID)
        {
            string logPrefix = Environment.MachineName + " - Task " + taskID + " - ";
            int batchSize = ConfigSettings.Settings.ProcessBatchSize;

            DiscoveryReportLogic discoveryReportLogic = (DiscoveryReportLogic)LogicFactory.GetLogic(LogicType.DiscoveryReport);
            
            for (int i = 0; i < lstArticles.Count; i += batchSize)
            {
                XDocument xdocMediaData = new XDocument(new XElement("MediaData"));
                List<Article> lstBatchArticles = new List<Article>();

                if (i + batchSize > lstArticles.Count)
                {
                    lstBatchArticles = lstArticles.GetRange(i, lstArticles.Count - i);
                }
                else
                {
                    lstBatchArticles = lstArticles.GetRange(i, batchSize);
                }

                LogMessage("INFO", String.Format("Fetch content for {0} media items {1}-{2}.", objSubMediaType.SubMediaType, i, i + lstBatchArticles.Count), taskID);

                if (lstBatchArticles.Count > 0)
                {
                    /* Call the appropriate method in DiscoveryReportLogic based on the DiscRptGenSearchMethod field of the IQ_MediaTypes table
                     * The method must return an XDocument object and accept the following parameters in this order:
                     *      - List of Article objects
                     *      - IQ_MediaTypeModel object
                     *      - Dictionary of sentiment thresholds (Dictionary<string, Tuple<float?, float?>>)
                     *      - Use Prominence Media Value boolean
                     *      - Log Prefix string
                     */
                    Type type = discoveryReportLogic.GetType();
                    MethodInfo methodInfo = type.GetMethod(objSubMediaType.DiscRptGenSearchMethod);
                    object classInstance = Activator.CreateInstance(type, null);
                    object[] parameters = new object[] { lstBatchArticles, objSubMediaType, dictThresholds, useProminenceMediaValue, logPrefix };

                    XDocument xDoc = (XDocument)methodInfo.Invoke(classInstance, parameters);

                    if (xDoc != null)
                    {
                        xdocMediaData.Root.Add(xDoc.Root);
                    }
                }

                LogMessage("INFO", String.Format("Call discovery insert sp for {0} media items {1}-{2}.", objSubMediaType.SubMediaType, i, i + lstBatchArticles.Count), taskID);

                if (ConfigurationManager.AppSettings.Get("LogXmlInput").ToLower() == "true")
                {
                    LogMessage("INFO", "Insert xml for sp: " + xdocMediaData.ToString(), taskID);
                }

                int? spResult = discoveryReportLogic.InsertDiscoveryReport(taskID, xdocMediaData.ToString());
                if (!spResult.HasValue || spResult.Value != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateTaskStatus(string p_Status, Int64 p_ID)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_discrptgen_IQService_DiscoveryReportGenerate_UpdateStatus", CommandType.StoredProcedure))
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
            LogMessage("INFO", "DiscoveryReportGenerate Service stopped at: " + DateTime.Now);
        }
    }
}
