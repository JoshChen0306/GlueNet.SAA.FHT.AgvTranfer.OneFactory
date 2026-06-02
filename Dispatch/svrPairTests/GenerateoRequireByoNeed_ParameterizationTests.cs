using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// Layer A — 參數化正確性 + Layer C — Happy path 回歸。
    /// 紅燈基準（C3 參數化前）：含單引號工單打斷拼接 SQL → 拋例外。綠燈（C3 後）：原值 round-trip。
    /// </summary>
    [TestClass]
    public class GenerateoRequireByoNeed_ParameterizationTests : IntegrationTestBase
    {
        private cPair _cPair;

        [TestInitialize]
        public void Setup()
        {
            _cPair = new cPair();
            _cPair.Initial();
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_WorkOrderWithSingleQuote_PreservedAndNoThrow()
        {
            const string wo = "O'Brien";
            string t = SeedPairRow(idx: 1, workOrder: wo, rackId: "R001");

            _cPair.GenerateoRequireByoNeed();

            var stored = Scalar("SELECT WorkOrder FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.IsNotNull(stored, "含單引號工單應成功產生 oRequire（改前此處會拋例外）");
            Assert.AreEqual(wo, stored.ToString(), "WorkOrder 原值應完整保存");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_SqlInjectionString_NeutralisedAndTableSurvives()
        {
            // ⚠️ payload 不可帶 ';' / DROP / DELETE / UPDATE（拼接版會真的執行破壞語句）。
            //    採「含單引號 + OR」的注入字串：拼接時只造成語法錯誤、不執行；參數化後原樣存入。
            const string wo = "x' OR '1'='1";
            string t = SeedPairRow(idx: 1, workOrder: wo, rackId: "R002");

            _cPair.GenerateoRequireByoNeed();

            var stored = Scalar("SELECT WorkOrder FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual(wo, (stored ?? "").ToString(), "注入字串應原樣存入、未被執行");

            var exists = Scalar("SELECT OBJECT_ID('oRequire')");
            Assert.IsTrue(exists != null && exists != System.DBNull.Value, "oRequire 表必須仍存在");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_RackIdWithSingleQuote_PreservedAndNoThrow()
        {
            const string rack = "RK'01";
            string t = SeedPairRow(idx: 1, workOrder: "UTESTWO", rackId: rack);

            _cPair.GenerateoRequireByoNeed();

            var stored = Scalar("SELECT RackId FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual(rack, (stored ?? "").ToString(), "含單引號 RackId 原值應完整保存");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_NormalRow_ProducesoRequireAndFlags()
        {
            // Layer C — Happy path 回歸
            string t = SeedPairRow(idx: 1, workOrder: "UTESTWO", rackId: "R001");

            _cPair.GenerateoRequireByoNeed();

            var cnt = (int)Scalar("SELECT COUNT(*) FROM oRequire WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual(1, cnt, "正常筆應產生一筆 oRequire");

            var flag = Scalar("SELECT AssignFlag FROM oNeed WHERE TaskDateTime = @t", P("@t", t));
            Assert.AreEqual("Y", (flag ?? "").ToString().Trim(), "正常筆 oNeed 應標 Y");

            var bgn = Scalar("SELECT BgnToEnd FROM oPort WHERE StationNo = @s", P("@s", TEST_BGN));
            Assert.AreEqual(TEST_BGN + ">" + TEST_END, (bgn ?? "").ToString().Trim(), "oPort 應註冊路徑");
        }

        // ── fixtures ─────────────────────────────────────────────

        /// <summary>種一筆 A91→B92 配對 oNeed（WorkOrder 非空，確保不走空工單取消路徑）。</summary>
        private string SeedPairRow(int idx, string workOrder, string rackId)
        {
            string t = MakeTaskDateTime(idx);
            ExecNonQuery(
                "INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) VALUES(@obj, @rack, @wo, @end, @src, @t)",
                P("@obj", TEST_BGN), P("@rack", rackId), P("@wo", workOrder), P("@end", TEST_END), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_cPair != null) _cPair.EndPair();
        }
    }
}
