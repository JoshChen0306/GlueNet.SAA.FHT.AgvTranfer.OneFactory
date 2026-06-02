using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// Layer B — 韌性測試（單筆隔離）。一廠 oNeed.RackId=nvarchar(50)、oRequire.RackId=nvarchar(20)，
    /// RackId 30 字的 oNeed 對 oNeed 合法、塞 oRequire 會 truncation 例外（參數化也照炸），
    /// 用來驗證 try/catch 是否真的隔離該筆、不拖垮整輪。
    /// </summary>
    [TestClass]
    public class GenerateoRequireByoNeed_ResilienceTests : IntegrationTestBase
    {
        private cPair _cPair;

        [TestInitialize]
        public void Setup()
        {
            _cPair = new cPair();
            _cPair.Initial();
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_PoisonRowThenValidRow_DoesNotThrow()
        {
            SeedPoisonRow(idx: 1);
            SeedValidRow(idx: 2);

            _cPair.GenerateoRequireByoNeed(); // 走到這行未拋例外即代表第 1 層 try/catch 生效
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_PoisonRow_IsQuarantinedAsX_ValidRowStillProcessed()
        {
            string poisonTaskTime = SeedPoisonRow(idx: 1);
            SeedValidRow(idx: 2);

            _cPair.GenerateoRequireByoNeed();

            var flag = Scalar("SELECT AssignFlag FROM oNeed WHERE TaskDateTime = @t", P("@t", poisonTaskTime));
            Assert.AreEqual("X", (flag ?? "").ToString().Trim(), "毒資料應被隔離標記為 X");

            var cnt = (int)Scalar("SELECT COUNT(*) FROM oRequire WHERE ObjStation = @s", P("@s", TEST_BGN2));
            Assert.IsTrue(cnt >= 1, "毒筆之後的正常筆仍應被處理、產生 oRequire");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_OnException_LogsOffendingRowDetail()
        {
            // 功能已於 cPair.HandleoNeedRowException 實作（06E 含站號/工單/RackId/Error），
            // 自動化 log 攔截待補，先以人工檢視 log 驗收。
            Assert.Inconclusive("log 攔截手法待補；功能已實作，人工檢視 log 驗證。");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_TransientConnectionError_DoesNotQuarantine()
        {
            // 功能已於 cPair.IsDataException 實作（非資料類例外→不隔離保留重試），
            // 連線類例外穩定觸發手法待補。
            Assert.Inconclusive("連線類例外模擬手法待補；功能已實作。");
        }

        // ── fixtures ─────────────────────────────────────────────

        /// <summary>毒筆：RackId 30 字（oRequire(20) 容不下），站號 A91→B92。</summary>
        private string SeedPoisonRow(int idx)
        {
            string t = MakeTaskDateTime(idx);
            string rack30 = new string('R', 30);
            ExecNonQuery(
                "INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) VALUES(@obj, @rack, @wo, @end, @src, @t)",
                P("@obj", TEST_BGN), P("@rack", rack30), P("@wo", "UTESTWO"), P("@end", TEST_END), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        /// <summary>正常筆：合法長度，站號 A93→B94（與毒筆不碰撞）。</summary>
        private string SeedValidRow(int idx)
        {
            string t = MakeTaskDateTime(idx);
            ExecNonQuery(
                "INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) VALUES(@obj, @rack, @wo, @end, @src, @t)",
                P("@obj", TEST_BGN2), P("@rack", "R001"), P("@wo", "UTESTWO"), P("@end", TEST_END2), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_cPair != null) _cPair.EndPair();
        }
    }
}
