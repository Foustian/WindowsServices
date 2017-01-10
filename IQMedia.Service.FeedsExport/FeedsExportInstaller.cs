using System.ComponentModel;
using System.ServiceProcess;

namespace IQMedia.Service.FeedsExport
{
    [RunInstaller(true)]
    public partial class FeedsExportInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public FeedsExportInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "FeedsExport -- Export data requested from Feeds.";
            _svcInstaller.DisplayName = "IQMedia Feeds Export Service";
            _svcInstaller.ServiceName = "FeedsExport";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
