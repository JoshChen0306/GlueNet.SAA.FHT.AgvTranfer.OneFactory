using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using SCP.Hubs;

namespace SCP.Services
{
    /// <summary>資料庫查詢通知（SqlNotificationEventArgs 的內容）。</summary>
    public sealed class QueryNotification
    {
        public QueryNotification(SqlNotificationType type, SqlNotificationInfo info, SqlNotificationSource source)
        {
            Type = type;
            Info = info;
            Source = source;
        }

        public SqlNotificationType Type { get; }
        public SqlNotificationInfo Info { get; }
        public SqlNotificationSource Source { get; }
    }

    /// <summary>註冊一次性的資料庫查詢通知；註冊失敗丟例外。正式實作為 SqlDependencyNotificationSource。</summary>
    public interface IQueryNotificationSource
    {
        void Subscribe(string sql, Action<QueryNotification> onNotified);
    }

    /// <summary>即時推播給前端；正式實作為 HubRealtimeNotifier（SignalR）。</summary>
    public interface IRealtimeNotifier
    {
        Task SendAsync(string eventName);
    }

    /// <summary>延遲執行（重註冊失敗的退避重試用）；正式實作為 TaskRetryScheduler。</summary>
    public interface IRetryScheduler
    {
        void Schedule(TimeSpan delay, Action action);
    }

    /// <summary>以 Task.Delay 延遲執行；Cancel 後尚未到期的動作不再執行。</summary>
    public sealed class TaskRetryScheduler : IRetryScheduler
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Serilog.ILogger _log;

        public TaskRetryScheduler(Serilog.ILogger log)
        {
            _log = log;
        }

        public void Schedule(TimeSpan delay, Action action)
        {
            var token = _cts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, token);
                    action();
                }
                catch (OperationCanceledException)
                {
                    // 服務停止，取消尚未到期的重試（預期行為）
                    _log.Debug("服務停止，取消即時更新重註冊排程");
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "即時更新重註冊排程執行失敗");
                }
            });
        }

        public void Cancel() => _cts.Cancel();
    }

    /// <summary>以 SqlDependency 註冊查詢通知（需資料庫啟用 Service Broker）。</summary>
    public sealed class SqlDependencyNotificationSource : IQueryNotificationSource
    {
        private readonly string _connectionString;
        private List<SqlDependency> _dependency = new List<SqlDependency>();

        public SqlDependencyNotificationSource(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Subscribe(string sql, Action<QueryNotification> onNotified)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(sql, connection))
                {
                    var dependency = new SqlDependency(command);
                    dependency.OnChange += (sender, e) => onNotified(new QueryNotification(e.Type, e.Info, e.Source));
                    _dependency.Add(dependency);

                    // 執行命令以註冊 SqlDependency 物件
                    command.ExecuteReader();
                }
            }
        }
    }

    /// <summary>以 SignalR CommonHub 廣播給所有前端。</summary>
    public sealed class HubRealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<CommonHub> _hub;

        public HubRealtimeNotifier(IHubContext<CommonHub> hub)
        {
            _hub = hub;
        }

        public Task SendAsync(string eventName) => _hub.Clients.All.SendAsync(eventName);
    }
}
