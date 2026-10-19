using Microsoft.Data.SqlClient;

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
        /// <summary>連續收到非 Change 通知的次數；收到 Change（訂閱恢復正常）才歸零，避免無效查詢每 5 秒重試一次洗 log。</summary>
        private int _nonChangeCount;
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

        /// <summary>
        /// SqlDependency 通知進入點（async void）：任何例外都在此接住，否則會在執行緒集區重新拋出、直接結束 SCP 行程。
        /// </summary>
        private async void OnNotified(QueryNotification notification)
        {
            try
            {
                await HandleNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "即時更新通知處理失敗（事件 {EventName}，SQL: {Sql}），前端此次需 F5，下次變更會再推播", _eventName, _sql);
            }
        }

        /// <summary>真正的資料變更（Type=Change 且 Info 為以下之一）才立即重註冊。</summary>
        private static readonly HashSet<SqlNotificationInfo> DataChangeInfos = new HashSet<SqlNotificationInfo>
        {
            SqlNotificationInfo.Insert, SqlNotificationInfo.Update, SqlNotificationInfo.Delete,
            SqlNotificationInfo.Truncate, SqlNotificationInfo.Merge,
        };

        private async Task HandleNotificationAsync(QueryNotification notification)
        {
            if (_stopped) return;

            bool isDataChange = notification.Type == SqlNotificationType.Change && DataChangeInfos.Contains(notification.Info);
            if (!isDataChange)
            {
                // 非資料變更：Subscribe/Invalid（訂閱本身失敗），或 Change 但 Info=Error/Restart 等（連線中斷、逾時、伺服器重啟）。
                // 立即重註冊可能立刻收到同樣通知 → 緊密迴圈；也不可放棄（訂閱會永久死亡），改依退避間隔重註冊。
                int attempt = Interlocked.Increment(ref _nonChangeCount);
                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length) - 1];
                _log.Warning("收到非資料變更通知（Type={Type}, Info={Info}, Source={Source}，連續第 {Attempt} 次），{DelaySeconds} 秒後重新註冊（SQL: {Sql}）",
                    notification.Type, notification.Info, notification.Source, attempt, delay.TotalSeconds, _sql);
                _scheduler.Schedule(delay, Register);

                // Type=Change 的異常（斷線、重啟）期間資料可能已變，推播一次讓前端刷新；Subscribe 類資料沒變，不推播
                if (notification.Type == SqlNotificationType.Change)
                {
                    await _notifier.SendAsync(_eventName);
                }
                return;
            }

            Interlocked.Exchange(ref _nonChangeCount, 0);
            Register();
            await _notifier.SendAsync(_eventName);
        }
    }
}
