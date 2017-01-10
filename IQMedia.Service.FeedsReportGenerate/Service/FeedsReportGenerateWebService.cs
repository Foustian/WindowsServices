using System;
using IQMedia.Service.Common.Util;
using IQMedia.Service.Logic;

namespace IQMedia.Service.FeedsReportGenerate.Service
{
    public class FeedsReportGenerateWeb : IFeedsReportGenerate
    {
        /*public bool EnqueueTask(Guid clipGuid, string outputExt, string outputPath = null, string outputDimensions = null)
        {
            var svcLgc = (FeedReportLogic)LogicFactory.GetLogic(LogicType.FeedsReport);
            return svcLgc.EnqueueClipForExport(clipGuid, outputExt, outputPath, outputDimensions);
        }*/

        /*public void WakeupService()
        {
            //Forcefully tell export to run.
            Logger.Info("FeedsReportGenerate kicked off by WCF Service.");
            FeedsReportGenerate.Instance.EnqueueTasks();
        }*/
    }
}
