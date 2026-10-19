using System;
using System.Collections.Generic;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// T19 空搬偵測（ToDo/20260726_一廠AB棟1F新路線與邏輯，spec.md AC-6）。
    /// 設計：end 回呼 WatchArrival 登記「等待到料」，StockSensorLink 每輪讀目的站 sensor：
    ///   ON → 到料正常，解除；登記後第一輪 OFF 不計（讀值可能早於登記），之後連續 ConfirmCount 輪 OFF → 判定空搬，
    ///   寫 Warning 並觸發 EmptyTransferDetected；讀取失敗 → 取消該次偵測。
    /// 不看 oPort.HaveFlag：end 回呼會無條件把目的站寫成有料。只寫 Log，不做畫面顯示（2026-07-26 確認）。
    /// </summary>
    [TestClass]
    public class EmptyTransferDetectionTests
    {
        private class FakeBitReader : IBitReader
        {
            public Dictionary<string, bool> Values { get; } = new Dictionary<string, bool>();
            public HashSet<string> FailAddresses { get; } = new HashSet<string>();
            /// <summary>讀 bit 之後、回傳之前呼叫：模擬 end 回呼剛好夾在讀值與檢查之間。</summary>
            public Action OnRead { get; set; }

            public bool TryReadBit(string address, out bool on)
            {
                if (FailAddresses.Contains(address))
                {
                    on = false;
                    return false;
                }
                on = Values.TryGetValue(address, out var v) && v;
                var callback = OnRead;
                OnRead = null;
                callback?.Invoke();
                return true;
            }
        }

        /// <summary>可變的 oPort 狀態：清空時同步改回空站，模擬正式 DB 行為。</summary>
        private class FakePortStore : IPortStateReader, IStockClearer
        {
            public Dictionary<string, oPortModel> Ports { get; } = new Dictionary<string, oPortModel>();
            public List<string> Cleared { get; } = new List<string>();

            public oPortModel GetPort(string stationNo)
                => Ports.TryGetValue(stationNo, out var p) ? p : new oPortModel { RackId = "", WorkOrder = "", HaveFlag = "0" };

            public void ClearStock(string stationNo)
            {
                Cleared.Add(stationNo);
                Ports[stationNo] = new oPortModel { RackId = "", WorkOrder = "", HaveFlag = "0" };
            }
        }

        private static readonly DateTime EndTime = new DateTime(2026, 10, 19, 15, 30, 0);

        private static (StockSensorLinkManager Sut, FakeBitReader Reader, FakePortStore Ports, List<EmptyTransferInfo> Detected) CreateSut(int confirmCount = 3)
        {
            var reader = new FakeBitReader();
            var ports = new FakePortStore();
            var sut = new StockSensorLinkManager(new List<SensorMapping> { new SensorMapping("M10", "B1") }, reader,
                ports, ports, null, 1000, confirmCount);
            var detected = new List<EmptyTransferInfo>();
            sut.EmptyTransferDetected += detected.Add;
            return (sut, reader, ports, detected);
        }

        private static void Scan(StockSensorLinkManager sut, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                sut.ScanOnce();
            }
        }

        // ── P0 ──────────────────────────────────────────────

        [TestMethod]
        public void Detect_TargetSensorOn_NoEmptyTransfer()
        {
            var (sut, reader, _, detected) = CreateSut();
            reader.Values["M10"] = true;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 5);

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void Detect_TargetSensorOffConfirmCountRounds_RaisesEmptyTransferWithTaskInfo()
        {
            var (sut, reader, _, detected) = CreateSut(confirmCount: 3);
            reader.Values["M10"] = false;

            Assert.AreEqual(ArrivalWatchResult.Watching, sut.WatchArrival("B1", "T001", "L1", EndTime));
            Scan(sut, 3);   // 第 1 輪只啟動不計，第 2、3 輪計 2
            Assert.AreEqual(0, detected.Count, "未達 ConfirmCount 不判定");
            sut.ScanOnce();

            Assert.AreEqual(1, detected.Count);
            Assert.AreEqual("B1", detected[0].EndStation);
            Assert.AreEqual("T001", detected[0].TaskCode);
            Assert.AreEqual("L1", detected[0].BeginStation);
            Assert.AreEqual(EndTime, detected[0].EndTime);
        }

        [TestMethod]
        public void Detect_AfterDetected_ReportsOnlyOnce()
        {
            var (sut, reader, _, detected) = CreateSut(confirmCount: 1);
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 5);

            Assert.AreEqual(1, detected.Count);
        }

        [TestMethod]
        public void Detect_NoWatch_SensorOff_NoEmptyTransfer()
        {
            // 平常空站 sensor 本來就 OFF，沒有任務送達時不可誤報
            var (sut, reader, _, detected) = CreateSut(confirmCount: 1);
            reader.Values["M10"] = false;

            Scan(sut, 5);

            Assert.AreEqual(0, detected.Count);
        }

        // ── P1 ──────────────────────────────────────────────

        [TestMethod]
        public void Detect_SensorUpdateDelayed_OffThenOnBeforeConfirm_NoEmptyTransfer()
        {
            var (sut, reader, _, detected) = CreateSut(confirmCount: 3);
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 3);              // 完成瞬間 sensor 尚未亮
            reader.Values["M10"] = true;
            sut.ScanOnce();            // 轉 ON → 到料確認
            reader.Values["M10"] = false;
            Scan(sut, 5);              // 之後被正常取走，不屬於這次任務

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void Detect_ReadValueBeforeRegistration_NotCounted()
        {
            // code review W1：end 回呼夾在讀 bit 與檢查之間，該輪讀值早於登記，不可算進 OFF 輪數
            var (sut, reader, _, detected) = CreateSut(confirmCount: 1);
            reader.Values["M10"] = false;
            reader.OnRead = () => sut.WatchArrival("B1", "T001", "L1", EndTime);

            sut.ScanOnce();
            Assert.AreEqual(0, detected.Count, "登記前的讀值不判定");
            sut.ScanOnce();

            Assert.AreEqual(1, detected.Count);
        }

        [TestMethod]
        public void Detect_ReadFailure_CancelsWatch()
        {
            // code review W2：PLC 長時間斷線後恢復，不可把早已正常取走的舊任務報成空搬
            var (sut, reader, _, detected) = CreateSut(confirmCount: 1);
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            reader.FailAddresses.Add("M10");
            sut.ScanOnce();
            reader.FailAddresses.Clear();
            Scan(sut, 5);

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void Detect_TargetStationWithoutSensor_ReturnsNoSensorAndNeverDetects()
        {
            var (sut, reader, _, detected) = CreateSut(confirmCount: 1);
            reader.Values["M10"] = false;

            Assert.AreEqual(ArrivalWatchResult.NoSensor, sut.WatchArrival("E1", "T001", "D1", EndTime));
            Scan(sut, 3);

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void Detect_TargetStationEmpty_ReturnsInvalidStationNotEmptyTransfer()
        {
            var (sut, _, _, detected) = CreateSut(confirmCount: 1);

            Assert.AreEqual(ArrivalWatchResult.InvalidStation, sut.WatchArrival("", "T001", "L1", EndTime));
            Assert.AreEqual(ArrivalWatchResult.InvalidStation, sut.WatchArrival(null, "T001", "L1", EndTime));
            Scan(sut, 3);

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void WatchArrival_ScanThreadEnded_ReturnsNotMonitoring()
        {
            // code review W2：PLC 連線建立失敗時掃描執行緒直接結束，登記會永遠無法判定
            var sut = new StockSensorLinkManager(new List<SensorMapping> { new SensorMapping("M10", "B1") },
                () => throw new InvalidOperationException("模擬 PLC 連線失敗"), null, null, null);
            sut.Start();
            sut.Stop();   // Join 等執行緒結束

            Assert.AreEqual(ArrivalWatchResult.NotMonitoring, sut.WatchArrival("B1", "T001", "L1", EndTime));
        }

        [TestMethod]
        public void Detect_NewTaskToSameStation_ReplacesPreviousWatch()
        {
            var (sut, reader, _, detected) = CreateSut(confirmCount: 2);
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 2);
            sut.WatchArrival("b1", "T002", "L2", EndTime.AddMinutes(1));   // 站號不分大小寫，重新起算
            Scan(sut, 2);
            Assert.AreEqual(0, detected.Count, "新任務重新起算");
            sut.ScanOnce();

            Assert.AreEqual(1, detected.Count);
            Assert.AreEqual("T002", detected[0].TaskCode);
        }

        [TestMethod]
        public void Detect_EventHandlerThrows_ScanContinuesAndOtherHandlersRun()
        {
            var (sut, reader, _, detected) = CreateSut(confirmCount: 1);
            sut.EmptyTransferDetected += _ => throw new InvalidOperationException("模擬掛載動作失敗");
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 2);   // 不可外拋
            sut.WatchArrival("B1", "T002", "L1", EndTime);
            Scan(sut, 2);

            Assert.AreEqual(2, detected.Count, "掛載例外後下一筆仍正常判定");
        }

        // ── 與既有 OFF 清空邏輯的互動（正式情境：end 已把目的站寫成有料）──

        [TestMethod]
        public void Detect_OccupiedTargetSensorOff_ClearsAndDetectsOnceEach()
        {
            var (sut, reader, ports, detected) = CreateSut(confirmCount: 3);
            ports.Ports["B1"] = new oPortModel { RackId = "R01", WorkOrder = "WO1", HaveFlag = "3" };
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 8);

            Assert.AreEqual(1, ports.Cleared.Count, "庫位清空一次");
            Assert.AreEqual(1, detected.Count, "空搬只報一次");
        }

        [TestMethod]
        public void Detect_OccupiedTargetSensorOnBeforeConfirm_NeitherClearsNorDetects()
        {
            var (sut, reader, ports, detected) = CreateSut(confirmCount: 3);
            ports.Ports["B1"] = new oPortModel { RackId = "R01", WorkOrder = "WO1", HaveFlag = "3" };
            reader.Values["M10"] = false;

            sut.WatchArrival("B1", "T001", "L1", EndTime);
            Scan(sut, 2);
            reader.Values["M10"] = true;
            Scan(sut, 5);

            Assert.AreEqual(0, ports.Cleared.Count);
            Assert.AreEqual(0, detected.Count);
        }
    }
}
