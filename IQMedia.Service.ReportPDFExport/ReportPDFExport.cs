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
using IQMedia.Service.ReportPDFExport.Config;
using IQMedia.Service.Common.Util;
using System.Xml;
using IQMedia.Service.Logic;
using System.IO;
using HiQPdf;

namespace IQMedia.Service.ReportPDFExport
{
    partial class ReportPDFExport : ServiceBase
    {
        private Thread _workerThread;
        private ServiceHost _host;
        private Queue<ReportPDFExportTask> _queOfRptPDFExportTsk;
        private readonly string _connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
        private static List<double> _pollIntervals;

        public ReportPDFExport()
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
                Logger.Info("ReportPDFExport Service started at: " + DateTime.Now);
                StartTasks();
            }
            catch (AddressAccessDeniedException ex)
            {
                Logger.Fatal("You must start this service with administrative rights.", ex);
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

                while (_queOfRptPDFExportTsk.Count > 0)
                {
                    var totalNoOfTsk = _queOfRptPDFExportTsk.Count > Config.ConfigSettings.Settings.NoOfTasks ? Config.ConfigSettings.Settings.NoOfTasks : _queOfRptPDFExportTsk.Count;

                    List<Task> listOfTask = new List<Task>(totalNoOfTsk);

                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;

                    for (int index = 0; index < totalNoOfTsk; index++)
                    {
                        var tmpExportTask = _queOfRptPDFExportTsk.Dequeue();
                        Logger.Info("Assign " + tmpExportTask.ID + " to Task " + index);
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
                        ReportPDFExportTask reportPDFExportTsk = (ReportPDFExportTask)tsk.AsyncState;

                        Logger.Debug(reportPDFExportTsk.ID + ": Tsk Status- " + tsk.Status);
                        Logger.Debug(reportPDFExportTsk.ID + " ReportPDFExportTsk Status- " + reportPDFExportTsk.Status);

                    }

                    listOfTask.Clear();
                }

                double currentMinutes = DateTime.Now.Minute + (DateTime.Now.Second / 60D);
                double pollInterval = _pollIntervals.Where(s => s > currentMinutes).DefaultIfEmpty(_pollIntervals.Min() + 60).Min();
                int sleepTime = Convert.ToInt32((pollInterval - currentMinutes) * 60);

                Logger.Info("ReportPDFExport Service Enqueuer sleeping until " + DateTime.Now.AddSeconds(sleepTime).ToString("hh:mm:ss tt"));
                Thread.Sleep(Convert.ToInt32(TimeSpan.FromSeconds(sleepTime).TotalMilliseconds));
            }
        }

        private void ProcessDBRecords(ReportPDFExportTask p_RptPDFExportTask, CancellationToken p_CToken)
        {
            try
            {
                if (p_CToken.IsCancellationRequested)
                {
                    Logger.Info("ID: " + p_RptPDFExportTask.ID + " is cancelled.");
                    p_CToken.ThrowIfCancellationRequested();
                }

                Logger.Info("ID: " + p_RptPDFExportTask.ID + " is processing.");

                p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.IN_PROCESS;
                UpdateTaskStatus(ReportPDFExportTask.TskStatus.IN_PROCESS.ToString(), p_RptPDFExportTask.ID);

                string DownloadPDFFileName = p_RptPDFExportTask.ReportTitle.Replace(" ", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("\\", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("/", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("*", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("?", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace(":", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("\"", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("<", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace(">", "_");
                DownloadPDFFileName = DownloadPDFFileName.Replace("|", "_");

                var rootPathLgc = (RootPathLogic)LogicFactory.GetLogic(LogicType.RootPath);
                var rootPath = rootPathLgc.GetRootPathByID(p_RptPDFExportTask.RootPathID);
                var subPath = "\\" + p_RptPDFExportTask.CreatedDate.Year + "\\" + p_RptPDFExportTask.CreatedDate.Month + "\\" + p_RptPDFExportTask.CreatedDate.Day + "\\" + DownloadPDFFileName + "_" + p_RptPDFExportTask.CreatedDate.ToString("yyyyMMddHHmmssfff") + CommonFunctions.GenerateRandomString(6) + ".pdf";
                var pdfPath = rootPath.StoragePath + subPath;

                if (p_CToken.IsCancellationRequested)
                {
                    Logger.Info("ID: " + p_RptPDFExportTask.ID + " is cancelled.");
                    p_CToken.ThrowIfCancellationRequested();
                }

                var destinationPath = Path.GetDirectoryName(pdfPath);

                try
                {
                    if (!Directory.Exists(destinationPath))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                }
                catch (Exception ex)
                {
                    p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.FAILED_DIRECTORY_CREATION;
                    Logger.Error("ID: " + p_RptPDFExportTask.ID + " Directory Creation failed." + destinationPath, ex);
                    UpdateTaskStatus(ReportPDFExportTask.TskStatus.FAILED_DIRECTORY_CREATION.ToString(), p_RptPDFExportTask.ID);
                    return;
                }

                string reportHTML = String.Empty;
                string tempHTMLPath = ConfigurationManager.AppSettings["DirReportExportHTML"] + p_RptPDFExportTask.HTMLFilename;
                try
                {
                    using (FileStream fs = new FileStream(tempHTMLPath, FileMode.Open))
                    {
                        using (var sr = new StreamReader(fs))
                        {
                            reportHTML = sr.ReadToEnd();
                        }
                    }
                }
                catch (Exception ex)
                {
                    p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.FAILED_READ_HTML;
                    Logger.Error("ID: " + p_RptPDFExportTask.ID + " HTML file read failed. Path: " + tempHTMLPath, ex);
                    UpdateTaskStatus(ReportPDFExportTask.TskStatus.FAILED_READ_HTML.ToString(), p_RptPDFExportTask.ID);
                    return;
                }

                try
                {
                    HtmlToPdf htmlToPdfConverter = new HtmlToPdf();
                    htmlToPdfConverter.SerialNumber = ConfigurationManager.AppSettings["HiQPdfSerialKey"];
                    htmlToPdfConverter.Document.Margins = new PdfMargins(20);
                    htmlToPdfConverter.BrowserWidth = 1000;
                    htmlToPdfConverter.ConvertHtmlToFile(reportHTML, p_RptPDFExportTask.BaseUrl, pdfPath);
                }
                catch (Exception ex)
                {
                    p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.FAILED_FILE_CREATION;
                    Logger.Error("ID: " + p_RptPDFExportTask.ID + " FileCreation failed.", ex);
                    UpdateTaskStatus(ReportPDFExportTask.TskStatus.FAILED_FILE_CREATION.ToString(), p_RptPDFExportTask.ID);
                    return;
                }

                try
                {
                    UpdateDownloadPath(p_RptPDFExportTask.ID, subPath);
                }
                catch (Exception ex)
                {
                    p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.FAILED_UPDATE_PATH;
                    Logger.Error("ID: " + p_RptPDFExportTask.ID + " Status update failed.", ex);
                    UpdateTaskStatus(ReportPDFExportTask.TskStatus.FAILED_UPDATE_PATH.ToString(), p_RptPDFExportTask.ID);
                    return;
                }

                try
                {
                    if (File.Exists(tempHTMLPath))
                    {
                        File.Delete(tempHTMLPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("ID: " + p_RptPDFExportTask.ID + " HTML delete failed.", ex);
                }

                p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.COMPLETED;
                UpdateTaskStatus(ReportPDFExportTask.TskStatus.COMPLETED.ToString(), p_RptPDFExportTask.ID);
            }
            catch (OperationCanceledException ex)
            {
                p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.FAILED_TSK_CANCELLED;
                Logger.Error("ID: " + p_RptPDFExportTask.ID + " task cancelled.", ex);
                UpdateTaskStatus(ReportPDFExportTask.TskStatus.FAILED_TSK_CANCELLED.ToString(), p_RptPDFExportTask.ID);

                throw ex;
            }
            catch (Exception ex)
            {
                p_RptPDFExportTask.Status = ReportPDFExportTask.TskStatus.FAILED;
                Logger.Error("ID: " + p_RptPDFExportTask.ID + " failed.", ex);
                UpdateTaskStatus(ReportPDFExportTask.TskStatus.FAILED.ToString(), p_RptPDFExportTask.ID);
            }    
        }

        private void GetRecordsFromDB()
        {
            try
            {
                var connStr = ConfigurationManager.ConnectionStrings["SqlServer"].ConnectionString;
                _queOfRptPDFExportTsk = new Queue<ReportPDFExportTask>();

                using (var conn = new SqlConnection(connStr))
                {
                    Logger.Debug("Fetching queued items from database.");
                    conn.Open();

                    var cmdStr = "usp_rptpdfexport_IQService_ReportPDFExport_SelectQueued";

                    using (var cmd = conn.GetCommand(cmdStr, CommandType.StoredProcedure))
                    {
                        cmd.Parameters.Add("@TopRows", SqlDbType.Int).Value = Config.ConfigSettings.Settings.QueueLimit;
                        cmd.Parameters.Add("@MachineName", SqlDbType.VarChar).Value = System.Environment.MachineName;

                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            _queOfRptPDFExportTsk.Enqueue(new ReportPDFExportTask(Convert.ToInt64(reader["ID"]),
                                                                                 new Guid(Convert.ToString(reader["CustomerGUID"])),
                                                                                 Convert.ToString(reader["BaseUrl"]),
                                                                                 Convert.ToString(reader["HTMLFilename"]),
                                                                                 Convert.ToInt32(reader["_RootPathID"]),
                                                                                 Convert.ToDateTime(reader["CreatedDate"]),
                                                                                 Convert.ToString(reader["Title"])
                                                                     )

                                             );
                        }

                        Logger.Info(_queOfRptPDFExportTsk.Count + " new items enqueued.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred while attempting to retrieve new tasks from the database.", ex);
            }
        }

        private void UpdateDownloadPath(Int64 ID, string path)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_rptpdfexport_IQService_ReportPDFExport_UpdateDownloadPath", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ID", ID);
                    cmd.Parameters.AddWithValue("@DownloadPath", path);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateTaskStatus(string p_Status, Int64 p_ID)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_rptpdfexport_IQService_ReportPDFExport_UpdateStatus", CommandType.StoredProcedure))
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
            ConfigurationManager.RefreshSection("ReportPDFExportSettings");

            //Test our config file to make sure we have everything we need...
            if (ConfigSettings.Settings == null)
                throw new XmlException("App.config is missing <ReportPDFExportSettings> node.");

            _pollIntervals = ConfigSettings.Settings.PollIntervals.Split(',').Select(s => Convert.ToDouble(s)).ToList();
        }

        /// <summary>
        /// Need to handle later all tasks, to either cancel or completed. 
        /// </summary>
        public void Quit()
        {
            Logger.Info("ReportPDFExport Service stopped at: " + DateTime.Now);
        }
    }
}
