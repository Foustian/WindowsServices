using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace IQMedia.Service.DiscoveryExport
{
    [RunInstaller(true)]
    public partial class DiscoveryExportInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public DiscoveryExportInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "DiscoveryExport -- Export data requested from Discovery.";
            _svcInstaller.DisplayName = "IQMedia Discovery Export Service";
            _svcInstaller.ServiceName = "DiscoveryExport";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
