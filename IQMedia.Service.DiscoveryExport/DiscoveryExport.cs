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
using System.Configuration;
using System.Threading.Tasks;
using System.Data.SqlClient;
using IQMedia.Service.DiscoveryExport.Config;
using IQMedia.Service.Common.Util;
using System.Xml;
using IQMedia.Service.Logic;
using System.IO;


namespace IQMedia.Service.DiscoveryExport
{
    partial class DiscoveryExport : ServiceBase
    {
        private Thread _workerThread;
        private ServiceHost _host;
        private Queue<DiscoveryExportTask> _queOfDiscExportTsk;
        private static readonly string _connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
        private static List<double> _pollIntervals;

        public DiscoveryExport()
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
                Logger.Info("DiscoveryExport Service started at: " + DateTime.Now);
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
                
                while (_queOfDiscExportTsk.Count > 0)
                {
                    var totalNoOfTsk = _queOfDiscExportTsk.Count > Config.ConfigSettings.Settings.NoOfTasks ? Config.ConfigSettings.Settings.NoOfTasks : _queOfDiscExportTsk.Count;

                    List<Task> listOfTask = new List<Task>(totalNoOfTsk);

                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;

                    for (int index = 0; index < totalNoOfTsk; index++)
                    {
                        var tmpExportTask = _queOfDiscExportTsk.Dequeue();
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
                        DiscoveryExportTask discExportTsk = (DiscoveryExportTask)tsk.AsyncState;

                        Logger.Debug(discExportTsk.ID + ": Tsk Status- " + tsk.Status);
                        Logger.Debug(discExportTsk.ID + " DiscExportTsk Status- " + discExportTsk.Status);

                    }

                    listOfTask.Clear();
                }

                double currentMinutes = DateTime.Now.Minute + (DateTime.Now.Second / 60D);
                double pollInterval = _pollIntervals.Where(s => s > currentMinutes).DefaultIfEmpty(_pollIntervals.Min() + 60).Min();
                int sleepTime = Convert.ToInt32((pollInterval - currentMinutes) * 60);

