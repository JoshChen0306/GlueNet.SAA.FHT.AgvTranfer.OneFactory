using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Tests.TestInfrastructure;

namespace SCP.Tests.Controllers
{
    /// <summary>
    /// T27 加工完成標記：RegisterLot 附加 ^DONE、InsertoNeed 與畫面資料移除 ^DONE
    /// （ToDo/20260726_一廠AB棟1F新路線與邏輯，spec.md AC-3a）
    /// 加工站區域 = AreaRules:ProcessDoneAreas（一廠為 E＝迅得2/3/4）。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("AB1F")]
    public class DispatchControllerProcessDoneTests
    {
        private static Dictionary<string, string?> Settings()
        {
            var settings = ScpTestDb.FactoryOneAreaRules();
            settings["AreaRules:RegisterAreas:2"] = "E";
            settings["AreaRules:ProcessDoneAreas:0"] = "E";
            settings["AreaRules:DispatchRoutes:E:0"] = "Z";
            return settings;
        }

        private static Dictionary<string, string> LotRequest(string stationNo, string workOrder)
            => new Dictionary<string, string>
            {
                ["stationNo"] = stationNo,
                ["workOrder"] = workOrder,
                ["rackId"] = "-1",
                ["isVcutMaterial"] = "false",
            };

        [TestMethod]
        public void RegisterLot_ProcessDoneArea_AppendsDoneMarker()
        {
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.RegisterLot(LotRequest("E1", "WO-NEW"));

            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("3", db.Port("E1").HaveFlag);
            Assert.AreEqual("WO-NEW^DONE", db.Port("E1").WorkOrder);
        }

        [TestMethod]
        public void RegisterLot_ProcessDoneAreaInputAlreadyMarked_DoesNotAppendTwice()
        {
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "3", workOrder: "WO-NEW^DONE");
            var controller = db.CreateDispatchController(settings: Settings());

            controller.RegisterLot(LotRequest("E1", "WO-NEW^DONE"));

            Assert.AreEqual("WO-NEW^DONE", db.Port("E1").WorkOrder);
        }

        [TestMethod]
        public void RegisterLot_ProcessDoneAreaWorkOrderOnlyMarker_ReturnsBadRequest()
        {
            // 掃到的條碼剛好是 ^DONE：移除標記後沒有工單，不可登記（否則 R2 會派出無工單任務，中光電收不到 Mapping）
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.RegisterLot(LotRequest("E1", "^DONE"));

            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("請輸入工單條碼", result.MessageOf());
            Assert.AreEqual("1", db.Port("E1").HaveFlag);
        }

        [TestMethod]
        public void RegisterLot_NotProcessDoneArea_KeepsWorkOrderUnchanged()
        {
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "0");
            var controller = db.CreateDispatchController(settings: Settings());

            controller.RegisterLot(LotRequest("A1", "WO-A"));

            Assert.AreEqual("WO-A", db.Port("A1").WorkOrder);
        }

        [TestMethod]
        public void InsertoNeed_SourceWorkOrderMarked_NeedWorkOrderStripped()
        {
            // 人員派發備援 E→Z：工單由後端讀起點 oPort，寫入 oNeed 前須移除 ^DONE（否則會帶到中光電 Mapping）
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2^DONE");
            db.AddPort("Z1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(new Dictionary<string, string>
            {
                ["BegingStation"] = "E2",
                ["EndStation"] = "Z1",
                ["RackId"] = "",
                ["WorkOrder"] = "",
            });

            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("WO-E2", db.Needs().Single().WorkOrder);
        }

        [TestMethod]
        public void GetAllStations_MarkedWorkOrder_ReturnedWithoutMarker()
        {
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "3", workOrder: "WO-E1^DONE");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = (JsonResult)controller.GetAllStations();

            var json = JsonSerializer.Serialize(result.Value);
            StringAssert.Contains(json, "WO-E1");
            Assert.IsFalse(json.Contains("DONE"), "畫面資料不可帶出 ^DONE：" + json);
        }
    }
}
