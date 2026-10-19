using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Helpers;

namespace SCP.Tests.Helpers
{
    /// <summary>
    /// T27 加工完成標記（ToDo/20260726_一廠AB棟1F新路線與邏輯，spec.md AC-3a）
    /// 加工站區域登記物料時在 WorkOrder 附加 ^DONE，R2 只派有標記的站；建任務與畫面顯示時移除。
    /// 比照二廠 2F V Cut 區 ^VCUT^DONE 做法。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("AB1F")]
    public class WorkOrderMarkerTests
    {
        [TestMethod]
        public void MarkProcessDone_PlainWorkOrder_AppendsMarker()
        {
            Assert.AreEqual("WO-1^DONE", WorkOrderMarker.MarkProcessDone("WO-1"));
        }

        [TestMethod]
        public void MarkProcessDone_AlreadyMarked_DoesNotAppendTwice()
        {
            Assert.AreEqual("WO-1^DONE", WorkOrderMarker.MarkProcessDone("WO-1^DONE"));
        }

        [TestMethod]
        public void Strip_MarkedWorkOrder_RemovesMarker()
        {
            Assert.AreEqual("WO-1", WorkOrderMarker.Strip("WO-1^DONE"));
        }

        [TestMethod]
        public void Strip_NullOrPlain_ReturnsAsIs()
        {
            Assert.AreEqual("", WorkOrderMarker.Strip(null));
            Assert.AreEqual("FHT^N01^238090671", WorkOrderMarker.Strip("FHT^N01^238090671"));
        }

        [TestMethod]
        public void Strip_MarkerInMiddleOfBarcode_KeepsBarcode()
        {
            // 只處理結尾的標記，條碼中間剛好含 ^DONE 不可被移除
            Assert.AreEqual("A^DONEB", WorkOrderMarker.Strip("A^DONEB"));
            Assert.IsFalse(WorkOrderMarker.HasProcessDone("A^DONEB"));
        }

        [TestMethod]
        public void Strip_LegacyVcutDone_KeptUnchanged()
        {
            // 二廠舊字樣 ^VCUT^DONE 不是本標記（前置清理計畫 B6：舊標記原樣保存）
            Assert.AreEqual("LOT01^VCUT^DONE", WorkOrderMarker.Strip("LOT01^VCUT^DONE"));
            Assert.IsFalse(WorkOrderMarker.HasProcessDone("LOT01^VCUT^DONE"));
        }

        [TestMethod]
        public void HasProcessDone_MarkedAndPlain_ReturnsExpected()
        {
            Assert.IsTrue(WorkOrderMarker.HasProcessDone("WO-1^DONE"));
            Assert.IsFalse(WorkOrderMarker.HasProcessDone("WO-1"));
            Assert.IsFalse(WorkOrderMarker.HasProcessDone(null));
        }
    }
}
