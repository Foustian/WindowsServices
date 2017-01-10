using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace IQMedia.Service.DiscoveryArchiveMetaDataUpdate
{
    [RunInstaller(true)]
    public partial class DiscoveryArchiveMetaDataUpdateInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public DiscoveryArchiveMetaDataUpdateInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "Discovery -- Archive Metadata update.";
            _svcInstaller.DisplayName = "IQMedia Discovery Archive Metadata Update Service";
            _svcInstaller.ServiceName = "DiscoveryArchiveMetaDataUpdate";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
