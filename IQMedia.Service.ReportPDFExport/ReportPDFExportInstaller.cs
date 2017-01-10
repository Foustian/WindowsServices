using System.ComponentModel;
using System.ServiceProcess;

namespace IQMedia.Service.ReportPDFExport
{
    [RunInstaller(true)]
    public partial class ReportPDFExportInstaller : System.Configuration.Install.Installer
    {
        private readonly ServiceProcessInstaller _processInstaller;
        private readonly ServiceInstaller _svcInstaller;

        public ReportPDFExportInstaller()
        {
            InitializeComponent();

            _processInstaller = new ServiceProcessInstaller();
            _svcInstaller = new ServiceInstaller();

            _processInstaller.Account = ServiceAccount.LocalService;

            _svcInstaller.StartType = ServiceStartMode.Manual;
            _svcInstaller.Description = "ReportPDFExport -- Export report as a PDF.";
            _svcInstaller.DisplayName = "IQMedia Report PDF Export Service";
            _svcInstaller.ServiceName = "ReportPDFExport";

            Installers.Add(_svcInstaller);
            Installers.Add(_processInstaller);
        }
    }
}
