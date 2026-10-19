namespace SCP.Services
{
    /// <summary>
    /// 單條查詢通知訂閱：註冊 → 收到通知 → 重新註冊並推播事件給前端。
    /// SqlDependency 每次通知後即失效，必須重新註冊才能收到下一次。
    /// </summary>
    public sealed class QueryNotificationSubscription
    {
        private readonly string _sql;
        private readonly string _eventName;
        private readonly IQueryNotificationSource _source;
        private readonly IRealtimeNotifier _notifier;
        private readonly IRetryScheduler _scheduler;
        private readonly Serilog.ILogger _log;

        public QueryNotificationSubscription(string sql, string eventName, IQueryNotificationSource source,
            IRealtimeNotifier notifier, IRetryScheduler scheduler, Serilog.ILogger log)
        {
            _sql = sql;
            _eventName = eventName;
            _source = source;
            _notifier = notifier;
            _scheduler = scheduler;
            _log = log;
        }

        /// <summary>重註冊失敗的退避間隔：5s → 15s → 60s，之後每 5 分鐘，直到成功。</summary>
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5),
        };

        private int _failureCount;
        private volatile bool _stopped;

        public void Start() => Register();

        /// <summary>服務停止：之後到期的重試與通知都不再重新註冊。</summary>
        public void Stop() => _stopped = true;

        private void Register()
        {
            if (_stopped) return;

            try
            {
                _source.Subscribe(_sql, OnNotified);
            }
            catch (Exception ex)
            {
                // 單一查詢註冊失敗不應中斷其他通知；不放棄，依退避間隔重試直到成功（2026-07-16 oShuttle 訂閱死亡致畫面凍結）
                int attempt = Interlocked.Increment(ref _failureCount);
                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length) - 1];
                _log.Error(ex, "註冊 SqlDependency 失敗（連續第 {Attempt} 次），{DelaySeconds} 秒後重試，期間該即時更新需 F5（SQL: {Sql}）",
                    attempt, delay.TotalSeconds, _sql);
                _scheduler.Schedule(delay, Register);
                return;
            }

            int failures = Interlocked.Exchange(ref _failureCount, 0);
            if (failures > 0)
            {
                _log.Information("註冊 SqlDependency 已恢復（先前連續失敗 {Failures} 次），即時更新恢復正常（SQL: {Sql}）", failures, _sql);
            }
        }

        private async void OnNotified(QueryNotification notification)
        {
            Register();
            await _notifier.SendAsync(_eventName);
        }
    }
}
