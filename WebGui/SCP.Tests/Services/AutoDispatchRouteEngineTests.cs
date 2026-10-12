using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Services;
using SCP.Tests.TestInfrastructure;

namespace SCP.Tests.Services
{
    /// <summary>
    /// T6 AutoDispatch 規則驅動引擎測試（ToDo/20260726_一廠AB棟1F新路線與邏輯）
    /// 對應 spec.md AC-3。M→K 重構回歸另見 AutoDispatchLegacyMToKTests。
    /// 區域代碼：D=中光電D、E=迅得2/3/4、O=中光電A、F=迅得5/6/7（區域代碼對照表.md）。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("AB1F")]
    public class AutoDispatchRouteEngineTests
    {
        /// <summary>R1 中光電D→迅得2/3/4：Sensor ON 寫 HaveFlag=1 即可派送，啟用 sensor 防呆。</summary>
        private static AutoDispatchRoute R1() => new AutoDispatchRoute
        {
            Name = "R1",
            SourceBlocks = new List<string> { "D" },
            TargetBlocks = new List<string> { "E" },
            SourceHaveFlags = new List<string> { "1", "3" },
            SensorGuard = true,
        };

        private static AutoDispatchRoute R4() => new AutoDispatchRoute
        {
            Name = "R4",
            SourceBlocks = new List<string> { "O" },
            TargetBlocks = new List<string> { "F" },
            SourceHaveFlags = new List<string> { "1", "3" },
            SensorGuard = true,
        };

        private static async Task<int> Run(ScpTestDb db, ListLogger logger, params AutoDispatchRoute[] routes)
            => await new AutoDispatchRouteEngine(db.Context, logger).ExecuteAsync(routes, CancellationToken.None);

        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public async Task Execute_R1SourceSensorOnHaveFlag1_CreatesOneAutoNeed()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R1());

