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
            public bool FailStop { get; set; }
            public int StartCount { get; private set; }

            public void Start(string connectionString)
            {
                StartCount++;
                if (FailStart) throw new InvalidOperationException("模擬 broker 未啟用");
            }

            public void Stop(string connectionString)
            {
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
