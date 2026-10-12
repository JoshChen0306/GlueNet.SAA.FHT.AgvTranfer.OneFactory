using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Models;
using SCP.Services;
using SCP.Tests.TestInfrastructure;

namespace SCP.Tests.Services
{
    /// <summary>
    /// T6 重構前行為快照：路線6 M→K 自動派送（ToDo/20260726_一廠AB棟1F新路線與邏輯）
    /// 重構前以反射呼叫 AutoDispatchService.CheckAndDispatchMToK 鎖定現有行為（2026-10-12 8/8 通過）；
    /// 重構後只改 RunMToKOnce 的呼叫方式（改呼叫 RunOnceAsync），斷言不得修改。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("AB1F")]
    public class AutoDispatchLegacyMToKTests
    {
        private static async Task RunMToKOnce(ScpTestDb db)
        {
            var services = new ServiceCollection();
            services.AddSingleton<agvDB_1400004Context>(db.Context);
            using var provider = services.BuildServiceProvider();

            var service = new AutoDispatchService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AutoDispatchService>.Instance,
                new ConfigurationBuilder().Build());

            // 設定檔沒有 AutoDispatch:Routes → 沿用路線6 M→K
            await service.RunOnceAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task MToK_OldestMaterialAndHighestPriorityslot_PairedFirst()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", putTime: "20260101120000", rackId: "R-1", workOrder: "WO-1", area: "FHT1-3F");
            db.AddPort("M2", haveFlag: "3", putTime: "20260101080000", rackId: "R-2", workOrder: "WO-2", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", priority: 10, area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", priority: 90, area: "FHT1-3F");

            await RunMToKOnce(db);

            var needs = db.Needs().OrderBy(n => n.TaskDateTime).ToList();
            Assert.AreEqual(2, needs.Count);
            // PutTime 最舊（M2）配 Priority 最高（K2）
            Assert.AreEqual("M2", needs[0].ObjStation);
            Assert.AreEqual("K2", needs[0].EndStation);
            Assert.AreEqual("R-2", needs[0].RackId);
            Assert.AreEqual("WO-2", needs[0].WorkOrder);
            Assert.AreEqual("Auto", needs[0].TaskSource);
            Assert.AreEqual("", needs[0].AssignFlag);
            Assert.AreEqual("M1", needs[1].ObjStation);
            Assert.AreEqual("K1", needs[1].EndStation);
        }

        [TestMethod]
        public async Task MToK_SamePriority_PicksSmallerPortFirst()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", putTime: "20260101080000", area: "FHT1-3F");
            db.AddPort("K3", haveFlag: "0", priority: 50, area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", priority: 50, area: "FHT1-3F");

            await RunMToKOnce(db);

            var need = db.Needs().Single();
            Assert.AreEqual("K2", need.EndStation);
        }

        [TestMethod]
        public async Task MToK_MoreMaterialsThanSlots_DispatchesMinCount()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", putTime: "20260101010000", area: "FHT1-3F");
            db.AddPort("M2", haveFlag: "3", putTime: "20260101020000", area: "FHT1-3F");
            db.AddPort("M3", haveFlag: "3", putTime: "20260101030000", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", area: "FHT1-3F");

            await RunMToKOnce(db);

            CollectionAssert.AreEquivalent(new[] { "M1", "M2" }, db.Needs().Select(n => n.ObjStation).ToList());
        }

        [TestMethod]
        public async Task MToK_SourceHaveFlagNot3OrUseFlagN_Excluded()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "1", area: "FHT1-3F");
            db.AddPort("M2", haveFlag: "2", area: "FHT1-3F");
            db.AddPort("M3", haveFlag: "3", useFlag: "N", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");

            await RunMToKOnce(db);

            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public async Task MToK_TargetOccupiedBgnToEndOrUseFlagN_Excluded()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", bgnToEnd: "M5", area: "FHT1-3F");
            db.AddPort("K3", haveFlag: "0", useFlag: "N", area: "FHT1-3F");

            await RunMToKOnce(db);

            Assert.AreEqual(0, db.Needs().Count);
        }

        [TestMethod]
        public async Task MToK_SourcePendingInNeedRequireOrMission_Excluded()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("M2", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("M3", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", area: "FHT1-3F");
            db.AddPort("K3", haveFlag: "0", area: "FHT1-3F");
            db.AddNeed("M1", "A1", assignFlag: "Y");
            db.AddRequire("M2", "A2", okFlag: "R");
            db.AddMission("M3", "A3", okFlag: "");

            await RunMToKOnce(db);

            // 只剩既有的 1 筆 oNeed，沒有新增
            Assert.AreEqual(1, db.Needs().Count);
        }

        [TestMethod]
        public async Task MToK_TargetPendingAsEnd_Excluded()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", priority: 90, area: "FHT1-3F");
            db.AddPort("K2", haveFlag: "0", priority: 10, area: "FHT1-3F");
            db.AddMission("L1", "K1", okFlag: "Y");

            await RunMToKOnce(db);

            var need = db.Needs().Single();
            Assert.AreEqual("K2", need.EndStation);
        }

        [TestMethod]
        public async Task MToK_FinishedTasks_DoNotBlockStations()
        {
            using var db = new ScpTestDb();
            db.AddPort("M1", haveFlag: "3", area: "FHT1-3F");
            db.AddPort("K1", haveFlag: "0", area: "FHT1-3F");
            db.AddRequire("M1", "K1", okFlag: "Y");   // oRequire 已完成（OkFlag=Y 不算 pending）
            db.AddMission("M1", "K1", okFlag: "E");   // oMission 其他結束狀態

            await RunMToKOnce(db);

            var need = db.Needs().Single();
            Assert.AreEqual("M1", need.ObjStation);
            Assert.AreEqual("K1", need.EndStation);
        }
    }
}
