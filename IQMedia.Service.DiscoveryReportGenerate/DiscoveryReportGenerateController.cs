﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using IQMedia.Service.Common.Util;

namespace IQMedia.Service.DiscoveryReportGenerate
{
    static class DiscoveryReportGenerateController
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.CommandLine.ToLower().Contains("debug"))
            {
                Logger.Info("Starting Service in Debug...");
                using (var debugService = new DiscoveryReportGenerate())
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
                ServicesToRun = new ServiceBase[] { new DiscoveryReportGenerate() };
                ServiceBase.Run(ServicesToRun);
            }

        }
    }
}
