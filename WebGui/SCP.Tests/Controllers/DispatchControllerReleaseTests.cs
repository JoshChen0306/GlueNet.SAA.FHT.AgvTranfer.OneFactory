using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Tests.TestInfrastructure;

namespace SCP.Tests.Controllers
{
    /// <summary>
    /// T2 Release 現有行為快照測試（ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-1。重構前先記錄一廠三條回送路線（K→L、M→A、B→A）的現有結果，
    /// 清除殘留與改查設定之後，這些案例必須不修改就通過。
    /// T12 區段為行為變更案例（spec.md 行為變更清單 B1），於 T12 補實作。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class DispatchControllerReleaseTests
    {
        private static Dictionary<string, string> Request(string stationNo)
            => new Dictionary<string, string> { ["stationNo"] = stationNo };

        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void Release_FromKArea_PicksEmptySlotInLArea()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("L1");
            db.AddPort("L2");
            db.AddPort("A1");   // 其他區的空位不可被選到
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("L1", result.ValueOf("endStation"));
            var need = db.Needs().Single();
            Assert.AreEqual("K1", need.ObjStation);
            Assert.AreEqual("L1", need.EndStation);
        }

        [TestMethod]
        public void Release_FromMArea_PicksEmptySlotInAArea()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "1");
            db.AddPort("A1");
            db.AddPort("A2");
            db.AddPort("L1");   // 其他區的空位不可被選到
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("M1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("A1", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public void Release_FromBArea_PicksEmptySlotInAArea()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("B1", haveFlag: "1");
            db.AddPort("A1");
            db.AddPort("A2");
            db.AddPort("L1");   // 其他區的空位不可被選到
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("B1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("A1", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public void Release_MultipleEmptySlots_PicksHighestPriorityThenLowestPort()
        {
            // Arrange: L1 權重較低；L2、L3 權重相同且較高 → 應取 L2
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("L1", priority: 50);
            db.AddPort("L3", priority: 80);
            db.AddPort("L2", priority: 80);
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("L2", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public void Release_Success_CreatesNeedWithSourceRackIdAndWebSource()
        {
            // Arrange: 起點站仍留有 RackId 與工單（標記空板只清工單，這裡刻意留著以確認不會被帶入）
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1", rackId: "Rack77", workOrder: "LOT0005");
            db.AddPort("L1");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            var need = db.Needs().Single();
            Assert.AreEqual("Rack77", need.RackId);
            Assert.AreEqual("", need.WorkOrder);
            Assert.AreEqual("Web", need.TaskSource);
            Assert.AreEqual("", need.AssignFlag);
            Assert.AreEqual(20, need.TaskDateTime!.Length);   // yyyyMMddHHmmssffffff
        }

        [TestMethod]
        public void Release_NoEmptySlotInTargetArea_ReturnsBadRequestAndCreatesNoNeed()
        {
            // Arrange: L 區三個站各因一種原因不可用（有料／停用／已被註冊路徑）
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("L1", haveFlag: "3");
            db.AddPort("L2", useFlag: "N");
            db.AddPort("L3", bgnToEnd: "A1>L3");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("L區（3F下料區）沒有可放置的空位", result.MessageOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void Release_SourceNotEmptyTray_ReturnsBadRequest()
        {
            // Arrange: 起點為料盤（HaveFlag=3），尚未標記空板
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "3");
            db.AddPort("L1");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("請先標記為空板", result.MessageOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void Release_SourceAlreadyHasPendingNeed_ReturnsBadRequest()
        {
            // Arrange: K1 已有尚未指派的派送需求
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("L1");
            db.AddPort("L2");
            db.AddNeed("K1", "L1", assignFlag: "");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("此站點已有待處理的派送任務，終點：L1", result.MessageOf());
            Assert.AreEqual(1, db.Needs().Count);
        }

        [TestMethod]
        public void Release_TargetSlotAlreadyPendingEndStation_SkipsThatSlot()
        {
            // Arrange: L1 已是另一筆待處理需求的終點 → 應改選 L2
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("K2", haveFlag: "1");
            db.AddPort("L1");
            db.AddPort("L2");
            db.AddNeed("K2", "L1", assignFlag: "");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("L2", result.ValueOf("endStation"));
        }

        [TestMethod]
        public void Release_UserHasRoutesButNoReleasePermission_ReturnsForbidden()
        {
            // Arrange: 使用者只有 A 區的一般派送路線，沒有 K 區的回送路線
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("L1");
            db.AddRoute("ROUTE_F1_A_TO_K", "A", "K,M", "DISPATCH");
            db.AddRoute("ROUTE_F1_K_RELEASE", "K", "L", "RELEASE");
            db.AddUserRoute("u1", "ROUTE_F1_A_TO_K");
            var controller = db.CreateDispatchController(userId: "u1");

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(403, result.StatusCodeOf());
            Assert.AreEqual("您沒有此區域的回送權限", result.MessageOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void Release_UserHasReleasePermissionForArea_Succeeds()
        {
            // Arrange: 使用者擁有 K 區的回送路線
            using var db = new ScpTestDb();
            db.AddPort("K1", haveFlag: "1");
            db.AddPort("L1");
            db.AddRoute("ROUTE_F1_K_RELEASE", "K", "L", "RELEASE");
            db.AddUserRoute("u1", "ROUTE_F1_K_RELEASE");
            var controller = db.CreateDispatchController(userId: "u1");

            // Act
            var result = controller.Release(Request("K1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("L1", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public void Release_StationNotFound_ReturnsBadRequest()
        {
            // Arrange: K9 不存在於 oPort
            using var db = new ScpTestDb();
            db.AddPort("L1");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("K9"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("站點不存在", result.MessageOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        // ── T12 行為變更案例（B1）────────────────────────────────

        [TestMethod]
        public void Release_AreaWithoutReleaseRule_ReturnsBadRequestAndCreatesNoNeed()
        {
            // Arrange: C 區不在一廠 ReleaseRoutes；M 區有空位（舊規則會落到 M→Q→R）
            using var db = new ScpTestDb();
            db.AddPort("C1", haveFlag: "1");
            db.AddPort("M1", area: "FHT1-3F");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.Release(Request("C1"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("此區域未設定回送路線", result.MessageOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void Release_NewAreaAddedByConfigOnly_PicksSlotInConfiguredTarget()
        {
            // Arrange: 設定只加 X→[Y]；M 區也有空位（舊規則會落到 M）
            using var db = new ScpTestDb();
            db.AddPort("X1", haveFlag: "1");
            db.AddPort("Y1");
            db.AddPort("M1", area: "FHT1-3F");
            var settings = ScpTestDb.FactoryOneAreaRules();
            settings["AreaRules:ReleaseRoutes:X:0"] = "Y";
            var controller = db.CreateDispatchController(settings: settings);

            // Act
            var result = controller.Release(Request("X1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("Y1", db.Needs().Single().EndStation);
        }
    }
}
