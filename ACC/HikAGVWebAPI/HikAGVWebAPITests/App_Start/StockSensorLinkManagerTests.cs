using System.Collections.Generic;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// 庫位 SENSOR 監控核心邏輯單元測試（對應工作計畫 Task 2）。
    /// 設計：level + 去抖 —— bit=OFF（平板不在）且軟體仍佔用（RackId / WorkOrder / HaveFlag 任一），
    ///       連續達 ConfirmCount 輪才清空。bit=ON、讀取失敗、軟體已空 → 計數歸 0。
    /// 以假件 IBitReader / IPortStateReader 餵值、spy IStockClearer 驗證清空次數與參數，不依賴真實 PLC / DB。
    /// </summary>
    [TestClass]
    public class StockSensorLinkManagerTests
    {
        #region 測試假件 / 工具

        private class FakeBitReader : IBitReader
        {
            public Dictionary<string, bool> Values { get; } = new Dictionary<string, bool>();
            public HashSet<string> FailAddresses { get; } = new HashSet<string>();

            public bool TryReadBit(string address, out bool on)
            {
                if (FailAddresses.Contains(address))
                {
                    on = false;
                    return false; // 讀取失敗
                }
                on = Values.TryGetValue(address, out var v) && v;
                return true;
            }
        }

        private class FakePortReader : IPortStateReader
        {
            public Dictionary<string, oPortModel> Ports { get; } = new Dictionary<string, oPortModel>();
            public oPortModel GetPort(string stationNo) => Ports.TryGetValue(stationNo, out var p) ? p : null;
        }

        private class SpyStockClearer : IStockClearer
        {
            public List<string> Cleared { get; } = new List<string>();
            public void ClearStock(string stationNo) => Cleared.Add(stationNo);
        }

        private static oPortModel Port(string rackId = "", string workOrder = "", string haveFlag = "0")
            => new oPortModel { RackId = rackId, WorkOrder = workOrder, HaveFlag = haveFlag };

        private static StockSensorLinkManager CreateSut(
            FakeBitReader reader, FakePortReader portReader, SpyStockClearer clearer,
            int confirmCount, params SensorMapping[] mappings)
        {
            // log 傳 null：實作需 null-safe（_log?.）
            return new StockSensorLinkManager(new List<SensorMapping>(mappings), reader, clearer,
                portReader, null, 1000, confirmCount);
        }

        #endregion 測試假件 / 工具

        #region IsOccupied 三欄位判斷

        [TestMethod]
        public void IsOccupied_RackId非空_視為佔用()
            => Assert.IsTrue(StockSensorLinkManager.IsOccupied(Port(rackId: "R001")));

        [TestMethod]
        public void IsOccupied_WorkOrder非空_視為佔用()
            => Assert.IsTrue(StockSensorLinkManager.IsOccupied(Port(workOrder: "WO123")));

        [TestMethod]
        public void IsOccupied_HaveFlag1_有料無工單_視為佔用()
            => Assert.IsTrue(StockSensorLinkManager.IsOccupied(Port(haveFlag: "1")));

        [TestMethod]
        public void IsOccupied_HaveFlag3_有料有工單_視為佔用()
            => Assert.IsTrue(StockSensorLinkManager.IsOccupied(Port(haveFlag: "3")));

        [TestMethod]
        public void IsOccupied_三者皆空且HaveFlag0_視為未佔用()
            => Assert.IsFalse(StockSensorLinkManager.IsOccupied(Port(haveFlag: "0")));

        [TestMethod]
        public void IsOccupied_HaveFlagE_視為未佔用()
            => Assert.IsFalse(StockSensorLinkManager.IsOccupied(Port(haveFlag: "E")));

        [TestMethod]
        public void IsOccupied_Null_視為未佔用()
            => Assert.IsFalse(StockSensorLinkManager.IsOccupied(null));

        #endregion IsOccupied 三欄位判斷

        #region bit=ON / 讀取失敗 / 未佔用 → 不清

        [TestMethod]
        public void ScanOnce_bitON_平板在位_不清()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 1, new SensorMapping("M10", "B1"));

            reader.Values["M10"] = true;                 // 平板在
            ports.Ports["B1"] = Port(rackId: "R001", haveFlag: "3");
            sut.ScanOnce();
            sut.ScanOnce();

            Assert.AreEqual(0, clearer.Cleared.Count);
        }

        [TestMethod]
        public void ScanOnce_bitOFF但軟體已空_不清()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 1, new SensorMapping("M10", "B1"));

            reader.Values["M10"] = false;                // 平板不在
            ports.Ports["B1"] = Port();                  // 軟體本來就空
            sut.ScanOnce();
            sut.ScanOnce();

            Assert.AreEqual(0, clearer.Cleared.Count);
        }

        [TestMethod]
        public void ScanOnce_讀取失敗_不清且計數歸零()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 2, new SensorMapping("M10", "B1"));
            ports.Ports["B1"] = Port(rackId: "R001");

            reader.Values["M10"] = false;
            sut.ScanOnce();                              // 計數 1
            reader.FailAddresses.Add("M10");
            sut.ScanOnce();                              // 讀取失敗 → 歸 0
            Assert.AreEqual(0, clearer.Cleared.Count);

            reader.FailAddresses.Clear();
            sut.ScanOnce();                              // 重新計數 1（非 2）→ 仍不清
            Assert.AreEqual(0, clearer.Cleared.Count);
            sut.ScanOnce();                              // 計數 2 → 清
            Assert.AreEqual(1, clearer.Cleared.Count);
            Assert.AreEqual("B1", clearer.Cleared[0]);
        }

        #endregion

        #region 去抖計數

        [TestMethod]
        public void ScanOnce_ConfirmCount1_OFF且佔用_當輪即清()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 1, new SensorMapping("M10", "B1"));

            reader.Values["M10"] = false;
            ports.Ports["B1"] = Port(workOrder: "WO1");
            sut.ScanOnce();

            Assert.AreEqual(1, clearer.Cleared.Count);
            Assert.AreEqual("B1", clearer.Cleared[0]);
        }

        [TestMethod]
        public void ScanOnce_ConfirmCount3_連續三輪OFF且佔用_第三輪才清一次()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 3, new SensorMapping("M10", "B1"));
            reader.Values["M10"] = false;
            ports.Ports["B1"] = Port(rackId: "R001", workOrder: "WO1", haveFlag: "3");

            sut.ScanOnce();                              // 1
            Assert.AreEqual(0, clearer.Cleared.Count);
            sut.ScanOnce();                              // 2
            Assert.AreEqual(0, clearer.Cleared.Count);
            sut.ScanOnce();                              // 3 → 清
            Assert.AreEqual(1, clearer.Cleared.Count);
            Assert.AreEqual("B1", clearer.Cleared[0]);
        }

        [TestMethod]
        public void ScanOnce_去抖中途bitON_計數重置_不誤清()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 3, new SensorMapping("M10", "B1"));
            ports.Ports["B1"] = Port(rackId: "R001");

            reader.Values["M10"] = false;
            sut.ScanOnce();                              // 1
            sut.ScanOnce();                              // 2
            reader.Values["M10"] = true;                 // 送料落位瞬間 ON
            sut.ScanOnce();                              // 歸 0
            reader.Values["M10"] = false;
            sut.ScanOnce();                              // 1
            sut.ScanOnce();                              // 2
            Assert.AreEqual(0, clearer.Cleared.Count);   // 還沒到 3，不清
            sut.ScanOnce();                              // 3 → 清
            Assert.AreEqual(1, clearer.Cleared.Count);
        }

        [TestMethod]
        public void ScanOnce_重啟殘留_開機即OFF且佔用_連續達標補清()
        {
            // 模擬 ACC 重啟：manager 全新、計數從 0；料早已被取走（OFF）、WorkOrder 殘留
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 3, new SensorMapping("M10", "B1"));
            reader.Values["M10"] = false;
            ports.Ports["B1"] = Port(workOrder: "WO殘留");

            sut.ScanOnce();
            sut.ScanOnce();
            sut.ScanOnce();                              // 第三輪補清

            Assert.AreEqual(1, clearer.Cleared.Count);
            Assert.AreEqual("B1", clearer.Cleared[0]);
        }

        [TestMethod]
        public void ScanOnce_清空後軟體變空_不再重複清()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 1, new SensorMapping("M10", "B1"));
            reader.Values["M10"] = false;
            ports.Ports["B1"] = Port(workOrder: "WO1");

            sut.ScanOnce();                              // 清一次
            Assert.AreEqual(1, clearer.Cleared.Count);

            ports.Ports["B1"] = Port();                  // 模擬 Update_oPortEmpty 後變空
            sut.ScanOnce();
            sut.ScanOnce();
            Assert.AreEqual(1, clearer.Cleared.Count);   // 不再清
        }

        #endregion 去抖計數

        #region 多庫位獨立

        [TestMethod]
        public void ScanOnce_多庫位_只清OFF且佔用者()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 1,
                new SensorMapping("M10", "B1"), new SensorMapping("M11", "B2"));

            reader.Values["M10"] = false;                // B1 平板不在、仍佔用 → 應清
            reader.Values["M11"] = true;                 // B2 平板在 → 不清
            ports.Ports["B1"] = Port(rackId: "R001");
            ports.Ports["B2"] = Port(rackId: "R002", haveFlag: "3");
            sut.ScanOnce();

            Assert.AreEqual(1, clearer.Cleared.Count);
            Assert.AreEqual("B1", clearer.Cleared[0]);
            CollectionAssert.DoesNotContain(clearer.Cleared, "B2");
        }

        #endregion 多庫位獨立

        #region ConfirmCount 防呆

        [TestMethod]
        public void ScanOnce_ConfirmCount給0_視為至少1輪即清()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var sut = CreateSut(reader, ports, clearer, 0, new SensorMapping("M10", "B1"));
            reader.Values["M10"] = false;
            ports.Ports["B1"] = Port(workOrder: "WO1");

            sut.ScanOnce();

            Assert.AreEqual(1, clearer.Cleared.Count);
        }

        #endregion ConfirmCount 防呆

        #region SensorMap 解析

        [TestMethod]
        public void ParseSensorMap_單筆_解析出位址與StationNo()
        {
            var result = StockSensorLinkManager.ParseSensorMap("M10:B1");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("M10", result[0].Address);
            Assert.AreEqual("B1", result[0].StationNo);
        }

        [TestMethod]
        public void ParseSensorMap_多筆逗號分隔_解析出多筆()
        {
            var result = StockSensorLinkManager.ParseSensorMap("M10:B1,M11:B2");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("M10", result[0].Address);
            Assert.AreEqual("B1", result[0].StationNo);
            Assert.AreEqual("M11", result[1].Address);
            Assert.AreEqual("B2", result[1].StationNo);
        }

        [TestMethod]
        public void ParseSensorMap_含空白與格式錯誤片段_略過()
        {
            var result = StockSensorLinkManager.ParseSensorMap(" M10:B1 , , M11 , M12:B3 ");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("M10", result[0].Address);
            Assert.AreEqual("B1", result[0].StationNo);
            Assert.AreEqual("M12", result[1].Address);
            Assert.AreEqual("B3", result[1].StationNo);
        }

        [TestMethod]
        public void ParseSensorMap_空字串_回空清單()
        {
            Assert.AreEqual(0, StockSensorLinkManager.ParseSensorMap("").Count);
            Assert.AreEqual(0, StockSensorLinkManager.ParseSensorMap(null).Count);
        }

        #endregion SensorMap 解析

        #region 中光電來源站 Sensor ON 寫 HaveFlag=1（T13，2026-10-12 定案）

        private class SpyStockRegistrar : IStockRegistrar
        {
            public List<string> Marked { get; } = new List<string>();
            public void MarkRackPresent(string stationNo) => Marked.Add(stationNo);
        }

        private class FakeSourceTaskStore : ISourceTaskStore
        {
            public HashSet<string> ActiveStations { get; } = new HashSet<string>();
            public HashSet<string> RunningStations { get; } = new HashSet<string>();
            public List<string> RunningQueries { get; } = new List<string>();
            public Dictionary<string, int> UndispatchedNeeds { get; } = new Dictionary<string, int>();
            public List<string> DeleteCalls { get; } = new List<string>();

            public bool HasActiveTask(string stationNo) => ActiveStations.Contains(stationNo);

            public bool HasRunningMission(string stationNo)
            {
                RunningQueries.Add(stationNo);
                return RunningStations.Contains(stationNo);
            }

            public int DeleteUndispatchedNeeds(string stationNo)
            {
                DeleteCalls.Add(stationNo);
                return UndispatchedNeeds.TryGetValue(stationNo, out var n) ? n : 0;
            }
        }

        private static StockSensorLinkManager CreateSourceSut(
            FakeBitReader reader, FakePortReader portReader, SpyStockClearer clearer,
            SpyStockRegistrar registrar, FakeSourceTaskStore taskStore, int confirmCount,
            string[] sensorOnStations, params SensorMapping[] mappings)
        {
            return new StockSensorLinkManager(new List<SensorMapping>(mappings), reader, clearer,
                portReader, null, 1000, confirmCount, registrar, taskStore, sensorOnStations);
        }

        [TestMethod]
        public void ScanOnce_來源站bitON且HaveFlag0_連續達ConfirmCount_寫HaveFlag1一次()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var registrar = new SpyStockRegistrar();
            var sut = CreateSourceSut(reader, ports, new SpyStockClearer(), registrar, new FakeSourceTaskStore(), 3,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            reader.Values["M11"] = true;
            ports.Ports["D1"] = Port();
            sut.ScanOnce();
            sut.ScanOnce();
            Assert.AreEqual(0, registrar.Marked.Count, "未達 ConfirmCount 不可寫");

            sut.ScanOnce();
            CollectionAssert.AreEqual(new[] { "D1" }, registrar.Marked);
        }

        [TestMethod]
        public void ScanOnce_來源站已是HaveFlag1或3_不重複寫()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var registrar = new SpyStockRegistrar();
            var sut = CreateSourceSut(reader, ports, new SpyStockClearer(), registrar, new FakeSourceTaskStore(), 1,
                new[] { "D1", "O1" }, new SensorMapping("M11", "D1"), new SensorMapping("M12", "O1"));

            reader.Values["M11"] = true;
            reader.Values["M12"] = true;
            ports.Ports["D1"] = Port(haveFlag: "1");
            ports.Ports["O1"] = Port(workOrder: "WO1", haveFlag: "3");
            sut.ScanOnce();
            sut.ScanOnce();

            Assert.AreEqual(0, registrar.Marked.Count);
        }

        [TestMethod]
        public void ScanOnce_非來源站bitON_維持既有行為不寫()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var registrar = new SpyStockRegistrar();
            var sut = CreateSourceSut(reader, ports, new SpyStockClearer(), registrar, new FakeSourceTaskStore(), 1,
                new[] { "D1" }, new SensorMapping("M10", "B1"));

            reader.Values["M10"] = true;
            ports.Ports["B1"] = Port();
            sut.ScanOnce();

            Assert.AreEqual(0, registrar.Marked.Count);
        }

        [TestMethod]
        public void ScanOnce_來源站有進行中任務_outbin後sensor未熄_不寫()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var registrar = new SpyStockRegistrar();
            var tasks = new FakeSourceTaskStore();
            var sut = CreateSourceSut(reader, ports, new SpyStockClearer(), registrar, tasks, 1,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            reader.Values["M11"] = true;
            ports.Ports["D1"] = Port();          // outbin 已清空
            tasks.ActiveStations.Add("D1");      // oMission 尚未 end
            sut.ScanOnce();

            Assert.AreEqual(0, registrar.Marked.Count);
        }

        [TestMethod]
        public void ScanOnce_來源站ON去抖中途讀取失敗_計數重置()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var registrar = new SpyStockRegistrar();
            var sut = CreateSourceSut(reader, ports, new SpyStockClearer(), registrar, new FakeSourceTaskStore(), 2,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            reader.Values["M11"] = true;
            ports.Ports["D1"] = Port();
            sut.ScanOnce();                      // 1
            reader.FailAddresses.Add("M11");
            sut.ScanOnce();                      // 失敗 → 歸 0
            reader.FailAddresses.Clear();
            sut.ScanOnce();                      // 1
            Assert.AreEqual(0, registrar.Marked.Count);

            sut.ScanOnce();                      // 2 → 寫
            Assert.AreEqual(1, registrar.Marked.Count);
        }

        [TestMethod]
        public void ScanOnce_來源站ON去抖中途OFF_計數重置()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var registrar = new SpyStockRegistrar();
            var sut = CreateSourceSut(reader, ports, new SpyStockClearer(), registrar, new FakeSourceTaskStore(), 2,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            ports.Ports["D1"] = Port();
            reader.Values["M11"] = true;
            sut.ScanOnce();
            reader.Values["M11"] = false;
            sut.ScanOnce();
            reader.Values["M11"] = true;
            sut.ScanOnce();

            Assert.AreEqual(0, registrar.Marked.Count);
        }

        #endregion 中光電來源站 Sensor ON 寫 HaveFlag=1

        #region 中光電來源站 Sensor OFF 清空時處理已建任務（T13，2026-10-12 定案）

        [TestMethod]
        public void ScanOnce_來源站OFF清空_刪除未派出oNeed()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var tasks = new FakeSourceTaskStore();
            var sut = CreateSourceSut(reader, ports, clearer, new SpyStockRegistrar(), tasks, 1,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            reader.Values["M11"] = false;
            ports.Ports["D1"] = Port(workOrder: "WO1", haveFlag: "3");
            tasks.UndispatchedNeeds["D1"] = 1;
            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "D1" }, clearer.Cleared);
            CollectionAssert.AreEqual(new[] { "D1" }, tasks.DeleteCalls);
        }

        [TestMethod]
        public void ScanOnce_來源站OFF清空且任務已派出_仍清空庫位不取消任務()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var tasks = new FakeSourceTaskStore();
            var sut = CreateSourceSut(reader, ports, clearer, new SpyStockRegistrar(), tasks, 1,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            reader.Values["M11"] = false;
            ports.Ports["D1"] = Port(haveFlag: "1");
            tasks.ActiveStations.Add("D1");      // 已進 oMission：只記 Error，由人員到 RCS 取消
            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "D1" }, clearer.Cleared);
        }

        [TestMethod]
        public void ScanOnce_來源站OFF清空且AGV執行中_視為正常取貨並查詢執行狀態()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var tasks = new FakeSourceTaskStore();
            var sut = CreateSourceSut(reader, ports, clearer, new SpyStockRegistrar(), tasks, 1,
                new[] { "D1" }, new SensorMapping("M11", "D1"));

            reader.Values["M11"] = false;
            ports.Ports["D1"] = Port(workOrder: "WO1", haveFlag: "3");
            tasks.ActiveStations.Add("D1");
            tasks.RunningStations.Add("D1");   // AGV 已頂起貨架、outbin 尚未回報
            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "D1" }, clearer.Cleared);
            CollectionAssert.AreEqual(new[] { "D1" }, tasks.RunningQueries, "須先判斷是否為 AGV 執行中，再決定是否記 Error");
        }

        [TestMethod]
        public void ScanOnce_非來源站OFF清空_不動oNeed()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var clearer = new SpyStockClearer();
            var tasks = new FakeSourceTaskStore();
            var sut = CreateSourceSut(reader, ports, clearer, new SpyStockRegistrar(), tasks, 1,
                new[] { "D1" }, new SensorMapping("M10", "B1"));

            reader.Values["M10"] = false;
            ports.Ports["B1"] = Port(workOrder: "WO1", haveFlag: "3");
            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "B1" }, clearer.Cleared);
            Assert.AreEqual(0, tasks.DeleteCalls.Count);
        }

        [TestMethod]
        public void ScanOnce_OFF清空_觸發StockCleared供中光電release()
        {
            var reader = new FakeBitReader();
            var ports = new FakePortReader();
            var sut = CreateSut(reader, ports, new SpyStockClearer(), 1, new SensorMapping("M11", "D1"));
            var raised = new List<string>();
            sut.StockCleared += raised.Add;

            reader.Values["M11"] = false;
            ports.Ports["D1"] = Port(haveFlag: "1");
            sut.ScanOnce();

            CollectionAssert.AreEqual(new[] { "D1" }, raised);
        }

        [TestMethod]
        public void ParseStationList_逗號分隔含空白_去除空白與空片段()
        {
            CollectionAssert.AreEqual(new[] { "D1", "O1" }, StockSensorLinkManager.ParseStationList(" D1, ,O1 "));
            Assert.AreEqual(0, StockSensorLinkManager.ParseStationList(null).Count);
        }

        #endregion 中光電來源站 Sensor OFF 清空時處理已建任務
    }
}
