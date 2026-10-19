using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SCP.Tests.Services
{
    /// <summary>
    /// SCP 即時更新 Service 硬化（ToDo/20260605_SCP即時更新Service硬化）
    /// T0：抽出 QueryNotificationSubscription 後，先鎖住 Service.cs 原本的行為（純結構重構，0 邏輯變更）。
    /// 以假的通知來源／推播取代 SqlDependency 與 SignalR，不需連資料庫。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("RealtimeHardening")]
    public class QueryNotificationSubscriptionTests
    {
        private const string Sql = "select HaveFlag,BgnToEnd from dbo.oPort";
        private const string EventName = "SendTracChange";

        internal sealed class FakeSource : IQueryNotificationSource
        {
            public int SubscribeCount { get; private set; }
            public Action<QueryNotification>? LastCallback { get; private set; }
            public string? LastSql { get; private set; }
            /// <summary>依序決定每次 Subscribe 是否丟例外（未列到的次數視為成功）。</summary>
            public Queue<bool> FailPlan { get; } = new Queue<bool>();

            public void Subscribe(string sql, Action<QueryNotification> onNotified)
            {
                SubscribeCount++;
                LastSql = sql;
                LastCallback = onNotified;
                if (FailPlan.Count > 0 && FailPlan.Dequeue())
                {
                    throw new InvalidOperationException("模擬註冊失敗");
                }
            }

            public void Notify(SqlNotificationType type, SqlNotificationInfo info = SqlNotificationInfo.Update)
                => LastCallback!(new QueryNotification(type, info, SqlNotificationSource.Data));
        }

        internal sealed class FakeNotifier : IRealtimeNotifier
        {
            public List<string> Sent { get; } = new List<string>();
            /// <summary>true 時模擬 SignalR 推播失敗（回傳失敗的 Task）。</summary>
            public bool Fail { get; set; }

            public Task SendAsync(string eventName)
            {
                Sent.Add(eventName);
                return Fail ? Task.FromException(new InvalidOperationException("模擬推播失敗")) : Task.CompletedTask;
            }
        }

        internal sealed class ListSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();
            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
            public bool Has(LogEventLevel level, string text)
                => Events.Any(e => e.Level == level && e.RenderMessage().Contains(text));
        }

        /// <summary>記錄排程，由測試手動執行到期的動作（不真的等待）。</summary>
        internal sealed class FakeScheduler : IRetryScheduler
        {
            public List<(TimeSpan Delay, Action Action)> Scheduled { get; } = new List<(TimeSpan, Action)>();

            public void Schedule(TimeSpan delay, Action action) => Scheduled.Add((delay, action));

            public bool Cancelled { get; private set; }

            public void Cancel() => Cancelled = true;

            /// <summary>執行最後一筆排程並移除。</summary>
            public void RunLast()
            {
                var last = Scheduled[^1];
                Scheduled.RemoveAt(Scheduled.Count - 1);
                last.Action();
            }
        }

        private static (QueryNotificationSubscription Sut, FakeSource Source, FakeNotifier Notifier, ListSink Log) Create()
            => Create(out _);

        private static (QueryNotificationSubscription Sut, FakeSource Source, FakeNotifier Notifier, ListSink Log) Create(out FakeScheduler scheduler)
        {
            var source = new FakeSource();
            var notifier = new FakeNotifier();
            var sink = new ListSink();
            scheduler = new FakeScheduler();
            var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            return (new QueryNotificationSubscription(Sql, EventName, source, notifier, scheduler, logger), source, notifier, sink);
        }

        // ── T3 推播例外不可打掛行程 ──────────────────────────────

        [TestMethod]
        public void OnNotified_SendThrows_LogsWarningAndKeepsSubscription()
        {
            // 修正前：async void 內的例外會在執行緒集區重新拋出，直接結束整個行程
            var (sut, source, notifier, log) = Create();
            sut.Start();
            notifier.Fail = true;

            source.Notify(SqlNotificationType.Change);

            Assert.IsTrue(log.Has(LogEventLevel.Warning, EventName));
            Assert.AreEqual(2, source.SubscribeCount, "推播失敗前已重新註冊，下一次變更仍收得到");

            notifier.Fail = false;
            source.Notify(SqlNotificationType.Change);
            Assert.AreEqual(2, notifier.Sent.Count);
        }

        // ── T1 非 Change 通知不立即重註冊（擋無限迴圈），交給退避重試 ─────

        [TestMethod]
        public void OnNotified_SubscribeType_DoesNotResubscribeImmediatelyNorSend()
        {
            // 訂閱本身失敗（Type=Subscribe，例：查詢不符合通知規則）時立即重註冊會立刻再收到同樣通知 → 緊密迴圈
            var (sut, source, notifier, log) = Create(out var scheduler);
            sut.Start();

            source.Notify(SqlNotificationType.Subscribe, SqlNotificationInfo.Invalid);

            Assert.AreEqual(1, source.SubscribeCount, "不可立即重註冊");
            Assert.AreEqual(0, notifier.Sent.Count, "資料沒有變更，不推播");
            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled.Single().Delay, "改走退避重試，不可永久放棄");
            Assert.IsTrue(log.Has(LogEventLevel.Warning, "Invalid"));
        }

        [TestMethod]
        public void OnNotified_RepeatedNonChange_BackoffGrows()
        {
            var (sut, source, _, _) = Create(out var scheduler);
            sut.Start();
            var delays = new List<TimeSpan>();

            for (int i = 0; i < 4; i++)
            {
                source.Notify(SqlNotificationType.Subscribe, SqlNotificationInfo.Invalid);
                delays.Add(scheduler.Scheduled[^1].Delay);
                scheduler.RunLast();   // 重註冊（成功），又立刻收到同樣的無效通知
            }

            CollectionAssert.AreEqual(
                new[] { 5, 15, 60, 300 }.Select(s => TimeSpan.FromSeconds(s)).ToArray(),
                delays.ToArray());
        }

        [TestMethod]
        public void OnNotified_ChangeAfterNonChange_ResetsBackoff()
        {
            var (sut, source, _, _) = Create(out var scheduler);
            sut.Start();
            source.Notify(SqlNotificationType.Subscribe, SqlNotificationInfo.Invalid);
            scheduler.RunLast();
            source.Notify(SqlNotificationType.Subscribe, SqlNotificationInfo.Invalid);
            scheduler.RunLast();

            source.Notify(SqlNotificationType.Change);                                   // 恢復正常
            source.Notify(SqlNotificationType.Subscribe, SqlNotificationInfo.Invalid);  // 之後再出錯

            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled.Single().Delay);
        }

        [TestMethod]
        public void OnNotified_ChangeWithErrorInfo_SendsOnceButResubscribesViaBackoff()
        {
            // code review W1：連線中斷／逾時／伺服器重啟時 SqlClient 送 Type=Change、Info=Error/Restart，
            // 不是資料變更；立即重註冊可能立刻又收到同樣通知（緊密迴圈）。斷線期間資料可能已變，仍推播一次讓前端刷新
            var (sut, source, notifier, log) = Create(out var scheduler);
            sut.Start();

            source.Notify(SqlNotificationType.Change, SqlNotificationInfo.Error);

            Assert.AreEqual(1, source.SubscribeCount, "不可立即重註冊");
            CollectionAssert.AreEqual(new[] { EventName }, notifier.Sent);
            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled.Single().Delay);
            Assert.IsTrue(log.Has(LogEventLevel.Warning, "Error"));
        }

        [TestMethod]
        public void OnNotified_AfterStop_DoesNothing()
        {
            // code review S1：停止期間的通知不推播、不排程、不留誤導 log
            var (sut, source, notifier, log) = Create(out var scheduler);
            sut.Start();
            sut.Stop();

            source.Notify(SqlNotificationType.Change);
            source.Notify(SqlNotificationType.Subscribe, SqlNotificationInfo.Invalid);

            Assert.AreEqual(0, notifier.Sent.Count);
            Assert.AreEqual(0, scheduler.Scheduled.Count);
            Assert.AreEqual(1, source.SubscribeCount);
            Assert.IsFalse(log.Events.Any(e => e.Level >= LogEventLevel.Warning));
        }

        // ── T6 重註冊失敗自動重試 ────────────────────────────────

        [TestMethod]
        public void Start_SubscribeFails_SchedulesRetryAfter5Seconds()
        {
            var (sut, source, _, _) = Create(out var scheduler);
            source.FailPlan.Enqueue(true);

            sut.Start();

            Assert.AreEqual(1, scheduler.Scheduled.Count);
            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled[0].Delay);
        }

        [TestMethod]
        public void Retry_KeepsFailing_BacksOff5s15s60sThenEvery5Minutes()
        {
            var (sut, source, _, _) = Create(out var scheduler);
            for (int i = 0; i < 6; i++) source.FailPlan.Enqueue(true);

            sut.Start();
            var delays = new List<TimeSpan> { scheduler.Scheduled[^1].Delay };
            for (int i = 0; i < 5; i++)
            {
                scheduler.RunLast();
                delays.Add(scheduler.Scheduled[^1].Delay);
            }

            CollectionAssert.AreEqual(
                new[] { 5, 15, 60, 300, 300, 300 }.Select(s => TimeSpan.FromSeconds(s)).ToArray(),
                delays.ToArray());
            Assert.AreEqual(6, source.SubscribeCount, "每次重試都要真的再註冊，不可放棄");
        }

        [TestMethod]
        public void Retry_SucceedsAfterFailures_LogsRecoveryAndStopsRetrying()
        {
            var (sut, source, _, log) = Create(out var scheduler);
            source.FailPlan.Enqueue(true);
            source.FailPlan.Enqueue(true);

            sut.Start();
            scheduler.RunLast();
            scheduler.RunLast();   // 第 3 次註冊成功

            Assert.AreEqual(0, scheduler.Scheduled.Count);
            Assert.IsTrue(log.Has(LogEventLevel.Information, "恢復"));
        }

        [TestMethod]
        public void Retry_FailsAgainAfterRecovery_BackoffRestartsAt5Seconds()
        {
            var (sut, source, _, _) = Create(out var scheduler);
            source.FailPlan.Enqueue(true);
            source.FailPlan.Enqueue(true);
            sut.Start();
            scheduler.RunLast();
            scheduler.RunLast();             // 恢復
            source.FailPlan.Enqueue(true);

            source.Notify(SqlNotificationType.Change);   // 收到通知後重新註冊失敗

            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled.Single().Delay);
        }

        [TestMethod]
        public void Retry_AfterStop_DoesNotSubscribe()
        {
            var (sut, source, _, _) = Create(out var scheduler);
            source.FailPlan.Enqueue(true);
            sut.Start();

            sut.Stop();
            scheduler.RunLast();

            Assert.AreEqual(1, source.SubscribeCount);
        }

        // ── T0 現有行為快照 ──────────────────────────────────────

        [TestMethod]
        public void Definitions_SixQueriesAndEventNames_UnchangedFromOriginal()
        {
            // 前端 connection.on 依賴事件名稱；查詢欄位決定哪些變更會觸發通知，兩者都不可改
            var expected = new[]
            {
                ("select HaveFlag,BgnToEnd from dbo.oPort", "SendTracChange"),
                ("select PosX,PosY from dbo.oShuttle", "SendAgvChange"),
                ("select Battery,Status,LastStation,BeginStation,EndStation from dbo.oShuttle", "SendAgvStatusChange"),
                ("select TaskDateTime from dbo.ubMission", "SendTotalTaskChange"),
                ("select AssignFlag,OkFlag from dbo.oRequire", "SendDispatchChange"),
                ("select OkFlag from dbo.oMission", "SendMissionChange"),
            };

            CollectionAssert.AreEqual(expected, Service.Definitions.Select(d => (d.Sql, d.EventName)).ToArray());
        }

        [TestMethod]
        public void Start_Subscribes_WithConfiguredSql()
        {
            var (sut, source, _, _) = Create();

            sut.Start();

            Assert.AreEqual(1, source.SubscribeCount);
            Assert.AreEqual(Sql, source.LastSql);
        }

        [TestMethod]
        public void OnNotified_Change_ResubscribesThenSendsEvent()
        {
            var (sut, source, notifier, _) = Create();
            sut.Start();

            source.Notify(SqlNotificationType.Change);

            Assert.AreEqual(2, source.SubscribeCount);
            CollectionAssert.AreEqual(new[] { EventName }, notifier.Sent);
        }

        [TestMethod]
        public void Start_SubscribeThrows_LogsErrorWithSql()
        {
            var (sut, source, _, log) = Create();
            source.FailPlan.Enqueue(true);

            sut.Start();

            Assert.IsTrue(log.Has(LogEventLevel.Error, Sql));
        }
    }
}
