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

            public Task SendAsync(string eventName)
            {
                Sent.Add(eventName);
                return Task.CompletedTask;
            }
        }

        internal sealed class ListSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();
            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
            public bool Has(LogEventLevel level, string text)
                => Events.Any(e => e.Level == level && e.RenderMessage().Contains(text));
        }

        private static (QueryNotificationSubscription Sut, FakeSource Source, FakeNotifier Notifier, ListSink Log) Create()
        {
            var source = new FakeSource();
            var notifier = new FakeNotifier();
            var sink = new ListSink();
            var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            return (new QueryNotificationSubscription(Sql, EventName, source, notifier, logger), source, notifier, sink);
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
