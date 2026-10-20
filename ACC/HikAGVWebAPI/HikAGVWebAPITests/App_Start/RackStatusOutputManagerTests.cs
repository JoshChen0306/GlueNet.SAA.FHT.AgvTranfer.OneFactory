using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// T18 貨架狀態輸出（三色燈）（ToDo/20260726_一廠AB棟1F新路線與邏輯，spec.md AC-5）。
    /// 規格（T16 我方暫定）：每站 3 個 M bit（綠/黃/紅，同時只亮一個）；HaveFlag 0 → 綠、1/2 → 黃、3 → 紅。
    /// 狀態有變才寫；寫入失敗不記錄狀態，下輪重寫（自癒）；PLC 斷線中止整輪；定期全量重寫；讀不到 oPort 維持現況。
    /// </summary>
    [TestClass]
    public class RackStatusOutputManagerTests
    {
        private class SpyBitWriter : IBitWriter, IPlcConnectionState
        {
            private readonly object _lock = new object();
            private readonly List<(string Address, bool On)> _writes = new List<(string, bool)>();
            public HashSet<string> FailAddresses { get; } = new HashSet<string>();
            /// <summary>失敗時模擬「連不上 PLC」（LastOpenFailed=true）；否則模擬位址錯誤（仍可連線）。</summary>
            public bool DisconnectOnFail { get; set; }
            public bool LastOpenFailed { get; private set; }
            public string SignalAddress { get; set; }
            public ManualResetEventSlim Signal { get; } = new ManualResetEventSlim(false);

            public bool TryWriteBit(string address, bool on)
            {
                lock (_lock)
                {
                    if (FailAddresses.Contains(address))
                    {
                        LastOpenFailed = DisconnectOnFail;
                        return false;
                    }
                    LastOpenFailed = false;
                    _writes.Add((address, on));
                }
                if (on && address == SignalAddress) Signal.Set();
                return true;
            }

            public (string Address, bool On)[] Writes { get { lock (_lock) return _writes.ToArray(); } }
            public void Clear() { lock (_lock) _writes.Clear(); }
        }

        private class FakeFlagReader : IHaveFlagReader
        {
            public Dictionary<string, string> Flags { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public bool Throw { get; set; }
            public int Calls { get; private set; }
            public void Set(string station, string haveFlag) => Flags[station] = haveFlag;

            public IDictionary<string, string> GetHaveFlags(IList<string> stationNos)
            {
                Calls++;
                if (Throw) throw new InvalidOperationException("SQL 查詢操作失敗");
                return stationNos.Where(Flags.ContainsKey).ToDictionary(s => s, s => Flags[s]);
            }
        }

        private static LampMapping B1 => new LampMapping("B1", "M200", "M201", "M202");
        private static LampMapping O1 => new LampMapping("O1", "M203", "M204", "M205");

        private static (RackStatusOutputManager Sut, SpyBitWriter Writer, FakeFlagReader Flags) Create(params LampMapping[] lamps)
        {
            var writer = new SpyBitWriter();
            var flags = new FakeFlagReader();
            var sut = new RackStatusOutputManager(new List<LampMapping>(lamps), writer, flags, null);
            return (sut, writer, flags);
        }

        // ── 燈號規則 ─────────────────────────────────────────

        [TestMethod]
        public void ColorFor_HaveFlagMapping()
        {
            Assert.AreEqual(LampColor.Green, RackStatusOutputManager.ColorFor("0"));
            Assert.AreEqual(LampColor.Yellow, RackStatusOutputManager.ColorFor("1"));
            Assert.AreEqual(LampColor.Yellow, RackStatusOutputManager.ColorFor("2"));
            Assert.AreEqual(LampColor.Red, RackStatusOutputManager.ColorFor("3"));
            Assert.AreEqual(LampColor.Unknown, RackStatusOutputManager.ColorFor("E"));
            Assert.AreEqual(LampColor.Unknown, RackStatusOutputManager.ColorFor(null));
        }

        // ── P0 ───────────────────────────────────────────────

        [TestMethod]
        public void ScanOnce_FirstRound_WritesTargetOnAndOthersOff_OffFirst()
        {
            var (sut, writer, flags) = Create(B1);
            flags.Set("B1", "3");   // 紅

            sut.ScanOnce();

            CollectionAssert.AreEqual(
                new[] { ("M200", false), ("M201", false), ("M202", true) },
                writer.Writes, "先關其他燈再開目標燈，避免同時亮兩個");
        }

        [TestMethod]
        public void ScanOnce_StateUnchanged_DoesNotWriteAgain()
        {
            var (sut, writer, flags) = Create(B1);
            flags.Set("B1", "0");
            sut.ScanOnce();
            writer.Clear();

            sut.ScanOnce();
            sut.ScanOnce();

            Assert.AreEqual(0, writer.Writes.Length);
        }

        [TestMethod]
        public void ScanOnce_StateChanged_WritesNewColor()
        {
            var (sut, writer, flags) = Create(B1);
            flags.Set("B1", "0");
            sut.ScanOnce();
            writer.Clear();

            flags.Set("B1", "1");   // 綠 → 黃
            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { ("M200", false), ("M202", false), ("M201", true) }, writer.Writes);
        }

        [TestMethod]
        public void ScanOnce_TargetOnFails_RewritesAllThreeBitsNextRound()
        {
            var (sut, writer, flags) = Create(B1);
            flags.Set("B1", "3");
            writer.FailAddresses.Add("M202");   // 關燈成功、開紅燈失敗
            sut.ScanOnce();

            writer.FailAddresses.Clear();
            writer.Clear();
            sut.ScanOnce();               // PLC 恢復 → 依最新狀態重寫 3 個 bit

            CollectionAssert.AreEqual(new[] { ("M200", false), ("M201", false), ("M202", true) }, writer.Writes);
        }

        [TestMethod]
        public void ScanOnce_PeriodicFullRefresh_RewritesUnchangedLamps()
        {
            // code review C1：PLC 重開機清掉 M bit 時，HaveFlag 沒變也要定期重寫
            var (sut, writer, flags) = Create(B1);
            sut.FullRefreshRounds = 3;
            flags.Set("B1", "0");
            sut.ScanOnce();               // 第 1 輪：寫
            writer.Clear();

            sut.ScanOnce();               // 第 2 輪：不變不寫
            Assert.AreEqual(0, writer.Writes.Length);
            sut.ScanOnce();               // 第 3 輪：全量重寫

            Assert.IsTrue(writer.Writes.Contains(("M200", true)));
        }

        // ── P1 ───────────────────────────────────────────────

        [TestMethod]
        public void ScanOnce_StationMissingFromRead_SkipsStationKeepsOthers()
        {
            var (sut, writer, flags) = Create(B1, O1);
            flags.Set("O1", "0");        // B1 查無資料

            sut.ScanOnce();

            var b1Addresses = new[] { "M200", "M201", "M202" };
            Assert.IsFalse(writer.Writes.Any(w => b1Addresses.Contains(w.Address)), "B1 本輪不寫");
            Assert.IsTrue(writer.Writes.Contains(("M203", true)), "O1 照常寫綠燈");
        }

        [TestMethod]
        public void ScanOnce_ReadThrows_NoWriteNoException()
        {
            var (sut, writer, flags) = Create(B1);
            flags.Throw = true;

            sut.ScanOnce();

            Assert.AreEqual(0, writer.Writes.Length, "讀不到 oPort 時維持現有燈號");
        }

        [TestMethod]
        public void ScanOnce_ReadsAllStationsInOneQuery()
        {
            var (sut, _, flags) = Create(B1, O1);
            flags.Set("B1", "0");
            flags.Set("O1", "0");

            sut.ScanOnce();

            Assert.AreEqual(1, flags.Calls, "每輪只查一次 DB");
        }

        [TestMethod]
        public void ScanOnce_UnknownHaveFlag_SkipsWithoutWriting()
        {
            var (sut, writer, flags) = Create(B1);
            flags.Set("B1", "E");

            sut.ScanOnce();

            Assert.AreEqual(0, writer.Writes.Length);
        }

        [TestMethod]
        public void ScanOnce_OneStationWriteFails_OthersStillWritten()
        {
            var (sut, writer, flags) = Create(B1, O1);
            flags.Set("B1", "3");
            flags.Set("O1", "3");
            writer.FailAddresses.Add("M200");   // B1 位址錯誤（仍連線中）

            sut.ScanOnce();

            Assert.IsTrue(writer.Writes.Contains(("M205", true)), "O1 不受 B1 失敗影響");
        }

        [TestMethod]
        public void ScanOnce_AddressErrorNotDisconnect_LaterStationsStillWrittenEveryRound()
        {
            // code review 複審 C1：位址錯誤（例：M99999 超出 PLC 範圍）不可被當成斷線而中止整輪
            var bad = new LampMapping("B1", "M99997", "M99998", "M99999");
            var (sut, writer, flags) = Create(bad, O1);
            flags.Set("B1", "3");
            flags.Set("O1", "0");
            writer.FailAddresses.Add("M99997");

            sut.ScanOnce();
            Assert.IsTrue(writer.Writes.Contains(("M203", true)), "第 1 輪 O1 有寫");

            writer.Clear();
            flags.Set("O1", "3");
            sut.ScanOnce();
            Assert.IsTrue(writer.Writes.Contains(("M205", true)), "第 2 輪 O1 仍會更新");
        }

        [TestMethod]
        public void ScanOnce_PlcDisconnected_AbortsRound()
        {
            // code review W1：斷線時不逐站等 Open 逾時
            var (sut, writer, flags) = Create(B1, O1);
            flags.Set("B1", "3");
            flags.Set("O1", "3");
            writer.FailAddresses.Add("M200");
            writer.DisconnectOnFail = true;

            sut.ScanOnce();

            Assert.IsFalse(writer.Writes.Any(w => w.Address == "M205"), "斷線後本輪不再嘗試其他站");
        }

        [TestMethod]
        public void ScanOnce_WriterThrows_DoesNotStopOtherStations()
        {
            var flags = new FakeFlagReader();
            flags.Set("B1", "3");
            flags.Set("O1", "0");
            var writer = new ThrowingWriter("M200");
            var sut = new RackStatusOutputManager(new List<LampMapping> { B1, O1 }, writer, flags, null);

            sut.ScanOnce();

            Assert.IsTrue(writer.Writes.Contains(("M203", true)));
        }

        private class ThrowingWriter : IBitWriter
        {
            private readonly string _throwOn;
            public List<(string, bool)> Writes { get; } = new List<(string, bool)>();
            public ThrowingWriter(string throwOn) { _throwOn = throwOn; }
            public bool TryWriteBit(string address, bool on)
            {
                if (address == _throwOn) throw new InvalidOperationException("COM 例外");
                Writes.Add((address, on));
                return true;
            }
        }

        // ── 點位載入 ─────────────────────────────────────────

        private static oPortPlcModel Row(string station, string g, string y, string r, string useFlag = "Y", string sensor = null)
            => new oPortPlcModel { StationNo = station, LampGreen = g, LampYellow = y, LampRed = r, LampUseFlag = useFlag, SensorAddress = sensor };

        [TestMethod]
        public void BuildLampMappings_SkipsDisabledIncompleteAndDuplicate()
        {
            var result = RackStatusOutputManager.BuildLampMappings(new[]
            {
                Row("B1", "M200", "M201", "M202"),
                Row("O1", "M203", "M204", "M205", useFlag: "N"),   // 停用
                Row("P1", "M206", "", "M208"),                     // 位址不齊
                Row("Z1", "M202", "M210", "M211"),                 // M202 已被 B1 紅燈使用
                Row("D1", "M212", "M212", "M214"),                 // 同站兩燈同位址
                null,
            }, out var skipped);

            Assert.AreEqual("B1", result.Single().StationNo);
            Assert.AreEqual(3, skipped.Count, "位址不齊、跨站重複、同站重複都要提示");
        }

        [TestMethod]
        public void BuildLampMappings_LampAddressEqualsSensorAddress_Skipped()
        {
            // code review W2：寫到 sensor bit 會讓 StockSensorLink 誤判清空庫位（即使該 sensor 停用也不可共用）
            var result = RackStatusOutputManager.BuildLampMappings(new[]
            {
                Row("B1", "M200", "M201", "M202", sensor: "M10"),
                Row("O1", "M10", "M204", "M205", sensor: "M11"),
            }, out var skipped);

            Assert.AreEqual("B1", result.Single().StationNo);
            StringAssert.Contains(skipped.Single(), "sensor");
        }

        [TestMethod]
        public void BuildLampMappings_WordDevice_Skipped()
        {
            var result = RackStatusOutputManager.BuildLampMappings(new[]
            {
                Row("B1", "D100", "M201", "M202"),     // D 為 word 裝置
                Row("O1", "L10", "B1F", "M205"),       // L、B（十六進位）可用
            }, out var skipped);

            Assert.AreEqual("O1", result.Single().StationNo);
            StringAssert.Contains(skipped.Single(), "bit 裝置");
        }

        [TestMethod]
        public void NormalizeAddress_LeadingZerosAndCase()
        {
            Assert.AreEqual("M200", PlcAddress.Normalize(" m0200 "));
            Assert.AreEqual("BA", PlcAddress.Normalize("B0a"));
            Assert.AreEqual("BF1", PlcAddress.Normalize("BF1"));
            Assert.AreEqual("SB10", PlcAddress.Normalize("SB010"));
            Assert.AreEqual("L5", PlcAddress.Normalize("L005"));
        }

        [TestMethod]
        public void BuildLampMappings_SameDeviceDifferentSpelling_TreatedAsDuplicate()
        {
            var result = RackStatusOutputManager.BuildLampMappings(new[]
            {
                Row("B1", "M200", "M201", "M202", sensor: "M10"),
                Row("O1", "M010", "M204", "M205"),     // M010＝B1 的 sensor M10
            }, out var skipped);

            Assert.AreEqual("B1", result.Single().StationNo);
            Assert.AreEqual(1, skipped.Count);
        }

        [TestMethod]
        public void ResolveLampMappings_TableMissing_EmptyWithoutRetry()
        {
            var load = RackStatusOutputBootstrap.ResolveLampMappings(() => throw new InvalidOperationException("208"), ex => true);

            Assert.IsFalse(load.Retry);
            Assert.AreEqual(0, load.Mappings.Count);
        }

        [TestMethod]
        public void ResolveLampMappings_ConnectionFailure_Retry()
        {
            var load = RackStatusOutputBootstrap.ResolveLampMappings(() => throw new InvalidOperationException("逾時"), ex => false);

            Assert.IsTrue(load.Retry);
            Assert.IsNull(load.Mappings);
        }

        [TestMethod]
        public void Start_NoEnabledLamps_StopsWithoutConnectingPlc()
        {
            bool plcCreated = false;
            var loaded = new ManualResetEventSlim(false);
            var sut = new RackStatusOutputManager(
                () => { loaded.Set(); return new LampMappingLoad(new List<LampMapping>(), "DB oPortPlc（全部停用）", false); },
                () => { plcCreated = true; return new SpyBitWriter(); },
                new FakeFlagReader(), null);

            sut.Start();
            Assert.IsTrue(loaded.Wait(5000), "loader 有被呼叫");
            for (int i = 0; i < 100 && sut.IsRunning; i++)
            {
                Thread.Sleep(50);   // 等執行緒自然結束（不靠 Stop 提前中斷）
            }

            Assert.IsFalse(sut.IsRunning, "0 站時執行緒自行結束");
            Assert.IsFalse(plcCreated, "沒有啟用點位時不連 PLC");
            sut.Stop();
        }

        [TestMethod]
        public void Start_LoaderRetryThenLamps_WritesAfterLoad()
        {
            var writer = new SpyBitWriter { SignalAddress = "M200" };
            var flags = new FakeFlagReader();
            flags.Set("B1", "0");
            int calls = 0;
            var sut = new RackStatusOutputManager(
                () => Interlocked.Increment(ref calls) == 1
                    ? new LampMappingLoad(null, "DB 連不上", retry: true)
                    : new LampMappingLoad(new List<LampMapping> { B1 }, "DB oPortPlc", false),
                () => writer, flags, null, pollIntervalMs: 50) { MappingRetryMs = 50 };

            sut.Start();
            bool written = writer.Signal.Wait(5000);
            sut.Stop();

            Assert.IsTrue(written, "載入後開始輸出綠燈");
            Assert.IsTrue(calls >= 2, "第一次失敗後有重試");
        }
    }
}
