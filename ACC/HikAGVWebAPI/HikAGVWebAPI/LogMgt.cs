using NLog;

public static class LogMgt
{
    public static ILogger Logger { get; set; }

    static LogMgt()
    {
        var setupBuilder = LogManager.Setup();
        setupBuilder.LoadConfigurationFromFile("NLog.config");

        // 初始化 Logger
        Logger = LogManager.GetLogger("Logger");
    }
}