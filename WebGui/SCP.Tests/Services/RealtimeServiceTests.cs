using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Services;
using Serilog;
using Serilog.Events;
using static SCP.Tests.Services.QueryNotificationSubscriptionTests;

namespace SCP.Tests.Services
{
    /// <summary>
    /// SCP 即時更新 Service 硬化 P2（ToDo/20260605_SCP即時更新Service硬化）
    /// T4 StopAsync 例外不外拋、T5 連線字串缺失與 broker 未啟用分開記錄。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("RealtimeHardening")]
    public class RealtimeServiceTests
    {
        private sealed class FakeListener : IQueryNotificationListener
        {
            public bool FailStart { get; set; }
            /// <summary>依序決定每次 Start 是否失敗（優先於 FailStart）。</summary>
            public Queue<bool> StartFailPlan { get; } = new Queue<bool>();
            public bool FailStop { get; set; }
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }

            public void Start(string connectionString)
            {
                StartCount++;
                bool fail = StartFailPlan.Count > 0 ? StartFailPlan.Dequeue() : FailStart;
                if (fail) throw new InvalidOperationException("模擬 broker 未啟用");
            }

            public void Stop(string connectionString)
            {
                StopCount++;
                if (FailStop) throw new InvalidOperationException("模擬 Stop 失敗");
            }
        }

        private static (Service Sut, FakeListener Listener, FakeSource Source, FakeScheduler Scheduler, ListSink Log) Create(string? connectionString = "Server=x;Database=y")
        {
            var listener = new FakeListener();
            var source = new FakeSource();
            var scheduler = new FakeScheduler();
            var sink = new ListSink();
            var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            var sut = new Service(connectionString, source, new FakeNotifier(), listener, scheduler, logger);
            return (sut, listener, source, scheduler, sink);
        }

        [TestMethod]
        public async Task StartAsync_Normal_SubscribesAllSixDefinitions()
        {
            var (sut, listener, source, _, _) = Create();

            await sut.StartAsync(CancellationToken.None);

            Assert.AreEqual(1, listener.StartCount);
            Assert.AreEqual(6, source.SubscribeCount);
        }

        [TestMethod]
        public async Task StartAsync_ListenerStartFails_LogsBrokerHintAndDoesNotSubscribe()
        {
            var (sut, listener, source, _, log) = Create();
            listener.FailStart = true;

            await sut.StartAsync(CancellationToken.None);

            Assert.AreEqual(0, source.SubscribeCount);
            Assert.IsTrue(log.Has(LogEventLevel.Error, "ENABLE_BROKER"));
        }

        // ── 接收端重建（整合測試發現：Broker 中斷後 SqlDependency 接收端壞掉，註冊看似成功但通知送不到）──

        [TestMethod]
        public async Task ListenerError_FromAllSix_RestartsOnceThenResubscribesAll()
        {
            var (sut, listener, source, scheduler, log) = Create();
            await sut.StartAsync(CancellationToken.None);
            var callbacks = source.Registrations.Select(r => r.Callback).ToList();

            foreach (var callback in callbacks) source.NotifyListenerError(callback);   // 6 條同時收到

            Assert.AreEqual(1, scheduler.Scheduled.Count, "6 條通知合併為一次重建");
            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled[0].Delay);

            scheduler.RunLast();

            Assert.AreEqual(1, listener.StopCount);
            Assert.AreEqual(2, listener.StartCount);
            Assert.AreEqual(12, source.SubscribeCount, "重建後 6 條全部重新註冊");
            Assert.IsTrue(log.Has(LogEventLevel.Information, "重建"));
        }

        [TestMethod]
        public async Task ListenerRestart_StartFails_RetriesWithBackoffUntilSuccess()
        {
            var (sut, listener, source, scheduler, _) = Create();
            await sut.StartAsync(CancellationToken.None);
            source.NotifyListenerError(source.Registrations[0].Callback);
            listener.StartFailPlan.Enqueue(true);   // 第 1 次重建失敗（例：Broker 仍未恢復）

            scheduler.RunLast();
            Assert.AreEqual(TimeSpan.FromSeconds(15), scheduler.Scheduled.Single().Delay);
            scheduler.RunLast();                    // 第 2 次成功

            Assert.AreEqual(0, scheduler.Scheduled.Count);
            Assert.AreEqual(12, source.SubscribeCount);
        }

        [TestMethod]
        public async Task StartAsync_ListenerStartFails_RetriesAndRecoversWhenBrokerEnabled()
        {
            // 開機時 Broker 未啟用：原本永久放棄（本機 2026-10-19 實況），改為退避重試，啟用後自動恢復
            var (sut, listener, source, scheduler, _) = Create();
            listener.StartFailPlan.Enqueue(true);

            await sut.StartAsync(CancellationToken.None);
            Assert.AreEqual(TimeSpan.FromSeconds(5), scheduler.Scheduled.Single().Delay);

            scheduler.RunLast();

            Assert.AreEqual(6, source.SubscribeCount);
        }

        // ── T5 連線字串缺失 ───────────────────────────────────────

        [TestMethod]
        public async Task StartAsync_EmptyConnectionString_LogsMissingConnectionStringNotBroker()
        {
            var (sut, listener, source, _, log) = Create(connectionString: "");

            await sut.StartAsync(CancellationToken.None);

            Assert.AreEqual(0, listener.StartCount, "連線字串缺失不必嘗試 SqlDependency.Start");
            Assert.AreEqual(0, source.SubscribeCount);
            Assert.IsTrue(log.Has(LogEventLevel.Error, "ConnectionStrings:DBContext"));
            Assert.IsFalse(log.Has(LogEventLevel.Error, "ENABLE_BROKER"), "不可誤導為 broker 未啟用");
        }

        [TestMethod]
        public async Task StopAsync_ListenerNeverStarted_DoesNotCallStopOrLogWarning()
        {
            // code review S2：連線字串缺失或 Start 失敗時，Stop 不再呼叫 SqlDependency.Stop（避免誤導的 Warning）
            var (sut, listener, _, _, log) = Create(connectionString: "");
            listener.FailStop = true;
            await sut.StartAsync(CancellationToken.None);

            await sut.StopAsync(CancellationToken.None);

            Assert.IsFalse(log.Has(LogEventLevel.Warning, "SqlDependency.Stop"));
        }

        // ── T4 StopAsync 例外不外拋 ──────────────────────────────

        [TestMethod]
        public async Task StopAsync_ListenerStopThrows_LogsWarningAndDoesNotThrow()
        {
            var (sut, listener, _, _, log) = Create();
            await sut.StartAsync(CancellationToken.None);
            listener.FailStop = true;

            await sut.StopAsync(CancellationToken.None);

            Assert.IsTrue(log.Has(LogEventLevel.Warning, "SqlDependency.Stop"));
        }

        [TestMethod]
        public async Task StopAsync_PendingRetry_NoLongerSubscribes()
        {
            var (sut, _, source, scheduler, _) = Create();
            source.FailPlan.Enqueue(true);   // 第一條訂閱失敗 → 排程重試
            await sut.StartAsync(CancellationToken.None);

            await sut.StopAsync(CancellationToken.None);
            scheduler.RunLast();

            Assert.AreEqual(6, source.SubscribeCount, "停止後到期的重試不可再註冊");
            Assert.IsTrue(scheduler.Cancelled);
        }
    }
}
