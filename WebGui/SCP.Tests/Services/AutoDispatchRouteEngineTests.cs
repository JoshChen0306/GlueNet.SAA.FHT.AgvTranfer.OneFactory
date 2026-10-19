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

        /// <summary>R3 迅得1(B1)→中光電B(P1)：L→B 跨樓層送達後轉送（2026-10-19，T28）。</summary>
        private static AutoDispatchRoute R3() => new AutoDispatchRoute
        {
            Name = "R3",
            SourceBlocks = new List<string> { "B" },
            TargetBlocks = new List<string> { "P" },
            SourceHaveFlags = new List<string> { "3" },
            SensorGuard = true,
        };

        /// <summary>R5 迅得5/6/7(F)→A1/A2：R4 送達的空盤轉送（2026-10-19，T26）。</summary>
        private static AutoDispatchRoute R5() => new AutoDispatchRoute
        {
            Name = "R5",
            SourceBlocks = new List<string> { "F" },
            TargetBlocks = new List<string> { "A" },
            SourceHaveFlags = new List<string> { "1" },
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
        public async Task Execute_SameGuardWarningWithinInterval_LoggedOnce()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "3", workOrder: "");
            db.AddPort("E1", haveFlag: "0");
            var logger = new ListLogger();
            var throttle = new AutoDispatchWarningThrottle(TimeSpan.FromMinutes(10));
            var now = new DateTime(2026, 10, 12, 8, 0, 0);

            await new AutoDispatchRouteEngine(db.Context, logger, throttle, () => now).ExecuteAsync(new[] { R1() }, CancellationToken.None);
            now = now.AddMinutes(5);
            await new AutoDispatchRouteEngine(db.Context, logger, throttle, () => now).ExecuteAsync(new[] { R1() }, CancellationToken.None);

            Assert.AreEqual(1, logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("D1")));
        }

        [TestMethod]
        public async Task Execute_SameGuardWarningAfterInterval_LoggedAgain()
        {
            using var db = new ScpTestDb();
            db.AddPort("D1", haveFlag: "3", workOrder: "");
            db.AddPort("E1", haveFlag: "0");
            var logger = new ListLogger();
            var throttle = new AutoDispatchWarningThrottle(TimeSpan.FromMinutes(10));
            var now = new DateTime(2026, 10, 12, 8, 0, 0);

            await new AutoDispatchRouteEngine(db.Context, logger, throttle, () => now).ExecuteAsync(new[] { R1() }, CancellationToken.None);
            now = now.AddMinutes(10);
            await new AutoDispatchRouteEngine(db.Context, logger, throttle, () => now).ExecuteAsync(new[] { R1() }, CancellationToken.None);

            Assert.AreEqual(2, logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("D1")));
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

        // ── R3 B1→中光電B（T28）────────────────────────────────

        [TestMethod]
        public async Task Execute_R3B1ArrivedFromLAndPEmpty_CreatesB1ToP1()
        {
            using var db = new ScpTestDb();
            db.AddPort("B1", haveFlag: "3", rackId: "R-L1", workOrder: "WO-L1");
            db.AddPort("P1", haveFlag: "0");

            await Run(db, new ListLogger(), R3());

            var need = db.Needs().Single();
            Assert.AreEqual("B1", need.ObjStation);
            Assert.AreEqual("P1", need.EndStation);
            Assert.AreEqual("WO-L1", need.WorkOrder);
        }

        [TestMethod]
        public async Task Execute_R3POccupied_DoesNotDispatch()
        {
            using var db = new ScpTestDb();
            db.AddPort("B1", haveFlag: "3", workOrder: "WO-L1");
            db.AddPort("P1", haveFlag: "3", workOrder: "WO-OLD");

            int created = await Run(db, new ListLogger(), R3());

            Assert.AreEqual(0, created);
        }

        [TestMethod]
        public async Task Execute_R3B1MarkedEmptyTray_DoesNotDispatch()
        {
            // B1 標記空板（HaveFlag=1）走路線5 Release 回 A，不可被 R3 送去中光電B
            using var db = new ScpTestDb();
            db.AddPort("B1", haveFlag: "1");
            db.AddPort("P1", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R3());

            Assert.AreEqual(0, created);
        }

        [TestMethod]
        public async Task Execute_R3B1AlreadyHasPendingTask_DoesNotDispatch()
        {
            // B1 已有人員建立的任務（例：人員派發備援 B1→P1 或 Release）時不重複派送
            using var db = new ScpTestDb();
            db.AddPort("B1", haveFlag: "3", workOrder: "WO-L1");
            db.AddPort("P1", haveFlag: "0");
            db.AddNeed("B1", "A1", assignFlag: "");

            int created = await Run(db, new ListLogger(), R3());

            Assert.AreEqual(0, created);
        }

        // ── R5 迅得5/6/7→A1/A2（T26）───────────────────────────

        [TestMethod]
        public async Task Execute_R5EmptyTrayOnFAndAEmpty_CreatesFToA()
        {
            using var db = new ScpTestDb();
            db.AddPort("F1", haveFlag: "1");
            db.AddPort("A1", haveFlag: "0");

            await Run(db, new ListLogger(), R5());

            var need = db.Needs().Single();
            Assert.AreEqual("F1", need.ObjStation);
            Assert.AreEqual("A1", need.EndStation);
        }

        [TestMethod]
        public async Task Execute_R5A1A2Occupied_DoesNotDispatch()
        {
            // A1 有登記物料、A2 有回送空板 → 不派，下一輪再查
            using var db = new ScpTestDb();
            db.AddPort("F1", haveFlag: "1");
            db.AddPort("A1", haveFlag: "3", workOrder: "WO-A1");
            db.AddPort("A2", haveFlag: "1");

            int created = await Run(db, new ListLogger(), R5());

            Assert.AreEqual(0, created);
        }

        [TestMethod]
        public async Task Execute_R5DisabledA3_NotSelectedAsTarget()
        {
            // 一廠 A3～A5 為 UseFlag=N，只能送 A1/A2
            using var db = new ScpTestDb();
            db.AddPort("F1", haveFlag: "1");
            db.AddPort("F2", haveFlag: "1");
            db.AddPort("A1", haveFlag: "0");
            db.AddPort("A3", haveFlag: "0", useFlag: "N");

            await Run(db, new ListLogger(), R5());

            Assert.AreEqual("A1", db.Needs().Single().EndStation);
        }

        [TestMethod]
        public async Task Execute_R5FHasMaterialHaveFlag3_DoesNotDispatch()
        {
            // R5 只送空盤；F 站若被登記成有料（HaveFlag=3）不送到 A
            using var db = new ScpTestDb();
            db.AddPort("F1", haveFlag: "3", workOrder: "WO-F1");
            db.AddPort("A1", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R5());

            Assert.AreEqual(0, created);
        }

        // ── R2 迅得2/3/4→中光電C＋^DONE（T27）─────────────────

        /// <summary>R2：只派人員加工後登記（WorkOrder 帶 ^DONE）的 E 站。</summary>
        private static AutoDispatchRoute R2() => new AutoDispatchRoute
        {
            Name = "R2",
            SourceBlocks = new List<string> { "E" },
            TargetBlocks = new List<string> { "Z" },
            SourceHaveFlags = new List<string> { "3" },
            SourceWorkOrderMarker = "^DONE",
        };

        [TestMethod]
        public async Task Execute_R2ArrivedFromDWithoutMarker_DoesNotDispatch()
        {
            // D→E 剛送達：HaveFlag=3＋中光電 itemCode，尚未加工，不可送去 C
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "3", workOrder: "ITEM-D1");
            db.AddPort("Z1", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R2());

            Assert.AreEqual(0, created);
        }

        [TestMethod]
        public async Task Execute_R2MarkedAndZEmpty_DispatchesWithMarkerStripped()
        {
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "3", workOrder: "ITEM-D1");
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-NEW^DONE");
            db.AddPort("Z1", haveFlag: "0");

            await Run(db, new ListLogger(), R2());

            var need = db.Needs().Single();
            Assert.AreEqual("E2", need.ObjStation);
            Assert.AreEqual("Z1", need.EndStation);
            Assert.AreEqual("WO-NEW", need.WorkOrder, "oNeed 不可帶 ^DONE，否則會帶到中光電 Mapping 的 itemCode");
        }

        [TestMethod]
        public async Task Execute_R2LegacyVcutDoneWorkOrder_DoesNotDispatch()
        {
            // 二廠殘留字樣 ^VCUT^DONE 不是加工完成標記，不可觸發 R2
            using var db = new ScpTestDb();
            db.AddPort("E1", haveFlag: "3", workOrder: "LOT01^VCUT^DONE");
            db.AddPort("Z1", haveFlag: "0");

            int created = await Run(db, new ListLogger(), R2());

            Assert.AreEqual(0, created);
        }

        [TestMethod]
        public async Task Execute_R2MarkedButZOccupied_DoesNotDispatch()
        {
            using var db = new ScpTestDb();
            db.AddPort("E2", haveFlag: "3", workOrder: "WO-NEW^DONE");
            db.AddPort("Z1", haveFlag: "3", workOrder: "WO-OLD");

            int created = await Run(db, new ListLogger(), R2());

            Assert.AreEqual(0, created);
        }

        [TestMethod]
        public async Task Execute_RouteWithoutMarkerSetting_DispatchesRegardlessOfMarker()
        {
            // 未設 SourceWorkOrderMarker 的路線（M→K、R1、R3、R4、R5）行為不變；oNeed 仍移除 ^DONE
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", workOrder: "WO-M1", area: "FHT1-3F", putTime: "20260101010000");
            db.AddPort("M2", haveFlag: "3", workOrder: "WO-M2^DONE", area: "FHT1-3F", putTime: "20260101020000");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", area: "FHT1-3F");

            int created = await Run(db, new ListLogger(), AutoDispatchRoute.LegacyMToK());

            Assert.AreEqual(2, created);
            CollectionAssert.AreEquivalent(new[] { "WO-M1", "WO-M2" }, db.Needs().Select(n => n.WorkOrder).ToList());
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
