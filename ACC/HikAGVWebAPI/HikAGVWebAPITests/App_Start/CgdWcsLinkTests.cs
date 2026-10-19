using System;
using System.Collections.Generic;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// T25 中光電儲位 API 介接（ToDo/20260726_一廠AB棟1F新路線與邏輯，`中光電儲位API介接流程.md`）。
    /// 以假件 ICgdWcsClient 驗證：release／Mapping 佇列與重試（5s／15s／60s，三次失敗放棄）、
    /// R1 來源站補資料（HaveFlag=1 → status → HaveFlag=3＋WorkOrder=itemCode）。不連中光電。
    /// </summary>
    [TestClass]
    public class CgdWcsLinkTests
    {
        #region 測試假件 / 工具

        private class FakeClient : ICgdWcsClient
        {
            public Queue<CgdWcsResult> Responses { get; } = new Queue<CgdWcsResult>();
            public CgdWcsResult Default { get; set; } = CgdWcsResult.FromCode(0, "OK");
            public List<string> Calls { get; } = new List<string>();

            private CgdWcsResult Next() => Responses.Count > 0 ? Responses.Dequeue() : Default;

            public CgdWcsResult GetStatus(string binCode)
            {
                Calls.Add($"status:{binCode}");
                return Next();
            }

            public CgdWcsResult Mapping(string binCode, string itemCode)
            {
                Calls.Add($"mapping:{binCode}:{itemCode}");
                return Next();
            }

            public CgdWcsResult Release(string binCode)
            {
                Calls.Add($"release:{binCode}");
                return Next();
            }
        }

        private class FakePortReader : IPortStateReader
        {
            public Dictionary<string, oPortModel> Ports { get; } = new Dictionary<string, oPortModel>();
            public oPortModel GetPort(string stationNo) => Ports.TryGetValue(stationNo, out var p) ? p : null;
        }

        private class SpyFiller : IMaterialFiller
        {
            public List<string> Filled { get; } = new List<string>();
            public bool Result { get; set; } = true;

            public bool FillMaterial(string stationNo, string itemCode)
            {
                Filled.Add($"{stationNo}:{itemCode}");
                return Result;
            }
        }

        private DateTime _now = new DateTime(2026, 10, 12, 8, 0, 0);
        private FakeClient _client;
        private FakePortReader _ports;
        private SpyFiller _filler;

        [TestInitialize]
        public void Init()
        {
            _now = new DateTime(2026, 10, 12, 8, 0, 0);
            _client = new FakeClient();
            _ports = new FakePortReader();
            _filler = new SpyFiller();
        }

        private CgdWcsLink CreateSut()
        {
            var binMap = new Dictionary<string, string>
            {
                { "O1", "BIN-A" }, { "P1", "BIN-B" }, { "Z1", "BIN-C" }, { "D1", "BIN-D" }
            };
            return new CgdWcsLink(_client, binMap, new[] { "D1" }, _ports, _filler, null,
                new[] { 5, 15, 60 }, () => _now);
        }

        private void Advance(int seconds) => _now = _now.AddSeconds(seconds);

        #endregion 測試假件 / 工具

        #region release / Mapping 佇列

        [TestMethod]
        public void EnqueueRelease_中光電站_送出release並移出佇列()
        {
            var sut = CreateSut();

            Assert.IsTrue(sut.EnqueueRelease("D1"));
            sut.ProcessDue();

            CollectionAssert.AreEqual(new[] { "release:BIN-D" }, _client.Calls);
            Assert.AreEqual(0, sut.PendingCount);
        }

        [TestMethod]
        public void EnqueueRelease_非中光電站_不排入()
        {
            var sut = CreateSut();

            Assert.IsFalse(sut.EnqueueRelease("E1"));
            sut.ProcessDue();

            Assert.AreEqual(0, _client.Calls.Count);
        }

        [TestMethod]
        public void EnqueueMapping_帶WorkOrder為itemCode()
        {
            var sut = CreateSut();

            Assert.IsTrue(sut.EnqueueMapping("Z1", "WO-001"));
            sut.ProcessDue();

            CollectionAssert.AreEqual(new[] { "mapping:BIN-C:WO-001" }, _client.Calls);
        }

        /// <summary>T27 防呆：SCP 建任務時已移除 ^DONE，萬一殘留也不可送給中光電。</summary>
        [TestMethod]
        public void EnqueueMapping_工單帶DONE標記_itemCode移除標記()
        {
            var sut = CreateSut();

            Assert.IsTrue(sut.EnqueueMapping("Z1", "WO-001^DONE"));
            sut.ProcessDue();

            CollectionAssert.AreEqual(new[] { "mapping:BIN-C:WO-001" }, _client.Calls);
        }

        [TestMethod]
        public void EnqueueMapping_WorkOrder空_不排入()
        {
            var sut = CreateSut();

            Assert.IsFalse(sut.EnqueueMapping("Z1", ""));
            Assert.AreEqual(0, sut.PendingCount);
        }

        [TestMethod]
        public void ProcessDue_回code2_依5s15s60s重試_第四次失敗後放棄()
        {
            _client.Default = CgdWcsResult.FromCode(2, "ERROR");
            var sut = CreateSut();
            sut.EnqueueRelease("O1");

            sut.ProcessDue();                    // 第 1 次
            Assert.AreEqual(1, _client.Calls.Count);

            Advance(4); sut.ProcessDue();        // 未到 5s
            Assert.AreEqual(1, _client.Calls.Count);
            Advance(1); sut.ProcessDue();        // 5s → 第 2 次
            Assert.AreEqual(2, _client.Calls.Count);

            Advance(14); sut.ProcessDue();
            Assert.AreEqual(2, _client.Calls.Count);
            Advance(1); sut.ProcessDue();        // 15s → 第 3 次
            Assert.AreEqual(3, _client.Calls.Count);

            Advance(60); sut.ProcessDue();       // 60s → 第 4 次
            Assert.AreEqual(4, _client.Calls.Count);
            Assert.AreEqual(0, sut.PendingCount, "三次重試都失敗後放棄（記 Error）");

            Advance(600); sut.ProcessDue();
            Assert.AreEqual(4, _client.Calls.Count);
        }

        [TestMethod]
        public void ProcessDue_連線失敗或逾時_視同code2重試()
        {
            _client.Responses.Enqueue(CgdWcsResult.Unreachable("timeout"));
            _client.Responses.Enqueue(CgdWcsResult.FromCode(0, "OK"));
            var sut = CreateSut();
            sut.EnqueueMapping("P1", "WO-9");

            sut.ProcessDue();
            Assert.AreEqual(1, sut.PendingCount);
            Advance(5); sut.ProcessDue();

            Assert.AreEqual(2, _client.Calls.Count);
            Assert.AreEqual(0, sut.PendingCount);
        }

        [TestMethod]
        public void ProcessDue_Mapping回code1已佔用_不重試()
        {
            _client.Default = CgdWcsResult.FromCode(1, "occupied");
            var sut = CreateSut();
            sut.EnqueueMapping("Z1", "WO-1");

            sut.ProcessDue();
            Advance(120); sut.ProcessDue();

            Assert.AreEqual(1, _client.Calls.Count);
            Assert.AreEqual(0, sut.PendingCount);
        }

        [TestMethod]
        public void ProcessDue_release回code1已釋放_視為成功()
        {
            _client.Default = CgdWcsResult.FromCode(1, "released");
            var sut = CreateSut();
            sut.EnqueueRelease("D1");

            sut.ProcessDue();
            Advance(120); sut.ProcessDue();

            Assert.AreEqual(1, _client.Calls.Count);
            Assert.AreEqual(0, sut.PendingCount);
        }

        [TestMethod]
        public void ProcessDue_一筆重試中_不阻擋其他筆()
        {
            _client.Responses.Enqueue(CgdWcsResult.FromCode(2, "ERROR"));
            var sut = CreateSut();
            sut.EnqueueRelease("D1");
            sut.ProcessDue();                    // D1 失敗，等 5s

            sut.EnqueueMapping("Z1", "WO-2");
            sut.ProcessDue();                    // Z1 立即送出

            CollectionAssert.AreEqual(new[] { "release:BIN-D", "mapping:BIN-C:WO-2" }, _client.Calls);
        }

        [TestMethod]
        public void ProcessDue_同一儲位release重試中_後到的Mapping等release完成才送()
        {
            _client.Responses.Enqueue(CgdWcsResult.FromCode(2, "ERROR"));   // release 第 1 次失敗
            var sut = CreateSut();
            sut.EnqueueRelease("P1");
            sut.ProcessDue();

            sut.EnqueueMapping("P1", "WO-NEW");                             // 新貨放上 P1
            sut.ProcessDue();
            CollectionAssert.AreEqual(new[] { "release:BIN-B" }, _client.Calls, "release 未完成前不可送 Mapping");

            Advance(5); sut.ProcessDue();                                   // release 重試成功
            sut.ProcessDue();                                               // 接著送 Mapping

            CollectionAssert.AreEqual(new[] { "release:BIN-B", "release:BIN-B", "mapping:BIN-B:WO-NEW" }, _client.Calls);
            Assert.AreEqual(0, sut.PendingCount);
        }

        [TestMethod]
        public void Stop_尚有未送出指令_清空佇列()
        {
            _client.Default = CgdWcsResult.FromCode(2, "ERROR");
            var sut = CreateSut();
            sut.EnqueueRelease("D1");
            sut.ProcessDue();

            sut.Stop();

            Assert.AreEqual(0, sut.PendingCount);
        }

        #endregion release / Mapping 佇列

        #region R1 來源站補資料（status）

        [TestMethod]
        public void FillStatusOnce_D1為HaveFlag1_status成功_寫回工單()
        {
            _ports.Ports["D1"] = new oPortModel { HaveFlag = "1" };
            _client.Default = CgdWcsResult.FromCode(0, "OK", "ITEM-123");
            var sut = CreateSut();

            sut.FillStatusOnce();

            CollectionAssert.AreEqual(new[] { "status:BIN-D" }, _client.Calls);
            CollectionAssert.AreEqual(new[] { "D1:ITEM-123" }, _filler.Filled);
        }

        [TestMethod]
        public void FillStatusOnce_寫回失敗_下一輪再查status()
        {
            _ports.Ports["D1"] = new oPortModel { HaveFlag = "1" };
            _client.Default = CgdWcsResult.FromCode(0, "OK", "ITEM-123");
            _filler.Result = false;
            var sut = CreateSut();

            sut.FillStatusOnce();
            sut.FillStatusOnce();

            Assert.AreEqual(2, _client.Calls.Count);
        }

        [TestMethod]
        public void FillStatusOnce_status回code2_不寫回_下一輪再查()
        {
            _ports.Ports["D1"] = new oPortModel { HaveFlag = "1" };
            _client.Default = CgdWcsResult.FromCode(2, "ERROR");
            var sut = CreateSut();

            sut.FillStatusOnce();
            sut.FillStatusOnce();

            Assert.AreEqual(2, _client.Calls.Count);
            Assert.AreEqual(0, _filler.Filled.Count);
        }

        [TestMethod]
        public void FillStatusOnce_itemCode空_不寫回()
        {
            _ports.Ports["D1"] = new oPortModel { HaveFlag = "1" };
            _client.Default = CgdWcsResult.FromCode(0, "OK", "");
            var sut = CreateSut();

            sut.FillStatusOnce();

            Assert.AreEqual(0, _filler.Filled.Count);
        }

        [TestMethod]
        public void FillStatusOnce_連線失敗_不寫回不拋例外()
        {
            _ports.Ports["D1"] = new oPortModel { HaveFlag = "1" };
            _client.Default = CgdWcsResult.Unreachable("timeout");
            var sut = CreateSut();

            sut.FillStatusOnce();

            Assert.AreEqual(0, _filler.Filled.Count);
        }

        [TestMethod]
        public void FillStatusOnce_D1為HaveFlag0或3_不呼叫status()
        {
            var sut = CreateSut();

            _ports.Ports["D1"] = new oPortModel { HaveFlag = "0" };
            sut.FillStatusOnce();
            _ports.Ports["D1"] = new oPortModel { HaveFlag = "3", WorkOrder = "ITEM-1" };
            sut.FillStatusOnce();

            Assert.AreEqual(0, _client.Calls.Count);
        }

        [TestMethod]
        public void FillStatusOnce_O1空盤不在StatusStations_不呼叫status()
        {
            _ports.Ports["O1"] = new oPortModel { HaveFlag = "1" };
            var sut = CreateSut();

            sut.FillStatusOnce();

            Assert.AreEqual(0, _client.Calls.Count);
        }

        #endregion R1 來源站補資料（status）

        #region 設定解析

        [TestMethod]
        public void ParseBinCodeMap_含空白與錯誤片段_略過()
        {
            var map = CgdWcsLink.ParseBinCodeMap(" D1:BIN-D , bad, O1:BIN-A,:x ");

            Assert.AreEqual(2, map.Count);
            Assert.AreEqual("BIN-D", map["D1"]);
            Assert.AreEqual("BIN-A", map["O1"]);
        }

        [TestMethod]
        public void ParseRetryDelays_空值用預設5_15_60()
        {
            CollectionAssert.AreEqual(new[] { 5, 15, 60 }, CgdWcsLink.ParseRetryDelays(null));
            CollectionAssert.AreEqual(new[] { 1, 2 }, CgdWcsLink.ParseRetryDelays("1, 2"));
        }

        #endregion 設定解析
    }
}
