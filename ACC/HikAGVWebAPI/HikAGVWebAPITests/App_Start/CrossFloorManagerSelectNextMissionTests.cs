using System.Collections.Generic;
using HikAGVWebAPI;
using HikAGVWebAPI.App_Start;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HikAGVWebAPITests.App_Start
{
    /// <summary>
    /// SelectNextMission 整合冷卻期檢查的單元測試（對應工作計畫 Task 4）
    /// 測試策略：
    ///   - Tests 1~3：測試純決策函數 CrossFloorManager.DecideNextCrossFloorAction
    ///     （已抽出為 internal static，不依賴 SQLData/HikAGV/Log）
    ///   - Tests 4~5：測試 OnCrossFloorDispatchCompleted(mission) 是否正確驅動 CooldownTracker
    ///     以 CrossFloorManager 實體 + 預設空 ElevatorSettings 建構
    /// </summary>
    [TestClass]
    public class CrossFloorManagerSelectNextMissionTests
    {
        private ElevatorPathCalculator _pathCalculator;

        [TestInitialize]
        public void Setup()
        {
            _pathCalculator = new ElevatorPathCalculator(new ElevatorSettings());
        }

        #region Happy Path — 決策函數行為

        [TestMethod]
        public void Decide_冷卻期命中且需派預調度_回傳CooldownHit並帶父任務()
        {
            // Arrange
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            cooldown.RecordCompletion("PARENT_A");

            // 父任務起點 W1（3F 客貨梯等待點，視為 3F）；車在 1F → 需要預調度
            var parentTask = new oMissionModel
            {
                TaskDateTime = "PARENT_A",
                BeginStation = "I1",
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { parentTask };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.CooldownHit, decision.Kind);
            Assert.AreSame(parentTask, decision.Task, "冷卻期命中應回傳 triggerTask (父任務) 而非新建預調度");
        }

        [TestMethod]
        public void Decide_冷卻期未命中且需派預調度_回傳NeedDispatch()
        {
            // Arrange
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            // 無 RecordCompletion → 冷卻期未命中

            var parentTask = new oMissionModel
            {
                TaskDateTime = "PARENT_B",
                BeginStation = "I1",
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { parentTask };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.NeedDispatch, decision.Kind);
            Assert.AreSame(parentTask, decision.Task);
            Assert.AreEqual("1F", decision.FromFloor);
            Assert.AreEqual("3F", decision.ToFloor);
        }

        [TestMethod]
        public void Decide_不同parent不受冷卻期影響_照常派預調度()
        {
            // Arrange
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            cooldown.RecordCompletion("PARENT_A");

            // triggerTask 的 TaskDateTime 與冷卻期儲存的 parent 不同
            var triggerTask = new oMissionModel
            {
                TaskDateTime = "PARENT_DIFFERENT",
                BeginStation = "I1",
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { triggerTask };

            // Act
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.NeedDispatch, decision.Kind,
                "不同 parent 不受冷卻期影響，應回傳 NeedDispatch 正常派預調度");
        }

        [TestMethod]
        public void OnCrossFloorDispatchCompleted_mission帶ParentTaskDateTime_記錄進冷卻期()
        {
            // Arrange
            var manager = CreateTestManager();
            var mission = new oMissionModel
            {
                TaskSource = CrossFloorManager.CROSS_FLOOR_DISPATCH,
                ParentTaskDateTime = "PARENT_RECORD",
            };

            // Act
            manager.OnCrossFloorDispatchCompleted(mission);

            // Assert
            Assert.IsTrue(manager.CooldownTracker.IsInCooldown("PARENT_RECORD"),
                "預調度完成 + 帶 ParentTaskDateTime → CooldownTracker 應記錄該 parent");
        }

        #endregion

        #region Edge Case

        [TestMethod]
        public void OnCrossFloorDispatchCompleted_mission無ParentTaskDateTime_不記錄()
        {
            // Arrange
            var manager = CreateTestManager();
            var mission = new oMissionModel
            {
                TaskSource = CrossFloorManager.CROSS_FLOOR_DISPATCH,
                ParentTaskDateTime = null,
            };

            // Act
            manager.OnCrossFloorDispatchCompleted(mission);

            // Assert
            Assert.IsFalse(manager.CooldownTracker.IsInCooldown("PARENT_ANY"),
                "ParentTaskDateTime 為 null → CooldownTracker 不應記錄任何 parent");
        }

        #endregion

        #region 純函數補強（一廠特化覆蓋 spec.md #5 同樓層直派 + 邊界場景）

        [TestMethod]
        public void Decide_跨樓層任務且車輛在起點樓層_回傳SameFloor並帶任務()
        {
            // Arrange — 同樓層應優先派發，即使冷卻期命中也不被攔截
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            cooldown.RecordCompletion("TASK_SAMEFLOOR");

            var task = new oMissionModel
            {
                TaskDateTime = "TASK_SAMEFLOOR",
                BeginStation = "I1",  // 3F (StationFloorMapping: I → 3F)
                EndStation = "I2",
                TaskSource = "MCS",
            };
            var pending = new List<oMissionModel> { task };

            // Act — 車輛在 3F（與任務起點同樓層）
            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "3F", _pathCalculator, cooldown);

            // Assert
            Assert.AreEqual(CrossFloorDecisionKind.SameFloor, decision.Kind,
                "同樓層任務優先派發，不受冷卻期影響（spec.md #5）");
            Assert.AreSame(task, decision.Task);
        }

        [TestMethod]
        public void Decide_pendingList為空_回傳None()
        {
            var cooldown = new CooldownTracker(cooldownSeconds: 30);

            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                new List<oMissionModel>(), currentFloor: "1F", _pathCalculator, cooldown);

            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind);
        }

        [TestMethod]
        public void Decide_pendingList為null_回傳None()
        {
            var cooldown = new CooldownTracker(cooldownSeconds: 30);

            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                null, currentFloor: "1F", _pathCalculator, cooldown);

            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind);
        }

        [TestMethod]
        public void Decide_僅含IDLE_RETURN與CROSS_FLOOR_DISPATCH_過濾後回傳None()
        {
            // 系統任務不應被當作待派 normal task
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            var systemTasks = new List<oMissionModel>
            {
                new oMissionModel { TaskDateTime = "T1", BeginStation = "I1", EndStation = "I2", TaskSource = CrossFloorManager.IDLE_RETURN },
                new oMissionModel { TaskDateTime = "T2", BeginStation = "I1", EndStation = "I2", TaskSource = CrossFloorManager.CROSS_FLOOR_DISPATCH },
            };

            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                systemTasks, currentFloor: "1F", _pathCalculator, cooldown);

            Assert.AreEqual(CrossFloorDecisionKind.None, decision.Kind,
                "IDLE_RETURN 與 CROSS_FLOOR_DISPATCH 應被過濾掉，剩餘 0 normal tasks 回 None");
        }

        [TestMethod]
        public void Decide_多筆pending_應取TaskDateTime最早的當trigger()
        {
            var cooldown = new CooldownTracker(cooldownSeconds: 30);
            var earlier = new oMissionModel { TaskDateTime = "001", BeginStation = "I1", EndStation = "I2", TaskSource = "MCS" };  // 3F
            var later = new oMissionModel { TaskDateTime = "999", BeginStation = "K1", EndStation = "K2", TaskSource = "MCS" };    // 4F
            var pending = new List<oMissionModel> { later, earlier };  // 故意亂序

            var decision = CrossFloorManager.DecideNextCrossFloorAction(
                pending, currentFloor: "1F", _pathCalculator, cooldown);

            Assert.AreEqual(CrossFloorDecisionKind.NeedDispatch, decision.Kind);
            Assert.AreSame(earlier, decision.Task, "應取 TaskDateTime 最早的任務當 trigger");
            Assert.AreEqual("3F", decision.ToFloor, "trigger 為 earlier（I1→3F）");
        }

        #endregion

        // ─── Helpers ──────────────────────────────────────────────

        /// <summary>
        /// 建立最小可用 CrossFloorManager，專門給「OnCrossFloorDispatchCompleted → CooldownTracker」
        /// 這類不涉及 SQL/HikAGV 的路徑使用。若呼叫到 _mDB / _hikAGV 會 NRE。
        /// </summary>
        private CrossFloorManager CreateTestManager()
        {
            return new CrossFloorManager(
                shuttleId: "3",
                idleReturnTimeoutSeconds: 60,
                idleReturnFloor: "1F",
                mapCodeFloorMapping: "AA:1F,BB:2F,DD:3F,FF:4F",
                mDB: null,
                mLog: new Log(),
                hikAGV: null,
                elevatorSettings: new ElevatorSettings());
        }
    }
}
