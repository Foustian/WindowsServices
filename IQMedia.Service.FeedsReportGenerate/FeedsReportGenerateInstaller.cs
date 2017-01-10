using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace IQMedia.Service.FeedsReportGenerate
{
    [RunInstaller(true)]
    public partial class FeedsReportGenerateInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public FeedsReportGenerateInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "Feeds -- Insert Data into Archive tables.";
            _svcInstaller.DisplayName = "IQMedia Feeds Report Generator Service";
            _svcInstaller.ServiceName = "FeedsReportGenerate";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
