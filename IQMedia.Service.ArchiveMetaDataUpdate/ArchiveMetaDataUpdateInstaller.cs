using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace IQMedia.Service.ArchiveMetaDataUpdate
{
    [RunInstaller(true)]
    public partial class ArchiveMetaDataUpdateInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public ArchiveMetaDataUpdateInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "Archive Metadata Update -- Update Highlighted/Closed Caption.";
            _svcInstaller.DisplayName = "IQMedia Archive Metadata Update Service";
            _svcInstaller.ServiceName = "ArchiveMetadataUpdate";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
