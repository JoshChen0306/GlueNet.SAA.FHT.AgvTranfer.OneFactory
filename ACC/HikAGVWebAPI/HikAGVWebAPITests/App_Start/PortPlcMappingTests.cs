using System;
using System.Collections.Generic;
using System.Linq;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// T29 PLC 點位表移入 DB（ToDo/20260726_一廠AB棟1F新路線與邏輯，spec.md AC-5a）。
    /// oPortPlc 一站一列（sensor + 三色燈位址）。規則（code review C1 修正）：
    ///   oPortPlc 有任何資料列 → DB 為準（全部停用就是不監控）；資料表不存在或 0 列 → 退回設定檔 SensorMap；
    ///   其他讀取失敗（DB 連不上）→ 重試，不用設定檔舊點位頂替。點位在掃描執行緒載入，不阻塞 ACC 啟動。
    /// </summary>
    [TestClass]
    public class PortPlcMappingTests
    {
        private static oPortPlcModel Row(string station, string sensor, string useFlag = "Y")
            => new oPortPlcModel { StationNo = station, SensorAddress = sensor, SensorUseFlag = useFlag };

        private static bool NeverMissing(Exception ex) => false;
        private static bool AlwaysMissing(Exception ex) => true;

        // ── BuildSensorMappings ─────────────────────────────

        [TestMethod]
        public void BuildSensorMappings_EnabledRows_MapsAddressToStation()
        {
            var result = StockSensorLinkManager.BuildSensorMappings(new[] { Row("B1", "M10"), Row("O1", "M11") });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("M10", result[0].Address);
            Assert.AreEqual("B1", result[0].StationNo);
            Assert.AreEqual("M11", result[1].Address);
            Assert.AreEqual("O1", result[1].StationNo);
        }

        [TestMethod]
        public void BuildSensorMappings_DisabledOrBlank_Skipped()
        {
            var result = StockSensorLinkManager.BuildSensorMappings(new[]
            {
                Row("B1", "M10"),
                Row("O1", "M11", useFlag: "N"),   // 停用
                Row("P1", ""),                    // 無 sensor（例：只有三色燈）
                Row("Z1", null),
                Row("", "M13"),                   // 站號空白
                null,
            });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("B1", result[0].StationNo);
        }

        [TestMethod]
        public void BuildSensorMappings_TrimsAndUseFlagCaseInsensitive()
        {
            var result = StockSensorLinkManager.BuildSensorMappings(new[] { Row(" D1 ", " M14 ", useFlag: "y") });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("M14", result[0].Address);
            Assert.AreEqual("D1", result[0].StationNo);
        }

        [TestMethod]
        public void BuildSensorMappings_DuplicateAddress_KeepsFirstAndReportsSkipped()
        {
            // code review W4：一個 bit 的 OFF 不可同時清兩個庫位
            var result = StockSensorLinkManager.BuildSensorMappings(new[] { Row("B1", "M10"), Row("O1", "m10") }, out var skipped);

            Assert.AreEqual("B1", result.Single().StationNo);
            Assert.AreEqual(1, skipped.Count);
            StringAssert.Contains(skipped[0], "O1");
        }

        // ── ResolveSensorMappings ───────────────────────────

        [TestMethod]
        public void ResolveSensorMappings_DbHasRows_UsesDbNotConfig()
        {
            var load = StockSensorLinkBootstrap.ResolveSensorMappings(
                () => new List<oPortPlcModel> { Row("O1", "M11") }, "M10:B1", NeverMissing);

            Assert.AreEqual("O1", load.Mappings.Single().StationNo);
            Assert.IsFalse(load.FellBackToConfig);
            Assert.IsFalse(load.Retry);
            StringAssert.Contains(load.Source, "oPortPlc");
        }

        [TestMethod]
        public void ResolveSensorMappings_DbRowsAllDisabled_NoMonitoringAndNoConfigFallback()
        {
            // code review C1：現場把壞掉的 B1 sensor 停用，重啟後不可用設定檔的 M10 重新監控（會誤清有料庫位）
            var load = StockSensorLinkBootstrap.ResolveSensorMappings(
                () => new List<oPortPlcModel> { Row("B1", "M10", useFlag: "N") }, "M10:B1", NeverMissing);

            Assert.AreEqual(0, load.Mappings.Count);
            Assert.IsFalse(load.FellBackToConfig);
            Assert.IsFalse(load.Retry);
        }

        [TestMethod]
        public void ResolveSensorMappings_DbEmpty_FallsBackToConfig()
        {
            var load = StockSensorLinkBootstrap.ResolveSensorMappings(
                () => new List<oPortPlcModel>(), "M10:B1", NeverMissing);

            Assert.AreEqual("B1", load.Mappings.Single().StationNo);
            Assert.IsTrue(load.FellBackToConfig);
            StringAssert.Contains(load.Source, "SensorMap");
        }

        [TestMethod]
        public void ResolveSensorMappings_TableMissing_FallsBackToConfig()
        {
            // 升級過渡期：現場尚未執行 Deploy_oPortPlc_ABBuilding1F.sql
            var load = StockSensorLinkBootstrap.ResolveSensorMappings(
                () => throw new InvalidOperationException("Invalid object name 'oPortPlc'"), "M10:B1", AlwaysMissing);

            Assert.AreEqual("B1", load.Mappings.Single().StationNo);
            Assert.IsTrue(load.FellBackToConfig);
            StringAssert.Contains(load.Source, "Deploy_oPortPlc");
        }

        [TestMethod]
        public void ResolveSensorMappings_ConnectionFailure_RetryWithoutConfigFallback()
        {
            // code review C1：DB 暫時連不上時不可用設定檔舊點位頂替（現場可能已改過 DB 位址）
            var load = StockSensorLinkBootstrap.ResolveSensorMappings(
                () => throw new InvalidOperationException("SQL 查詢操作失敗: 連線逾時"), "M10:B1", NeverMissing);

            Assert.IsTrue(load.Retry);
            Assert.IsNull(load.Mappings);
            StringAssert.Contains(load.Source, "連線逾時");
        }

        [TestMethod]
        public void ResolveSensorMappings_DuplicateAddress_ReportedAsWarning()
        {
            var load = StockSensorLinkBootstrap.ResolveSensorMappings(
                () => new List<oPortPlcModel> { Row("B1", "M10"), Row("O1", "M10") }, "", NeverMissing);

            Assert.AreEqual(1, load.Mappings.Count);
            Assert.AreEqual(1, load.Warnings.Count);
        }

        [TestMethod]
        public void IsTableMissing_NonSqlException_False()
        {
            Assert.IsFalse(StockSensorLinkBootstrap.IsTableMissing(new Exception("x", new InvalidOperationException("y"))));
        }

        // ── Manager 背景載入點位 ─────────────────────────────

        private class FakeBitReader : IBitReader
        {
            public bool TryReadBit(string address, out bool on) { on = true; return true; }
        }

        private static StockSensorLinkManager CreateWithLoader(Func<SensorMappingLoad> loader, Func<IBitReader> readerFactory = null)
            => new StockSensorLinkManager(loader, readerFactory ?? (() => new FakeBitReader()), null, null, null);

        [TestMethod]
        public void TryLoadMappings_RetryThenSuccess_AppliesMappingsAfterLoad()
        {
            var results = new Queue<SensorMappingLoad>(new[]
            {
                new SensorMappingLoad(null, "DB 連不上", false, retry: true),
                new SensorMappingLoad(new List<SensorMapping> { new SensorMapping("M10", "B1") }, "DB oPortPlc", false, false),
            });
            var sut = CreateWithLoader(() => results.Dequeue());

            Assert.IsFalse(sut.TryLoadMappings());
            Assert.AreEqual(ArrivalWatchResult.NoSensor, sut.WatchArrival("B1", "T1", "L1", DateTime.Now), "載入前視為無 sensor");

            Assert.IsTrue(sut.TryLoadMappings());
            Assert.AreEqual(ArrivalWatchResult.Watching, sut.WatchArrival("B1", "T1", "L1", DateTime.Now));
        }

        [TestMethod]
        public void TryLoadMappings_LoaderThrows_ReturnsFalseForRetry()
        {
            var sut = CreateWithLoader(() => throw new InvalidOperationException("boom"));

            Assert.IsFalse(sut.TryLoadMappings());
        }

        [TestMethod]
        public void Stop_WhileLoaderKeepsRetrying_EndsWithoutConnectingPlc()
        {
            bool plcCreated = false;
            int calls = 0;
            var sut = CreateWithLoader(
                () => { calls++; return new SensorMappingLoad(null, "DB 連不上", false, retry: true); },
                () => { plcCreated = true; return new FakeBitReader(); });
            sut.MappingRetryMs = 50;

            sut.Start();
            System.Threading.Thread.Sleep(300);
            sut.Stop();

            Assert.IsTrue(calls >= 1, "有嘗試載入");
            Assert.IsFalse(plcCreated, "點位未載入前不連 PLC");
            Assert.AreEqual(ArrivalWatchResult.NoSensor, sut.WatchArrival("B1", "T1", "L1", DateTime.Now));
        }

        [TestMethod]
        public void Start_NoEnabledMappings_StopsWithoutConnectingPlc()
        {
            bool plcCreated = false;
            var sut = CreateWithLoader(
                () => new SensorMappingLoad(new List<SensorMapping>(), "DB oPortPlc（全部停用）", false, false),
                () => { plcCreated = true; return new FakeBitReader(); });

            sut.Start();
            sut.Stop();   // Join 等執行緒結束

            Assert.IsFalse(plcCreated, "沒有啟用點位時不連 PLC");
            Assert.AreEqual(ArrivalWatchResult.NoSensor, sut.WatchArrival("B1", "T1", "L1", DateTime.Now));
        }
    }
}
