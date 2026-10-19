using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using SCP.Hubs;
using Serilog;

namespace SCP.Services
{
    public class Service : IHostedService
    {
        /// <summary>6 條即時更新訂閱：查詢欄位變動時推播對應事件（前端 connection.on 依賴事件名稱，不可改）。</summary>
        public static readonly IReadOnlyList<(string Sql, string EventName)> Definitions = new List<(string, string)>
        {
            ("select HaveFlag,BgnToEnd from dbo.oPort", "SendTracChange"),
            ("select PosX,PosY from dbo.oShuttle", "SendAgvChange"),
            ("select Battery,Status,LastStation,BeginStation,EndStation from dbo.oShuttle", "SendAgvStatusChange"),
            ("select TaskDateTime from dbo.ubMission", "SendTotalTaskChange"),
            ("select AssignFlag,OkFlag from dbo.oRequire", "SendDispatchChange"),
            ("select OkFlag from dbo.oMission", "SendMissionChange"),
        };

        private string _connectionString;
        private readonly List<QueryNotificationSubscription> _subscriptions;
        private readonly IQueryNotificationListener _listener;
        private readonly IRetryScheduler _retryScheduler;
        private readonly Serilog.ILogger _log;

        public Service(IConfiguration configuration, IHubContext<CommonHub> hub)
            : this(configuration.GetConnectionString("DBContext"), hub)
        {
        }

        private Service(string connectionString, IHubContext<CommonHub> hub)
            : this(connectionString, new SqlDependencyNotificationSource(connectionString), new HubRealtimeNotifier(hub),
                   new SqlDependencyListener(), new TaskRetryScheduler(Log.Logger), Log.Logger)
        {
        }

        /// <summary>供測試注入假的通知來源／推播／監聽／排程（DI 只會使用上方的建構子）。</summary>
        public Service(string? connectionString, IQueryNotificationSource source, IRealtimeNotifier notifier,
            IQueryNotificationListener listener, IRetryScheduler retryScheduler, Serilog.ILogger log)
        {
            _connectionString = connectionString;
            _listener = listener;
            _retryScheduler = retryScheduler;
            _log = log;
            _subscriptions = Definitions
                .Select(d => new QueryNotificationSubscription(d.Sql, d.EventName, source, notifier, retryScheduler, log))
                .ToList();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // SqlDependency 需要資料庫啟用 Service Broker，且連線帳號具備
                // SUBSCRIBE QUERY NOTIFICATIONS 權限。任一未滿足，Start 會丟例外，
                // 導致整套 SignalR 即時推播（任務列表/AGV/地圖）失效、客戶端需手動 F5。
                _listener.Start(_connectionString);
                _log.Information("SqlDependency.Start 成功，開始註冊查詢通知");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "SqlDependency.Start 失敗：請確認資料庫已啟用 Service Broker"
                    + "（ALTER DATABASE [db] SET ENABLE_BROKER）且帳號具 SUBSCRIBE QUERY NOTIFICATIONS 權限。"
                    + "即時更新將失效，前端需手動 F5。");
                return Task.CompletedTask;
            }

            foreach (var subscription in _subscriptions)
            {
                subscription.Start();
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // 先停掉重註冊排程，避免停止期間又去註冊
            foreach (var subscription in _subscriptions)
            {
                subscription.Stop();
            }
            _retryScheduler.Cancel();

            _listener.Stop(_connectionString);
            return Task.CompletedTask;
        }
    }
}
