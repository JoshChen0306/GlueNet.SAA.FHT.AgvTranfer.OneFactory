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
        private readonly Serilog.ILogger _log;

        public QueryNotificationSubscription(string sql, string eventName, IQueryNotificationSource source,
            IRealtimeNotifier notifier, Serilog.ILogger log)
        {
            _sql = sql;
            _eventName = eventName;
            _source = source;
            _notifier = notifier;
            _log = log;
        }

        public void Start() => Register();

        private void Register()
        {
            try
            {
                _source.Subscribe(_sql, OnNotified);
            }
            catch (Exception ex)
            {
                // 單一查詢註冊失敗不應中斷其他通知；記錄後讓對應功能退化為需 F5。
                _log.Error(ex, "註冊 SqlDependency 失敗，該即時更新將失效（SQL: {Sql}）", _sql);
            }
        }

        private async void OnNotified(QueryNotification notification)
        {
            Register();
            await _notifier.SendAsync(_eventName);
        }
    }
}
