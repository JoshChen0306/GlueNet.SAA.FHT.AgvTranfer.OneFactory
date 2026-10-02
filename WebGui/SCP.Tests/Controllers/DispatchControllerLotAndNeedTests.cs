using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SCP.Tests.Controllers
{
    /// <summary>
    /// T3 RegisterLot / InsertoNeed / Index 現有行為快照測試骨架
    /// （ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-1。沿用 T2 建立的 SQLite 記憶體資料庫基礎建設。
    /// T12 區段為行為變更案例（spec.md 行為變更清單 B6、B7），於 T12 補實作。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class DispatchControllerLotAndNeedTests
    {
        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void RegisterLot_AreaAEmptyStation_SetsHaveFlag3AndWritesLotInfo()
        {
            // Arrange: A1 HaveFlag=0；workOrder、rackId 皆有值
            // Act:     RegisterLot
            // Assert:  回 200；oPort A1 HaveFlag=3，WorkOrder / RackId 寫入，PutTime 非空
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void RegisterLot_EmptyWorkOrder_ReturnsBadRequest()
        {
            // Arrange: A1；workOrder 空
            // Act:     RegisterLot
            // Assert:  回 400「請輸入工單條碼」；oPort 不變
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void RegisterLot_AreaKOrLWithEmptyRackId_ReturnsBadRequest()
        {
            // Arrange: L1（另一案例 K1）；rackId 空
            // Act:     RegisterLot
            // Assert:  回 400「請輸入貨架條碼」
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void InsertoNeed_Normal_InsertsNeedWithWebSource()
        {
            // Arrange: A1 → K1，帶工單與 RackId
            // Act:     InsertoNeed
            // Assert:  oNeed 一筆，ObjStation=A1、EndStation=K1、TaskSource="Web"、AssignFlag 空
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void InsertoNeed_EmptyWorkOrder_UsesWorkOrderFromSourcePort()
        {
            // Arrange: 請求 WorkOrder 空；oPort A1.WorkOrder = "LOT0005"
            // Act:     InsertoNeed
            // Assert:  oNeed.WorkOrder = "LOT0005"
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void InsertoNeed_EmptyEndStation_ReturnsBadRequestAndInsertsNothing()
        {
            // Arrange: EndStation 空
            // Act:     InsertoNeed
            // Assert:  回 400；oNeed 無新增
            Assert.Inconclusive("TODO: T3 補實作");
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void RegisterLot_RackIdSentinelMinusOne_PassesValidation()
        {
            // TODO: L1、rackId = "-1"（隱藏貨架條碼欄位時的哨兵值）→ 通過，RackId 寫入 "-1"
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void RegisterLot_StationIsPendingEndStation_ReturnsBadRequest()
        {
            // TODO: oNeed 有以此站為終點的待處理資料 → 回 400，oPort 不變
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void RegisterLot_StationHasRunningTask_ReturnsBadRequest()
        {
            // TODO: oMission 有以此站為起點的進行中任務 → 回 400，oPort 不變
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void Index_UserWithDispatchRoutes_AreaListContainsOnlyRouteSources()
        {
            // TODO: 使用者有 A、L 的 DISPATCH 路線 → ViewBag.AreaList 只有 A、L
            Assert.Inconclusive("TODO: T3 補實作");
        }

        [TestMethod]
        public void Index_UserWithoutRoutesInGroup2_AreaListContainsOnlyAreaA()
        {
            // TODO: 使用者無 pUserRoute、群組 2 → ViewBag.AreaList 只有 A
            Assert.Inconclusive("TODO: T3 補實作");
        }

        // ── T12 行為變更案例（B6、B7）────────────────────────────

        [TestMethod]
        public void RegisterLot_WorkOrderContainingLegacyTagText_IsStoredUnchanged()
        {
            // TODO: 工單含 "^NG" 等字樣 → 原樣寫入，不再被移除（B6）
            Assert.Inconclusive("TODO: T12 補實作");
        }

        [TestMethod]
        public void RegisterLot_RackIdRequiredAreaFromConfig_EmptyRackIdReturnsBadRequest()
        {
            // TODO: AreaRules.RackIdRequiredAreas 加入假區域 → 該區 RackId 空白回 400（B7）
            Assert.Inconclusive("TODO: T12 補實作");
        }
    }
}
