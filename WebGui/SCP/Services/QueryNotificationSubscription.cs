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
        private readonly Action? _onListenerError;

        /// <param name="onListenerError">
        /// 接收端中斷（Type=Change, Info=Error, Source=Client）時呼叫，由 Service 重建 SqlDependency 接收端後統一重新註冊；
        /// 未提供時改由本訂閱自行退避重註冊。
        /// </param>
        public QueryNotificationSubscription(string sql, string eventName, IQueryNotificationSource source,
            IRealtimeNotifier notifier, IRetryScheduler scheduler, Serilog.ILogger log, Action? onListenerError = null)
        {
            _sql = sql;
            _eventName = eventName;
            _source = source;
            _notifier = notifier;
            _scheduler = scheduler;
            _log = log;
            _onListenerError = onListenerError;
        }

        private int _failureCount;
        /// <summary>連續收到非 Change 通知的次數；收到 Change（訂閱恢復正常）才歸零，避免無效查詢每 5 秒重試一次洗 log。</summary>
        private int _nonChangeCount;
        /// <summary>
        /// 註冊世代：每次註冊遞增，通知與重試都帶著註冊當時的世代；世代過期者忽略，
        /// 確保同一條查詢同時只有一個有效訂閱（重新註冊後舊訂閱才觸發，不會再推播或多註冊一條）。
        /// </summary>
        private int _generation;
        private volatile bool _stopped;

        /// <summary>真正的資料變更（Type=Change 且 Info 為以下之一）才立即重註冊。</summary>
        private static readonly HashSet<SqlNotificationInfo> DataChangeInfos = new HashSet<SqlNotificationInfo>
        {
            SqlNotificationInfo.Insert, SqlNotificationInfo.Update, SqlNotificationInfo.Delete,
            SqlNotificationInfo.Truncate, SqlNotificationInfo.Merge,
        };

        public void Start() => Register();

        /// <summary>接收端重建後重新註冊；先前的訂閱與尚未到期的重試全部作廢。</summary>
        public void Resubscribe() => Register();

        /// <summary>服務停止：之後到期的重試與通知都不再重新註冊。</summary>
        public void Stop() => _stopped = true;

        private bool IsCurrent(int generation) => generation == Volatile.Read(ref _generation);

        private void Register()
        {
            if (_stopped) return;

            int generation = Interlocked.Increment(ref _generation);
            try
            {
                _source.Subscribe(_sql, notification => OnNotified(generation, notification));
            }
            catch (Exception ex)
            {
                // 單一查詢註冊失敗不應中斷其他通知；不放棄，依退避間隔重試直到成功（2026-07-16 oShuttle 訂閱死亡致畫面凍結）
                int attempt = Interlocked.Increment(ref _failureCount);
                var delay = RetryBackoff.Delay(attempt);
                _log.Error(ex, "註冊 SqlDependency 失敗（連續第 {Attempt} 次），{DelaySeconds} 秒後重試，期間該即時更新需 F5（SQL: {Sql}）",
                    attempt, delay.TotalSeconds, _sql);
                _scheduler.Schedule(delay, () => RetryIfCurrent(generation));
                return;
            }

            int failures = Interlocked.Exchange(ref _failureCount, 0);
            if (failures > 0)
            {
                _log.Information("註冊 SqlDependency 已恢復（先前連續失敗 {Failures} 次），即時更新恢復正常（SQL: {Sql}）", failures, _sql);
            }
        }

        private void RetryIfCurrent(int generation)
        {
            if (IsCurrent(generation)) Register();
        }

        /// <summary>
        /// SqlDependency 通知進入點（async void）：任何例外都在此接住，否則會在執行緒集區重新拋出、直接結束 SCP 行程。
        /// </summary>
        private async void OnNotified(int generation, QueryNotification notification)
        {
            try
            {
                await HandleNotificationAsync(generation, notification);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "即時更新通知處理失敗（事件 {EventName}，SQL: {Sql}），前端此次需 F5，下次變更會再推播", _eventName, _sql);
            }
        }

        private async Task HandleNotificationAsync(int generation, QueryNotification notification)
        {
            if (_stopped) return;
            if (!IsCurrent(generation)) return;   // 已重新註冊，舊訂閱的通知忽略

            bool isDataChange = notification.Type == SqlNotificationType.Change && DataChangeInfos.Contains(notification.Info);
            if (!isDataChange)
            {
                bool listenerError = notification.Type == SqlNotificationType.Change
                    && notification.Info == SqlNotificationInfo.Error
                    && notification.Source == SqlNotificationSource.Client;

                if (listenerError && _onListenerError != null)
                {
                    // 接收端中斷（2026-10-19 本機實測：停用 Broker 時 6 條都收到 Change/Error/Client，之後重新註冊看似成功但通知永遠送不到）
                    // → 交給 Service 重建接收端後統一重新註冊
                    _log.Warning("SqlDependency 接收端中斷（Type={Type}, Info={Info}, Source={Source}），交由服務重建（SQL: {Sql}）",
                        notification.Type, notification.Info, notification.Source, _sql);
                    _onListenerError();
                }
                else
                {
                    // 非資料變更：Subscribe/Invalid（訂閱本身失敗），或 Change 但 Info=Restart 等。
                    // 立即重註冊可能立刻收到同樣通知 → 緊密迴圈；也不可放棄（訂閱會永久死亡），改依退避間隔重註冊。
                    int attempt = Interlocked.Increment(ref _nonChangeCount);
                    var delay = RetryBackoff.Delay(attempt);
                    _log.Warning("收到非資料變更通知（Type={Type}, Info={Info}, Source={Source}，連續第 {Attempt} 次），{DelaySeconds} 秒後重新註冊（SQL: {Sql}）",
                        notification.Type, notification.Info, notification.Source, attempt, delay.TotalSeconds, _sql);
                    _scheduler.Schedule(delay, () => RetryIfCurrent(generation));
                }

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

    /// <summary>重試退避間隔：5s → 15s → 60s，之後每 5 分鐘，直到成功。</summary>
    public static class RetryBackoff
    {
        private static readonly TimeSpan[] Delays =
        {
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5),
        };

        /// <summary>第 attempt 次（從 1 起算）失敗後的等待時間。</summary>
        public static TimeSpan Delay(int attempt) => Delays[Math.Clamp(attempt, 1, Delays.Length) - 1];
    }
}
