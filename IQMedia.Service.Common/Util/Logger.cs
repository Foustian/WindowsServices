using System;
using System.Text;
using System.Threading;
using log4net;

namespace IQMedia.Service.Common.Util
{
    public static class Logger
    {
        private readonly static ILog Log;

		static Logger() {
		    //Load the log4net Configuration from the config file
            log4net.Config.XmlConfigurator.Configure();
            
            //Set some parameters we can use in the logger...
		    GlobalContext.Properties["system"] = Environment.MachineName;
            
            //Instantiate the log manager
            Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		}

		public static void Info(string message) 
        {
			if (Log.IsInfoEnabled) Log.Info(message);
		}

		public static void Debug(string message)
		{
			if (Log.IsDebugEnabled) Log.Debug(message);
		}

        public static void Warning(Exception ex)
        {
            Warning(null, ex);
        }
        
        public static void Warning(string message, Exception ex = null)
		{
            if (Log.IsWarnEnabled) Log.Warn(message, ex);
		}

        public static void Error(Exception ex)
		{
			Error(null, ex);
		}

        public static void Error(string message, Exception ex = null)
        {
            if (Log.IsErrorEnabled) Log.Error(message, ex);
        }

        public static void Fatal(Exception ex)
        {
            Fatal(null, ex);
        }

        public static void Fatal(string message, Exception ex = null)
        {
            if (Log.IsFatalEnabled) Log.Fatal(message, ex);
        }

        #region Helper Functions

        private static string CreateLoggerMesssage(string message, Exception ex)
        {
            var logMsg = message;
            if (ex != null)
            {
                if (!String.IsNullOrEmpty(logMsg)) logMsg += Environment.NewLine;
                logMsg += CreateExceptionMessage(ex);
            }

            return logMsg;
        }

		private static string CreateExceptionMessage(Exception ex)
		{
			if (ex is ThreadAbortException)
				return "Thread aborted for Project: " + Thread.CurrentThread.Name;

			var buffer = new StringBuilder();
			buffer.Append(GetExceptionAlertMessage(ex));
			buffer.Append(ex.Message).Append(Environment.NewLine);
			buffer.Append("----------").Append(Environment.NewLine);
			buffer.Append(ex.ToString()).Append(Environment.NewLine);
			buffer.Append("----------").Append(Environment.NewLine);
			return buffer.ToString();
		}

		private static string GetExceptionAlertMessage(Exception ex)
		{
			return (ex is ApplicationException) ? "Exception: " : "INTERNAL ERROR: ";
        }

        #endregion
    }
}
