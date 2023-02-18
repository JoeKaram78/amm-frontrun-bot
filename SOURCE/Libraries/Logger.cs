using System;
using System.Globalization;
using log4net;

namespace SmartCryptoBot.Libraries {

    public interface ILogger {
        void LogException(Exception exception);
        void LogError(string message);
        void LogWarningMessage(string message);
        void LogInfoMessage(string message);
    }

    public class Logger : ILogger {

        private static ILog log = null;

        static Logger() {
            //var log4NetConfigDirectory = 
            //AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
            //var log4NetConfigFilePath = Path.Combine(log4NetConfigDirectory, "log4net.config");
            //log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(log4NetConfigFilePath));
            log = LogManager.GetLogger(typeof(Logger));
            log4net.GlobalContext.Properties["host"] = Environment.MachineName;
        }

        public Logger(Type logClass) {
            log = LogManager.GetLogger(logClass);
        }

        #region ILogger Members
        public void LogException(Exception exception) {
            if (log.IsErrorEnabled)
                log.Error(string.Format(CultureInfo.InvariantCulture, "{0}", exception.Message), exception);
        }

        public void LogError(string message) {
            if (log.IsErrorEnabled)
                log.Error(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }

        public void LogWarningMessage(string message) {
            if (log.IsWarnEnabled)
                log.Warn(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }

        public void LogInfoMessage(string message) {
            if (log.IsInfoEnabled)
                log.Info(string.Format(CultureInfo.InvariantCulture, "{0}", message));
        }
        #endregion

    }
}
