using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SCP.Tests.Controllers
{
    /// <summary>
    /// T2 Release 現有行為快照測試骨架（ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-1。重構前先記錄一廠三條回送路線（K→L、M→A、B→A）的現有結果。
    /// T2 先建立 SQLite 記憶體資料庫的測試基礎建設，再補齊 Arrange/Act。
    /// T12 區段為行為變更案例（spec.md 行為變更清單 B1），於 T12 補實作。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class DispatchControllerReleaseTests
    {
        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void Release_FromKArea_PicksEmptySlotInLArea()
        {
            // Arrange: K1 HaveFlag=1；L1、L2 HaveFlag=0、UseFlag=Y、BgnToEnd 空
            // Act:     Release(stationNo = "K1")
            // Assert:  回 200；oNeed 一筆，ObjStation=K1，EndStation 為 L 區空位
            Assert.Inconclusive("TODO: T2 補實作（SQLite 記憶體資料庫）");
        }

        [TestMethod]
        public void Release_FromMArea_PicksEmptySlotInAArea()
        {
            // Arrange: M1 HaveFlag=1；A 區有空位
            // Act:     Release(stationNo = "M1")
            // Assert:  EndStation 為 A 區空位
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_FromBArea_PicksEmptySlotInAArea()
        {
            // Arrange: B1 HaveFlag=1；A 區有空位
            // Act:     Release(stationNo = "B1")
            // Assert:  EndStation 為 A 區空位
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_MultipleEmptySlots_PicksHighestPriorityThenLowestPort()
        {
            // Arrange: 目的區三個空位，Priority 與 Port 各異
            // Act:     Release
            // Assert:  終點 = Priority 最高者；同 Priority 取 Port 最小者
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_Success_CreatesNeedWithSourceRackIdAndWebSource()
        {
            // Arrange: 起點站 RackId = "Rack77"
            // Act:     Release
            // Assert:  oNeed.RackId = "Rack77"、WorkOrder 空、TaskSource = "Web"、AssignFlag 空
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_NoEmptySlotInTargetArea_ReturnsBadRequestAndCreatesNoNeed()
        {
            // Arrange: 目的區全部 HaveFlag != 0
            // Act:     Release
            // Assert:  回 400；oNeed 無新增
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_SourceNotEmptyTray_ReturnsBadRequest()
        {
            // Arrange: 起點 HaveFlag = 3
            // Act:     Release
            // Assert:  回 400「請先標記為空板」
            Assert.Inconclusive("TODO: T2 補實作");
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void Release_SourceAlreadyHasPendingNeed_ReturnsBadRequest()
        {
            // TODO: oNeed 已有同起點且 AssignFlag 空的資料 → 回 400，不重複建立
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_TargetSlotAlreadyPendingEndStation_SkipsThatSlot()
        {
            // TODO: 最優先的空位已是 pending oNeed 的終點 → 選下一個空位
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_UserHasRoutesButNoReleasePermission_ReturnsForbidden()
        {
            // TODO: 使用者有 pUserRoute 但無該區 RELEASE 路線 → 回 403
            Assert.Inconclusive("TODO: T2 補實作");
        }

        [TestMethod]
        public void Release_StationNotFound_ReturnsBadRequest()
        {
            // TODO: oPort 查無此站 → 回 400「站點不存在」
            Assert.Inconclusive("TODO: T2 補實作");
        }

        // ── T12 行為變更案例（B1）────────────────────────────────

        [TestMethod]
        public void Release_AreaWithoutReleaseRule_ReturnsBadRequestAndCreatesNoNeed()
        {
            // Arrange: 站點區域不在 AreaRules.ReleaseRoutes
            // Act:     Release
            // Assert:  回 400「此區域未設定回送路線」；oNeed 無新增
            Assert.Inconclusive("TODO: T12 補實作");
        }

        [TestMethod]
        public void Release_NewAreaAddedByConfigOnly_PicksSlotInConfiguredTarget()
        {
            // Arrange: 設定加入假區域 X→[Y]，並建立 X、Y 區站點
            // Act:     Release(stationNo = X 區站點)
            // Assert:  終點為 Y 區空位（不需改程式）
            Assert.Inconclusive("TODO: T12 補實作");
        }
    }
}
