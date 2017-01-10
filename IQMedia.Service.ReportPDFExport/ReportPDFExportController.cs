using System;
using System.ServiceProcess;
using IQMedia.Service.Common.Util;

namespace IQMedia.Service.ReportPDFExport
{
    static class ReportPDFExportController
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.CommandLine.ToLower().Contains("debug"))
            {
                Logger.Info("Starting Service in Debug...");
                using (var debugService = new ReportPDFExport())
                {
                    debugService.Run();
                    Logger.Info("Service started. Press 'Enter' to exit.");
                    Console.ReadLine();
                    debugService.Quit();
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new ReportPDFExport() };
                ServiceBase.Run(ServicesToRun);
            }

        }
    }
}
