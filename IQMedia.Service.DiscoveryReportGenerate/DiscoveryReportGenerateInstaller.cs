using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace IQMedia.Service.DiscoveryReportGenerate
{
    [RunInstaller(true)]
    public partial class DiscoveryReportGenerateInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public DiscoveryReportGenerateInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "Discovery -- Insert Data into Archive tables.";
            _svcInstaller.DisplayName = "IQMedia Discovery Report Generator Service";
            _svcInstaller.ServiceName = "DiscoveryReportGenerate";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
