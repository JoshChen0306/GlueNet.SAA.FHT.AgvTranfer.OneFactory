using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Tests.TestInfrastructure;

namespace SCP.Tests.Controllers
{
    /// <summary>
    /// T3 RegisterLot / InsertoNeed / Index 現有行為快照測試
    /// （ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-1。沿用 T2 建立的 SQLite 記憶體資料庫基礎建設。
    /// T12 區段為行為變更案例（spec.md 行為變更清單 B6、B7），於 T12 補實作。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class DispatchControllerLotAndNeedTests
    {
        private const string WorkOrderBarcode = "FHT^N01^238090671^DMT6CVJ1536D^P3804231^202309181158";

        private static Dictionary<string, string> LotRequest(string stationNo, string workOrder, string rackId)
            => new Dictionary<string, string>
            {
                ["stationNo"] = stationNo,
                ["workOrder"] = workOrder,
                ["rackId"] = rackId,
                ["isVcutMaterial"] = "false"   // 一廠前端一律送 false（V Cut 勾選欄位是隱藏的）
            };

        /// <summary>派車請求：欄位與 Dispatch.js 送出的內容一致（Status 預設為下拉選單第一項 "1"）。</summary>
        private static Dictionary<string, string> NeedRequest(string area, string begin, string end,
            string rackId = "", string workOrder = "")
            => new Dictionary<string, string>
            {
                ["Floor"] = "FHT1-1F",
                ["Area"] = area,
                ["BegingStation"] = begin,
                ["EndStation"] = end,
                ["RackId"] = rackId,
                ["WorkOrder"] = workOrder,
                ["btnName"] = "ConfirmButton",
                ["Status"] = "1"
            };

        /// <summary>一廠 appsettings 的 Area 與 FloorSettings。</summary>
        private static Dictionary<string, string?> FactoryOneSettings() => new Dictionary<string, string?>
        {
            ["Area:1F 供貨區(Prepare area)"] = "A",
            ["Area:1F 下料區(Unloading area)"] = "B",
            ["Area:3F 左側上料區(Left loading area)"] = "K",
            ["Area:3F 右側下料區(Right unloading area)"] = "L",
            ["Area:3F 暫存區(Buffer area)"] = "M",
            ["FloorSettings:FHT1-1F:DisplayName"] = "1F",
            ["FloorSettings:FHT1-1F:Areas:0"] = "A",
            ["FloorSettings:FHT1-1F:Areas:1"] = "B",
            ["FloorSettings:FHT1-1F:Areas:2"] = "C",
            ["FloorSettings:FHT1-3F:DisplayName"] = "3F",
            ["FloorSettings:FHT1-3F:Areas:0"] = "K",
            ["FloorSettings:FHT1-3F:Areas:1"] = "L",
            ["FloorSettings:FHT1-3F:Areas:2"] = "M",
        };

        private static string AreaValues(SCP.Controllers.DispatchController controller)
            => string.Join(",", ((List<SelectListItem>)controller.ViewBag.AreaList).Select(i => i.Value).OrderBy(v => v));

        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void RegisterLot_AreaAEmptyStation_SetsHaveFlag3AndWritesLotInfo()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "0");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("A1", WorkOrderBarcode, "Rack01"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            var port = db.Port("A1");
            Assert.AreEqual("3", port.HaveFlag);
            Assert.AreEqual(WorkOrderBarcode, port.WorkOrder);
            Assert.AreEqual("Rack01", port.RackId);
            Assert.AreEqual(20, port.PutTime!.Length);   // yyyyMMddHHmmssffffff
        }

