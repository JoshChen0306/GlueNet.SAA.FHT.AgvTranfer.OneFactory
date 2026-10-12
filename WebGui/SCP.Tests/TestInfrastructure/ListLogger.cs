using Microsoft.Extensions.Logging;

namespace SCP.Tests.TestInfrastructure
{
    /// <summary>收集 Log 的測試用 ILogger，可斷言是否寫出指定等級的訊息。</summary>
    public sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        public bool Has(LogLevel level, string contains)
            => Entries.Any(e => e.Level == level && e.Message.Contains(contains));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
