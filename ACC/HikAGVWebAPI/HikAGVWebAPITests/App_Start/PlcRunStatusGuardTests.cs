using System;
using System.Collections.Generic;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// T30 PLC 未 RUN 時暫停 sensor 判定。
    /// 背景（2026-10-20 GX Simulator 實測）：PLC 重開後、程式未執行前讀取成功但 M 全為 0，有料站會被全清；
    /// STOP 時 SM400 仍為 1 不可用，改讀 CPU 運行狀態 SD203（RUN=0）。
    /// 規則：每輪先讀 RunStatusAddress；讀取失敗或 ≠0 → 整輪跳過（不清空、不登記、取消空搬偵測、去抖歸 0）；
    /// 恢復為 0 後再等 RunResumeGraceRounds 輪才恢復判定。RunStatusAddress 留空＝不檢查（相容舊行為）。
    /// </summary>
    [TestClass]
    public class PlcRunStatusGuardTests
    {
        #region 測試假件 / 工具

        private class FakePlc : IBitReader, IWordReader
        {
            public Dictionary<string, bool> Bits { get; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, short> Words { get; } = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> FailWords { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public int BitReads { get; private set; }
            public int WordReads { get; private set; }

            public bool TryReadBit(string address, out bool on)
            {
                BitReads++;
                on = Bits.TryGetValue(address, out var v) && v;
                return true;
            }

            public bool TryReadWord(string address, out short value)
            {
                WordReads++;
                value = 0;
                if (FailWords.Contains(address))
                {
                    return false;
                }
                value = Words.TryGetValue(address, out var v) ? v : (short)0;
                return true;
            }
        }

        /// <summary>只會讀 bit、不支援 word 讀取的 reader。</summary>
        private class BitOnlyReader : IBitReader
        {
            public bool TryReadBit(string address, out bool on)
            {
                on = false;
                return true;
            }
        }

        private class FakePortReader : IPortStateReader
        {
            public Dictionary<string, oPortModel> Ports { get; } = new Dictionary<string, oPortModel>(StringComparer.OrdinalIgnoreCase);
            public oPortModel GetPort(string stationNo) => Ports.TryGetValue(stationNo, out var p) ? p : null;
        }

        private class SpyClearer : IStockClearer
        {
            public List<string> Cleared { get; } = new List<string>();
            public void ClearStock(string stationNo) => Cleared.Add(stationNo);
        }

        private class SpyRegistrar : IStockRegistrar
        {
            public List<string> Marked { get; } = new List<string>();
            public void MarkRackPresent(string stationNo) => Marked.Add(stationNo);
        }

        private class NoTaskStore : ISourceTaskStore
        {
            public bool HasActiveTask(string stationNo) => false;
            public bool HasRunningMission(string stationNo) => false;
            public int DeleteUndispatchedNeeds(string stationNo) => 0;
        }

        private const string Run = "SD203";

        private static oPortModel Occupied() => new oPortModel { RackId = "R001", WorkOrder = "WO1", HaveFlag = "3" };
        private static oPortModel Empty() => new oPortModel { RackId = "", WorkOrder = "", HaveFlag = "0" };

        private static StockSensorLinkManager CreateSut(IBitReader reader, FakePortReader ports, SpyClearer clearer,
            int confirmCount, string runStatusAddress = Run, int graceRounds = 3, SpyRegistrar registrar = null,
            string[] sensorOnStations = null)
        {
            var sut = new StockSensorLinkManager(
                new List<SensorMapping> { new SensorMapping("M10", "B1"), new SensorMapping("M11", "O1") },
                reader, clearer, ports, null, 1000, confirmCount,
                registrar, new NoTaskStore(), sensorOnStations);
            sut.RunStatusAddress = runStatusAddress;
            sut.RunResumeGraceRounds = graceRounds;
            return sut;
        }

        private static void Scan(StockSensorLinkManager sut, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                sut.ScanOnce();
            }
        }

        #endregion 測試假件 / 工具

        #region RUN 中 → 照常判定

        [TestMethod]
        public void PLC在RUN_sensorOFF且佔用_照常清空()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 0;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1);

            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "B1" }, clearer.Cleared);
            Assert.AreEqual(1, plc.WordReads, "每輪先讀一次運行狀態");
        }

        [TestMethod]
        public void 未設定RunStatusAddress_不讀運行狀態_照常清空()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 2;   // 即使 PLC 在 STOP，未設定就不檢查（相容舊行為）
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1, runStatusAddress: "  ");

            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "B1" }, clearer.Cleared);
            Assert.AreEqual(0, plc.WordReads);
        }

        [TestMethod]
        public void 啟動第一輪即為RUN_不需寬限_立即判定()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 0;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1, graceRounds: 3);

            sut.ScanOnce();

            Assert.AreEqual(1, clearer.Cleared.Count, "寬限只用在從非 RUN 恢復時");
        }

        #endregion RUN 中 → 照常判定

        #region 非 RUN / 讀取失敗 → 整輪跳過

        [DataTestMethod]
        [DataRow((short)2, DisplayName = "STOP=2")]
        [DataRow((short)66, DisplayName = "重開未 RUN=66")]
        [DataRow((short)3, DisplayName = "PAUSE=3")]
        public void PLC不在RUN_不清空且不讀sensor(short status)
        {
            var plc = new FakePlc();
            plc.Words[Run] = status;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            ports.Ports["O1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1);

            Scan(sut, 10);

            Assert.AreEqual(0, clearer.Cleared.Count);
            Assert.AreEqual(0, plc.BitReads, "非 RUN 時 sensor 值不可信，不讀");
        }

        [TestMethod]
        public void 運行狀態讀取失敗_不清空()
        {
            var plc = new FakePlc();
            plc.FailWords.Add(Run);
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1);

            Scan(sut, 10);

            Assert.AreEqual(0, clearer.Cleared.Count);
            Assert.AreEqual(0, plc.BitReads);
        }

        [TestMethod]
        public void Reader不支援word讀取_無法確認RUN_不清空()
        {
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(new BitOnlyReader(), ports, clearer, confirmCount: 1);

            Scan(sut, 5);

            Assert.AreEqual(0, clearer.Cleared.Count, "設了 RunStatusAddress 卻無法讀取 → 寧可不清");
        }

        [TestMethod]
        public void PLC不在RUN_中光電來源站不登記HaveFlag1()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 2;
            plc.Bits["M11"] = true;
            var ports = new FakePortReader();
            ports.Ports["O1"] = Empty();
            var registrar = new SpyRegistrar();
            var sut = CreateSut(plc, ports, new SpyClearer(), confirmCount: 1, registrar: registrar, sensorOnStations: new[] { "O1" });

            Scan(sut, 5);

            Assert.AreEqual(0, registrar.Marked.Count);
        }

        #endregion 非 RUN / 讀取失敗 → 整輪跳過

        #region 恢復 RUN 後的寬限與計數重置

        [TestMethod]
        public void 從非RUN恢復_寬限輪數內不判定_之後恢復()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 66;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1, graceRounds: 3);

            Scan(sut, 2);          // 重開、未 RUN
            plc.Words[Run] = 0;    // 進 RUN
            Scan(sut, 3);          // 寬限 3 輪
            Assert.AreEqual(0, clearer.Cleared.Count, "寬限期間不判定（PLC 程式尚未把 M 寫回）");
            Assert.AreEqual(0, plc.BitReads);

            sut.ScanOnce();        // 寬限結束
            CollectionAssert.AreEqual(new[] { "B1" }, clearer.Cleared);
        }

        [TestMethod]
        public void 寬限期間又掉出RUN_寬限重新起算()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 2;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1, graceRounds: 3);

            sut.ScanOnce();
            plc.Words[Run] = 0;
            Scan(sut, 2);          // 寬限剩 1 輪
            plc.Words[Run] = 2;
            sut.ScanOnce();        // 又 STOP
            plc.Words[Run] = 0;
            Scan(sut, 3);          // 重新寬限 3 輪

            Assert.AreEqual(0, clearer.Cleared.Count);
            sut.ScanOnce();
            Assert.AreEqual(1, clearer.Cleared.Count);
        }

        [TestMethod]
        public void 寬限為0_恢復RUN當輪即判定()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 2;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1, graceRounds: 0);

            sut.ScanOnce();
            plc.Words[Run] = 0;
            sut.ScanOnce();

            Assert.AreEqual(1, clearer.Cleared.Count);
        }

        [TestMethod]
        public void 非RUN會重置去抖計數_恢復後需重新累積ConfirmCount輪()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 0;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 3, graceRounds: 0);

            Scan(sut, 2);          // 已累積 2 輪 OFF
            plc.Words[Run] = 2;
            sut.ScanOnce();        // STOP：計數歸 0
            plc.Words[Run] = 0;
            Scan(sut, 2);          // 重新累積 2 輪，未達 3
            Assert.AreEqual(0, clearer.Cleared.Count);

            sut.ScanOnce();
            Assert.AreEqual(1, clearer.Cleared.Count);
        }

        [TestMethod]
        public void 非RUN會取消空搬偵測_恢復後不誤報()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 0;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Empty();
            var sut = CreateSut(plc, ports, new SpyClearer(), confirmCount: 1, graceRounds: 0);
            var detected = new List<EmptyTransferInfo>();
            sut.EmptyTransferDetected += detected.Add;

            Assert.AreEqual(ArrivalWatchResult.Watching, sut.WatchArrival("B1", "T1", "A1", DateTime.Now));
            plc.Words[Run] = 66;
            sut.ScanOnce();        // 非 RUN：取消登記
            plc.Words[Run] = 0;
            Scan(sut, 5);

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void 一直在RUN_空搬偵測照常()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 0;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Empty();
            var sut = CreateSut(plc, ports, new SpyClearer(), confirmCount: 1);
            var detected = new List<EmptyTransferInfo>();
            sut.EmptyTransferDetected += detected.Add;

            sut.WatchArrival("B1", "T1", "A1", DateTime.Now);
            Scan(sut, 2);          // 第一輪 Armed、第二輪判定

            Assert.AreEqual(1, detected.Count);
        }

        [TestMethod]
        public void 暫停期間登記的空搬偵測_恢復後不誤報()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 2;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Empty();
            var sut = CreateSut(plc, ports, new SpyClearer(), confirmCount: 1, graceRounds: 0);
            var detected = new List<EmptyTransferInfo>();
            sut.EmptyTransferDetected += detected.Add;

            sut.ScanOnce();                                   // 進入暫停
            sut.WatchArrival("B1", "T1", "A1", DateTime.Now); // 暫停中 end 回呼
            sut.ScanOnce();                                   // 仍暫停：取消
            plc.Words[Run] = 0;
            Scan(sut, 5);

            Assert.AreEqual(0, detected.Count);
        }

        [TestMethod]
        public void 寬限期間登記的空搬偵測_寬限結束後照常判定()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 2;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Empty();
            var sut = CreateSut(plc, ports, new SpyClearer(), confirmCount: 1, graceRounds: 2);
            var detected = new List<EmptyTransferInfo>();
            sut.EmptyTransferDetected += detected.Add;

            sut.ScanOnce();
            plc.Words[Run] = 0;
            sut.ScanOnce();                                   // 寬限 1/2
            sut.WatchArrival("B1", "T1", "A1", DateTime.Now); // 寬限中 end 回呼（RUN 中，登記保留）
            sut.ScanOnce();                                   // 寬限 2/2
            Assert.AreEqual(0, detected.Count);

            Scan(sut, 2);                                     // Armed → 判定
            Assert.AreEqual(1, detected.Count);
        }

        #endregion 恢復 RUN 後的寬限與計數重置

        #region SD203 只看運行狀態（低 4 bit）

        [DataTestMethod]
        [DataRow((short)0x0020, DisplayName = "RUN 但要因 bit 殘留=0x0020")]
        [DataRow((short)0x0040, DisplayName = "RUN 但要因 bit 殘留=0x0040")]
        public void 低4bit為0_視為RUN(short status)
        {
            var plc = new FakePlc();
            plc.Words[Run] = status;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1);

            sut.ScanOnce();

            Assert.AreEqual(1, clearer.Cleared.Count);
        }

        [TestMethod]
        public void 重開實測值0x42_低4bit為STOP_暫停()
        {
            var plc = new FakePlc();
            plc.Words[Run] = 0x42;
            var ports = new FakePortReader();
            ports.Ports["B1"] = Occupied();
            var clearer = new SpyClearer();
            var sut = CreateSut(plc, ports, clearer, confirmCount: 1);

            Scan(sut, 5);

            Assert.AreEqual(0, clearer.Cleared.Count);
        }

        #endregion SD203 只看運行狀態（低 4 bit）
    }
}
