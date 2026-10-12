using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Tests.TestInfrastructure;

namespace SCP.Tests.Controllers
{
    /// <summary>
    /// T7 R2、R3 人工派車後端檢查（ToDo/20260726_一廠AB棟1F新路線與邏輯，spec.md AC-2）
    /// 起點區域列在 AreaRules:DispatchRoutes 時，InsertoNeed 會檢查起點有貨、終點為設定目的區的空位；
    /// 不在 DispatchRoutes 的舊路線（A→K、L→B）維持原行為（不檢查）。
    /// R2：E（迅得2/3/4）→ Z（中光電C）；R3：B（迅得1）→ P（中光電B）。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("AB1F")]
    public class DispatchControllerManualDispatchTests
    {
        private static Dictionary<string, string?> Settings()
        {
            var settings = ScpTestDb.FactoryOneAreaRules();
            settings["AreaRules:DispatchRoutes:E:0"] = "Z";
            settings["AreaRules:DispatchRoutes:B:0"] = "P";
            settings["AreaRules:AreaNames:Z"] = "中光電C";
            settings["AreaRules:AreaNames:P"] = "中光電B";
            return settings;
        }

        private static Dictionary<string, string> Request(string begin, string end, string workOrder = "")
            => new Dictionary<string, string>
            {
                ["BegingStation"] = begin,
                ["EndStation"] = end,
                ["RackId"] = "",
                ["WorkOrder"] = workOrder,
            };

        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void InsertoNeed_R2SourceLoadedTargetEmpty_CreatesNeedWithSourceWorkOrder()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            db.AddPort("Z1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "Z1"));

            Assert.AreEqual(200, result.StatusCodeOf());
            var need = db.Needs().Single();
            Assert.AreEqual("E2", need.ObjStation);
            Assert.AreEqual("Z1", need.EndStation);
            Assert.AreEqual("WO-E2", need.WorkOrder);
            Assert.AreEqual("Web", need.TaskSource);
        }

        [TestMethod]
        public void InsertoNeed_R3FromB1ToP1_CreatesNeed()
        {
            using var db = new ScpTestDb();
            db.AddPort("B1", haveFlag: "3", workOrder: "WO-B1");
            db.AddPort("P1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("B1", "P1"));

            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("P1", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public void InsertoNeed_TargetOccupied_ReturnsBadRequestWithReason()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            db.AddPort("Z1", haveFlag: "3", workOrder: "WO-X");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "Z1"));

            Assert.AreEqual(400, result.StatusCodeOf());
            StringAssert.Contains(result.MessageOf(), "Z1");
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void InsertoNeed_TargetPendingAsEnd_ReturnsBadRequest()
        {
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "3", workOrder: "WO-E1");
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            db.AddPort("Z1");
            db.AddMission("E1", "Z1", okFlag: "R");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "Z1"));

            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void InsertoNeed_TargetReservedBgnToEnd_ReturnsBadRequest()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            db.AddPort("Z1", bgnToEnd: "E9");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "Z1"));

            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void InsertoNeed_TargetNotInConfiguredArea_ReturnsBadRequest()
        {
            // R2 只能送 Z，不能送 P
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            db.AddPort("P1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "P1"));

            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void InsertoNeed_NoTargetSelected_ReturnsAreaLabelMessage()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", ""));

            Assert.AreEqual(400, result.StatusCodeOf());
            StringAssert.Contains(result.MessageOf(), "Z區（中光電C）");
        }

        [TestMethod]
        public void InsertoNeed_SourceEmpty_ReturnsBadRequest()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "0");
            db.AddPort("Z1");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "Z1"));

            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void InsertoNeed_SourcePending_ReturnsBadRequest()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-E2");
            db.AddPort("Z1");
            db.AddPort("Z2");
            db.AddNeed("E2", "Z2", assignFlag: "");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("E2", "Z1"));

            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual(1, db.Needs().Count);
        }

        // ── 回歸：DispatchRoutes 以外的舊路線不受影響 ──────────────

        [TestMethod]
        public void InsertoNeed_LegacyAreaNotInDispatchRoutes_KeepsOldBehaviorWithoutChecks()
        {
            // A→K 不在 DispatchRoutes：即使終點已有貨也照舊寫入（重構前行為）
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "3", workOrder: "WO-A1");
            db.AddPort("K1", haveFlag: "3");
            var controller = db.CreateDispatchController(settings: Settings());

            var result = controller.InsertoNeed(Request("A1", "K1"));

            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual(1, db.Needs().Count);
        }
    }
}