                Logger.Info("DiscoveryExport Service Enqueuer sleeping until " + DateTime.Now.AddSeconds(sleepTime).ToString("hh:mm:ss tt"));
                Thread.Sleep(Convert.ToInt32(TimeSpan.FromSeconds(sleepTime).TotalMilliseconds));
            }
        }

        private void ProcessDBRecords(DiscoveryExportTask p_DiscExportTask, CancellationToken p_CToken)
        {
            try
            {
                if (p_CToken.IsCancellationRequested)
                {
                    Logger.Info("ID: " + p_DiscExportTask.ID + " is cancelled.");
                    p_CToken.ThrowIfCancellationRequested();
                }

                Logger.Info("ID: " + p_DiscExportTask.ID + " is processing.");

                p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.IN_PROCESS;
                UpdateTaskStatus(DiscoveryExportTask.TskStatus.IN_PROCESS.ToString(), p_DiscExportTask.ID);

                var discLgc = (DiscoveryReportLogic)LogicFactory.GetLogic(LogicType.DiscoveryReport);

                var csvData=discLgc.ExportCSV(p_DiscExportTask.ID, p_DiscExportTask.IsSelectAll, p_DiscExportTask.SearchCriteria, p_DiscExportTask.ArticleXml,p_DiscExportTask.CustomerGUID);

                var rootPathLgc=(RootPathLogic)LogicFactory.GetLogic(LogicType.RootPath);

                var rootPath = rootPathLgc.GetRootPathByID(p_DiscExportTask.RootPathID);

                var subPath = "\\" + p_DiscExportTask.CreatedDate.Year + "\\" + p_DiscExportTask.CreatedDate.Month + "\\" + p_DiscExportTask.CreatedDate.Day + "\\" + p_DiscExportTask.CreatedDate.ToString("yyyyMMddHHmmssfff") + CommonFunctions.GenerateRandomString(6) + ".csv";

                var csvPath = rootPath.StoragePath + subPath;

                if (p_CToken.IsCancellationRequested)
                {
                    Logger.Info("ID: " + p_DiscExportTask.ID + " is cancelled.");
                    p_CToken.ThrowIfCancellationRequested();
                }

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
                    p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.FAILED_DIRECTORY_CREATION;
                    Logger.Error("ID: " + p_DiscExportTask.ID + " Directory Creation failed." +destinationPath, ex);
                    UpdateTaskStatus(DiscoveryExportTask.TskStatus.FAILED_DIRECTORY_CREATION.ToString(), p_DiscExportTask.ID);
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
                    p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.FAILED_FILE_CREATION;
                    Logger.Error("ID: " + p_DiscExportTask.ID + " FileCreation failed.",ex);
                    UpdateTaskStatus(DiscoveryExportTask.TskStatus.FAILED_FILE_CREATION.ToString(), p_DiscExportTask.ID);
                    return;
                }

                try
                {
                    discLgc.UpdateDiscExpDownloadPath(p_DiscExportTask.ID, subPath);
                }
                catch (Exception ex)
                {
                    p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.FAILED_UPDATE_PATH;
                    Logger.Error("ID: " + p_DiscExportTask.ID + " Status update failed.",ex);
                    UpdateTaskStatus(DiscoveryExportTask.TskStatus.FAILED_UPDATE_PATH.ToString(), p_DiscExportTask.ID);
                    return;
                }

                p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.COMPLETED;
                UpdateTaskStatus(DiscoveryExportTask.TskStatus.COMPLETED.ToString(), p_DiscExportTask.ID);
            }
            catch(OperationCanceledException ex)
            {
                p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.FAILED_TSK_CANCELLED;
                Logger.Error("ID: " + p_DiscExportTask.ID + " task cancelled.", ex);
                UpdateTaskStatus(DiscoveryExportTask.TskStatus.FAILED_TSK_CANCELLED.ToString(), p_DiscExportTask.ID);

                throw ex;
            }
            catch (Exception ex)
            {
                p_DiscExportTask.Status = DiscoveryExportTask.TskStatus.FAILED;
                Logger.Error("ID: " + p_DiscExportTask.ID + " failed.", ex);
                UpdateTaskStatus(DiscoveryExportTask.TskStatus.FAILED.ToString(), p_DiscExportTask.ID);
            }            
        }

        private void GetRecordsFromDB()
        {
            try
            {
                var connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
                _queOfDiscExportTsk = new Queue<DiscoveryExportTask>();

                using (var conn = new SqlConnection(connStr))
                {
                    Logger.Debug("Fetching queued items from database.");
                    conn.Open();

                    var cmdStr = "usp_discexport_IQService_DiscoveryExport_SelectQueued";

                    using (var cmd = conn.GetCommand(cmdStr, CommandType.StoredProcedure))
                    {
                        cmd.Parameters.Add("@TopRows", SqlDbType.Int).Value = Config.ConfigSettings.Settings.QueueLimit;
                        cmd.Parameters.Add("@MachineName", SqlDbType.VarChar).Value = System.Environment.MachineName;

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _queOfDiscExportTsk.Enqueue(new DiscoveryExportTask(Convert.ToInt64(reader["ID"]),
                                                                                 new Guid(Convert.ToString(reader["CustomerGUID"])),
                                                                                 Convert.ToString(reader["SearchCriteria"]),
                                                                                 Convert.ToString(reader["ArticleXml"]),
                                                                                 Convert.ToInt32(reader["_RootPathID"]),
                                                                                 Convert.ToBoolean(reader["IsSelectAll"]),
                                                                                 Convert.ToDateTime(reader["CreatedDate"])
                                                                     )

                                             );
                        }

                        Logger.Info(_queOfDiscExportTsk.Count + " new items enqueued.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred while attempting to retrieve new tasks from the database.", ex);
            }
        }

        private void UpdateTaskStatus(string p_Status, Int64 p_ID)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_discexport_IQService_DiscoveryExport_UpdateStatus", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", p_ID);
                    cmd.Parameters.AddWithValue("@Status",p_Status);

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
            ConfigurationManager.RefreshSection("DiscoveryExportSettings");
            SolrEngineLogic.SolrRequestor = SolrEngineLogic.SolrReqestorType.DiscoveryExport;
            IQCommon.CommonFunctions.ConnString = _connStr;

            //Test our config file to make sure we have everything we need...
            if (ConfigSettings.Settings == null)
                throw new XmlException("App.config is missing <DiscoveryExportSettings> node.");

            _pollIntervals = ConfigSettings.Settings.PollIntervals.Split(',').Select(s => Convert.ToDouble(s)).ToList();
        }

        /// <summary>
        /// Need to handle later all tasks, to either cancel or completed. 
        /// </summary>
        public void Quit()
        {
            Logger.Info("DiscoveryExport Service stopped at: " + DateTime.Now);
        }
    }
}
