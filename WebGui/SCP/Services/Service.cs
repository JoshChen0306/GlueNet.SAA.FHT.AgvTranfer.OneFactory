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

        private readonly string? _connectionString;
        /// <summary>SqlDependency.Start 成功才需要 Stop（連線字串缺失或 Start 失敗時呼叫 Stop 只會留下誤導的 log）。</summary>
        private bool _listenerStarted;
        private readonly List<QueryNotificationSubscription> _subscriptions;
        private readonly IQueryNotificationListener _listener;
        private readonly IRetryScheduler _retryScheduler;
        private readonly Serilog.ILogger _log;

        /// <summary>保護 _listenerStarted 與接收端 Start/Stop（重建排程與 StopAsync 可能並行）。</summary>
        private readonly object _listenerLock = new object();
        /// <summary>1＝已有排程中的接收端重建（6 條訂閱同時回報中斷時只重建一次）。</summary>
        private int _restartPending;
        private int _restartFailures;
        private volatile bool _stopped;

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
                .Select(d => new QueryNotificationSubscription(d.Sql, d.EventName, source, notifier, retryScheduler, log, RequestListenerRestart))
                .ToList();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                // 與「broker 未啟用」分開記錄，避免現場往資料庫設定方向誤查
                _log.Error("即時更新未啟動：appsettings 缺少連線字串 ConnectionStrings:DBContext，前端需手動 F5");
                return Task.CompletedTask;
            }

            try
            {
                // SqlDependency 需要資料庫啟用 Service Broker，且連線帳號具備
                // SUBSCRIBE QUERY NOTIFICATIONS 權限。任一未滿足，Start 會丟例外，
                // 導致整套 SignalR 即時推播（任務列表/AGV/地圖）失效、客戶端需手動 F5。
                lock (_listenerLock)
                {
                    _listener.Start(_connectionString);
                    _listenerStarted = true;
                }
                _log.Information("SqlDependency.Start 成功，開始註冊查詢通知");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "SqlDependency.Start 失敗：請確認資料庫已啟用 Service Broker"
                    + "（ALTER DATABASE [db] SET ENABLE_BROKER）且帳號具 SUBSCRIBE QUERY NOTIFICATIONS 權限。"
                    + "即時更新暫時失效（前端需手動 F5），將依退避間隔重試，啟用後自動恢復。");
                // 不放棄：Broker 之後啟用即可自動恢復，不需重啟服務
                RequestListenerRestart();
                return Task.CompletedTask;
            }

            foreach (var subscription in _subscriptions)
            {
                subscription.Start();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 接收端中斷或啟動失敗時排程重建（多條訂閱同時回報只排一次）。
        /// 2026-10-19 本機實測：Broker 中斷後 SqlDependency 接收端壞掉，之後重新註冊不報錯但通知永遠送不到，
        /// 必須 SqlDependency.Stop + Start 重建接收端再重新註冊。
        /// </summary>
        private void RequestListenerRestart()
        {
            if (_stopped) return;
            if (Interlocked.CompareExchange(ref _restartPending, 1, 0) != 0) return;
            ScheduleListenerRestart();
        }

        private void ScheduleListenerRestart()
        {
            var delay = RetryBackoff.Delay(Volatile.Read(ref _restartFailures) + 1);
            _log.Warning("即時更新接收端將於 {DelaySeconds} 秒後重建", delay.TotalSeconds);
            _retryScheduler.Schedule(delay, RestartListener);
        }

        private void RestartListener()
        {
            if (_stopped) return;

            try
            {
                lock (_listenerLock)
                {
                    if (_stopped) return;
                    if (_listenerStarted)
                    {
                        try
                        {
                            _listener.Stop(_connectionString!);
                        }
                        catch (Exception ex)
                        {
                            // 舊接收端已壞，Stop 失敗不影響重建
                            _log.Warning(ex, "重建前 SqlDependency.Stop 失敗，繼續重建");
                        }
                        _listenerStarted = false;
                    }
                    _listener.Start(_connectionString!);
                    _listenerStarted = true;
                }
            }
            catch (Exception ex)
            {
                int failures = Interlocked.Increment(ref _restartFailures);
                _log.Error(ex, "重建即時更新接收端失敗（連續第 {Attempt} 次），請確認 Service Broker 已啟用", failures);
                ScheduleListenerRestart();
                return;
            }

            int previousFailures = Interlocked.Exchange(ref _restartFailures, 0);
            Interlocked.Exchange(ref _restartPending, 0);
            _log.Information("即時更新接收端已重建（先前失敗 {Failures} 次），重新註冊 {Count} 條查詢通知", previousFailures, _subscriptions.Count);
            foreach (var subscription in _subscriptions)
            {
                subscription.Resubscribe();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // 先停掉重註冊與重建排程，避免停止期間又去註冊
            _stopped = true;
            foreach (var subscription in _subscriptions)
            {
                subscription.Stop();
            }
            _retryScheduler.Cancel();

            lock (_listenerLock)
            {
                if (!_listenerStarted)
                {
                    return Task.CompletedTask;
                }

                try
                {
                    _listener.Stop(_connectionString!);
                }
                catch (Exception ex)
                {
                    // 與 Start 對稱：停止失敗不外拋，避免主機關閉流程中斷
                    _log.Warning(ex, "SqlDependency.Stop 失敗（服務停止中，可忽略；若反覆發生請檢查資料庫連線）");
                }
                _listenerStarted = false;
            }
            return Task.CompletedTask;
        }
    }
}
