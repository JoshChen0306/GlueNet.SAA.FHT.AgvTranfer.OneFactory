using NLog;
using ILogger = NLog.ILogger;

namespace SCP
{
    public static class LogMgt
    {
        public static ILogger Logger { get; set; }
        public static ILogger RcsLogger { get; set; }
        public static ILogger BridgeLogger { get; set; }

        static LogMgt()
        {
            var setupBuilder = LogManager.Setup();
            setupBuilder.LoadConfigurationFromFile("NLog.config");
            
            // 初始化 Logger
            Logger = LogManager.GetLogger("Logger");
        }
    }
}