            Assert.AreEqual(1, created);
            var need = db.Needs().Single();
            Assert.AreEqual("D1", need.ObjStation);
            Assert.AreEqual("E1", need.EndStation);
            Assert.AreEqual("Auto", need.TaskSource);
        }

        [TestMethod]
        public async Task Execute_R1SourceHaveFlag3WithWorkOrder_CarriesMaterialData()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "3", rackId: "R-9", workOrder: "WO-9");
            db.AddPort("E1", haveFlag: "0");

            await Run(db, new ListLogger(), R1());

            var need = db.Needs().Single();
            Assert.AreEqual("D1", need.ObjStation);
            Assert.AreEqual("E1", need.EndStation);
            Assert.AreEqual("R-9", need.RackId);
            Assert.AreEqual("WO-9", need.WorkOrder);
            Assert.AreEqual("Auto", need.TaskSource);
        }

        [TestMethod]
        public async Task Execute_DefaultSourceHaveFlags_OnlyHaveFlag3Dispatched()
        {
            // SourceHaveFlags 未設定 → 預設只認 3（與 M→K 重構前一致）
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");
            var route = R1();
            route.SourceHaveFlags = new List<string>();

            await Run(db, new ListLogger(), route);

            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public async Task Execute_MultipleTargetSlots_PicksByPriorityThenPort()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0", priority: 10);
            db.AddPort("E3", haveFlag: "0", priority: 90);
            db.AddPort("E2", haveFlag: "0", priority: 90);

            await Run(db, new ListLogger(), R1());

            Assert.AreEqual("E2", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public async Task Execute_MultipleSourceBlocksFifo_PicksOldestPutTimeAcrossBlocks()
        {
            // 引擎通用能力（R2 已改人員派發，以測試設定驗證）
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "3", workOrder: "WO-D", putTime: "20260101120000");
            db.AddPort("O1", haveFlag: "3", workOrder: "WO-O", putTime: "20260101080000");
            db.AddPort("E1", haveFlag: "0");
            var route = new AutoDispatchRoute
            {
                Name = "多來源",
                SourceBlocks = new List<string> { "D", "O" },
                TargetBlocks = new List<string> { "E" },
            };

            await Run(db, new ListLogger(), route);

            Assert.AreEqual("O1", db.Needs().Single().ObjStation);
        }

        [TestMethod]
        public async Task Execute_NoEmptyTargetSlot_DoesNotCreateNeedAndDoesNotThrow()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "3", workOrder: "WO-1");
            db.AddPort("E2", haveFlag: "0", bgnToEnd: "D9");

            int created = await Run(db, new ListLogger(), R1());

            Assert.AreEqual(0, created);
            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public async Task Execute_SourceHasPendingTask_ExcludesFromDispatch()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");
            db.AddPort("E2", haveFlag: "0");
            db.AddMission("D1", "E9", okFlag: "R");

            await Run(db, new ListLogger(), R1());

            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public async Task Execute_TargetAlreadyAssignedAsEnd_ExcludesFromSelection()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0", priority: 90);
            db.AddPort("E2", haveFlag: "0", priority: 10);
            db.AddNeed("B1", "E1", assignFlag: "");

            await Run(db, new ListLogger(), R1());

            var created = db.Needs().Single(n => n.ObjStation == "D1");
            Assert.AreEqual("E2", created.EndStation);
        }

        [TestMethod]
        public async Task Execute_SensorGuardSourceHaveFlag3WithoutWorkOrder_SkipsAndLogsWarning()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "3", workOrder: "");
            db.AddPort("E1", haveFlag: "0");
            var logger = new ListLogger();

            await Run(db, logger, R1());

            Assert.AreEqual(0, db.Needs().Count);
            Assert.IsTrue(logger.Has(LogLevel.Warning, "D1"));
        }

        [TestMethod]
        public async Task Execute_SensorGuardTargetHaveFlag0WithLeftoverData_SkipsSlotAndLogsWarning()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0", priority: 90, workOrder: "WO-OLD");
            db.AddPort("E2", haveFlag: "0", priority: 10);
            var logger = new ListLogger();

            await Run(db, logger, R1());

            Assert.AreEqual("E2", db.Needs().Single().EndStation);
            Assert.IsTrue(logger.Has(LogLevel.Warning, "E1"));
        }

        [TestMethod]
        public async Task Execute_SensorGuardOff_LegacyDataStillDispatched()
        {
            // M→K 未開防呆：HaveFlag=3 無工單仍照舊派送（重構前行為）
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", workOrder: "", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");

            await Run(db, new ListLogger(), AutoDispatchRoute.LegacyMToK());

            Assert.AreEqual(1, db.Needs().Count);
        }

        [TestMethod]
        public async Task Execute_TwoRoutesSameRound_EachDispatchesIndependently()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");
            db.AddPort("O1", haveFlag: "1");
            db.AddPort("F1", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R1(), R4());

            Assert.AreEqual(2, created);
            CollectionAssert.AreEquivalent(new[] { "D1>E1", "O1>F1" },
                db.Needs().Select(n => n.ObjStation + ">" + n.EndStation).ToList());
        }

        [TestMethod]
        public async Task Execute_TwoRoutesSharingTarget_SlotNotAssignedTwice()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("O1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");
            var r4ToE = R4();
            r4ToE.TargetBlocks = new List<string> { "E" };

            await Run(db, new ListLogger(), R1(), r4ToE);

            Assert.AreEqual(1, db.Needs().Count);
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public async Task Execute_RouteDisabled_SkipsEntireRoute()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");
            var route = R1();
            route.Enabled = false;

            await Run(db, new ListLogger(), route);

            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public async Task Execute_EmptyRoutesConfig_DoesNothingAndLogs()
        {
            using var db = new ScpTestDb();
            var logger = new ListLogger();

            int created = await Run(db, logger);

            Assert.AreEqual(0, created);
            Assert.IsTrue(logger.Has(LogLevel.Warning, "Routes"));
        }

        [TestMethod]
        public async Task Execute_RouteMissingBlocks_SkipsAndLogsWarning()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0");
            var route = R1();
            route.TargetBlocks = new List<string>();
            var logger = new ListLogger();

            await Run(db, logger, route);

            Assert.AreEqual(0, db.Needs().Count);
            Assert.IsTrue(logger.Has(LogLevel.Warning, "R1"));
        }

        [TestMethod]
        public async Task Execute_MoreMaterialsThanSlots_DispatchesMinCount()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1", putTime: "20260101010000");
            db.AddPort("D2", haveFlag: "1", putTime: "20260101020000");
            db.AddPort("D3", haveFlag: "1", putTime: "20260101030000");
            db.AddPort("E1", haveFlag: "0");
            db.AddPort("E2", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R1());

            Assert.AreEqual(2, created);
        }

        [TestMethod]
        public async Task Execute_UseFlagDisabledStations_AreExcluded()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "1", useFlag: "N");
            db.AddPort("D2", haveFlag: "1");
            db.AddPort("E1", haveFlag: "0", useFlag: "N");

            await Run(db, new ListLogger(), R1());

            Assert.AreEqual(0, db.Needs().Count);
        }
    }
}