        [TestMethod]
        public void RegisterLot_EmptyWorkOrder_ReturnsBadRequest()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "0");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("A1", "", "Rack01"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("請輸入工單條碼", result.MessageOf());
            Assert.AreEqual("0", db.Port("A1").HaveFlag);
        }

        [DataTestMethod]
        [DataRow("K1")]
        [DataRow("L1")]
        public void RegisterLot_AreaKOrLWithEmptyRackId_ReturnsBadRequest(string stationNo)
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort(stationNo, haveFlag: "0", area: "FHT1-3F");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest(stationNo, WorkOrderBarcode, ""));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("請輸入貨架條碼", result.MessageOf());
            Assert.AreEqual("0", db.Port(stationNo).HaveFlag);
        }

        [TestMethod]
        public void RegisterLot_AreaAWithEmptyRackId_IsAllowed()
        {
            // Arrange: A 區不要求貨架條碼
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "0");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("A1", WorkOrderBarcode, ""));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("3", db.Port("A1").HaveFlag);
        }

        [TestMethod]
        public void InsertoNeed_Normal_InsertsNeedWithWebSource()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "3", rackId: "Rack01", workOrder: "LOT0001");
            db.AddPort("K1", area: "FHT1-3F");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.InsertoNeed(NeedRequest("A", "A1", "K1", rackId: "Rack01", workOrder: "LOT0001"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            var need = db.Needs().Single();
            Assert.AreEqual("A1", need.ObjStation);
            Assert.AreEqual("K1", need.EndStation);
            Assert.AreEqual("Rack01", need.RackId);
            Assert.AreEqual("LOT0001", need.WorkOrder);
            Assert.AreEqual("Web", need.TaskSource);
            Assert.AreEqual("", need.AssignFlag);
            Assert.AreEqual(20, need.TaskDateTime!.Length);
        }

        [TestMethod]
        public void InsertoNeed_EmptyWorkOrder_UsesWorkOrderFromSourcePort()
        {
            // Arrange: 一廠樓層隱藏工單欄位，前端送空字串
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "3", workOrder: "LOT0005");
            db.AddPort("K1", area: "FHT1-3F");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.InsertoNeed(NeedRequest("A", "A1", "K1", workOrder: ""));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("LOT0005", db.Needs().Single().WorkOrder);
        }

        [TestMethod]
        public void InsertoNeed_EmptyEndStation_ReturnsBadRequestAndInsertsNothing()
        {
            // Arrange
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "3", workOrder: "LOT0005");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.InsertoNeed(NeedRequest("A", "A1", ""));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("暫存區無空架", result.MessageOf());
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public void InsertoNeed_RequestWithoutLegacyFields_InsertsNeed()
        {
            // Arrange: T9（B4）後端不再讀取 Area / btnName / Status，T10 前端將停止傳送
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "3", workOrder: "LOT0005");
            db.AddPort("K1", area: "FHT1-3F");
            var controller = db.CreateDispatchController();
            var request = new Dictionary<string, string>
            {
                ["BegingStation"] = "A1",
                ["EndStation"] = "K1",
                ["RackId"] = "",
                ["WorkOrder"] = ""
            };

            // Act
            var result = controller.InsertoNeed(request);

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            var need = db.Needs().Single();
            Assert.AreEqual("A1", need.ObjStation);
            Assert.AreEqual("K1", need.EndStation);
            Assert.AreEqual("LOT0005", need.WorkOrder);
            Assert.AreEqual("", need.AssignFlag);
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void RegisterLot_RackIdSentinelMinusOne_PassesValidation()
        {
            // Arrange: 隱藏貨架條碼欄位時，前端固定送 -1
            using var db = new ScpTestDb();
            db.AddPort("L1", haveFlag: "0", area: "FHT1-3F");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("L1", WorkOrderBarcode, "-1"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual("-1", db.Port("L1").RackId);
        }

        [TestMethod]
        public void RegisterLot_StationIsPendingEndStation_ReturnsBadRequest()
        {
            // Arrange: A1 是 M1 回送任務的終點
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "0");
            db.AddNeed("M1", "A1", assignFlag: "");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("A1", WorkOrderBarcode, "Rack01"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("此站點有待處理的回送任務（來自 M1），請等待任務完成後再登記", result.MessageOf());
            Assert.AreEqual("0", db.Port("A1").HaveFlag);
        }

        [TestMethod]
        public void RegisterLot_StationHasRunningTask_ReturnsBadRequest()
        {
            // Arrange: A1 是執行中任務的起點
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "3", workOrder: "LOT0001");
            db.AddMission("A1", "K1", okFlag: "R");
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("A1", WorkOrderBarcode, "Rack01"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("此站點有進行中的派送任務，無法修改物料資訊", result.MessageOf());
            Assert.AreEqual("LOT0001", db.Port("A1").WorkOrder);
        }

        [TestMethod]
        public void RegisterLot_StationNotFound_ReturnsBadRequest()
        {
            // Arrange
            using var db = new ScpTestDb();
            var controller = db.CreateDispatchController();

            // Act
            var result = controller.RegisterLot(LotRequest("A9", WorkOrderBarcode, "Rack01"));

            // Assert
            Assert.AreEqual(400, result.StatusCodeOf());
            Assert.AreEqual("站點不存在", result.MessageOf());
        }

        [TestMethod]
        public void Index_UserWithDispatchRoutes_AreaListContainsOnlyRouteSources()
        {
            // Arrange: 使用者有 A、L 的一般派送路線與 K 的回送路線
            using var db = new ScpTestDb();
            db.AddRoute("ROUTE_F1_A_TO_K", "A", "K,M", "DISPATCH");
            db.AddRoute("ROUTE_F1_L_TO_B", "L", "B", "DISPATCH");
            db.AddRoute("ROUTE_F1_K_RELEASE", "K", "L", "RELEASE");
            db.AddUserRoute("u1", "ROUTE_F1_A_TO_K");
            db.AddUserRoute("u1", "ROUTE_F1_L_TO_B");
            db.AddUserRoute("u1", "ROUTE_F1_K_RELEASE");
            var controller = db.CreateDispatchController(userId: "u1", groupId: "1", settings: FactoryOneSettings());

            // Act
            controller.Index();

            // Assert: 派送區域只有一般派送路線的起點；回送區域另外傳給前端
            Assert.AreEqual("A,L", AreaValues(controller));
            CollectionAssert.AreEquivalent(new[] { "K" }, (List<string>)controller.ViewBag.AllowedReleaseAreas);
            CollectionAssert.AreEquivalent(new[] { "A", "L" }, (List<string>)controller.ViewBag.AllowedSourceAreas);
        }

        [DataTestMethod]
        [DataRow("2", "A")]            // 群組 2：只看得到 A 區
        [DataRow("5", "")]             // 群組 5：原本對應 F 區，一廠沒有 → 無可派送區域
        [DataRow("6", "")]             // 群組 6：原本對應 C、D 區，一廠派送選單沒有 → 無可派送區域
        [DataRow("1", "A,B,K,L,M")]    // 其他群組：全部區域
        public void Index_UserWithoutRoutes_AreaListFollowsLegacyGroupFilter(string groupId, string expectedAreas)
        {
            // Arrange: 使用者沒有任何路線權限設定 → 走舊的群組篩選
            using var db = new ScpTestDb();
            var controller = db.CreateDispatchController(userId: "u2", groupId: groupId, settings: FactoryOneSettings());

            // Act
            controller.Index();

            // Assert
            Assert.AreEqual(expectedAreas, AreaValues(controller));
        }

        // ── T12 行為變更案例（B6、B7）────────────────────────────

        [TestMethod]
        public void RegisterLot_WorkOrderContainingLegacyTagText_IsStoredUnchanged()
        {
            // Arrange: 工單含舊專案標記字樣（B6：不再移除）
            using var db = new ScpTestDb();
            db.AddPort("A1", haveFlag: "0");
            var controller = db.CreateDispatchController();
            const string workOrder = "LOT01^NG^RETURN^VCUT^DONE";

            // Act
            var result = controller.RegisterLot(LotRequest("A1", workOrder, "Rack01"));

            // Assert
            Assert.AreEqual(200, result.StatusCodeOf());
            Assert.AreEqual(workOrder, db.Port("A1").WorkOrder);
        }

        [TestMethod]
        public void RegisterLot_RackIdRequiredAreaFromConfig_EmptyRackIdReturnsBadRequest()
        {
            // Arrange: RackIdRequiredAreas 只設 X（B7：依設定判斷，不再寫死 J/H/I/K/L）
            using var db = new ScpTestDb();
            db.AddPort("X1", haveFlag: "0");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");
            var settings = ScpTestDb.FactoryOneAreaRules();
            settings.Remove("AreaRules:RackIdRequiredAreas:0");
            settings.Remove("AreaRules:RackIdRequiredAreas:1");
            settings["AreaRules:RackIdRequiredAreas:0"] = "X";
            var controller = db.CreateDispatchController(settings: settings);

            // Act
            var resultX = controller.RegisterLot(LotRequest("X1", WorkOrderBarcode, ""));
            var resultK = controller.RegisterLot(LotRequest("K1", WorkOrderBarcode, ""));

            // Assert
            Assert.AreEqual(400, resultX.StatusCodeOf());
            Assert.AreEqual("請輸入貨架條碼", resultX.MessageOf());
            Assert.AreEqual(200, resultK.StatusCodeOf());
        }
    }
}
