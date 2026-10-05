using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// T4 cPair 區域判斷現有行為整合測試（ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-1。清除舊自動配對（T7）與移除字母清單（T14）前，先記錄一廠
    /// A / B / K / L / M 起點的 oNeed → oRequire 結果，之後這些案例必須不修改就通過。
    ///
    /// 連線：沿用 IntegrationTestBase，僅允許本機測試庫 agvDB_1400004_1。
    /// 站號：本類別使用 Port 95~99 的哨兵站（一廠各區最多到 5 號），TestCleanup 一律清除。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class GenerateoRequireByoNeed_AreaGateTests : IntegrationTestBase
    {
        // 起點：一廠五區各一個；終點：各自獨立，避免 BgnToEnd 互相干擾
        private static readonly string[] Begins = { "A95", "B95", "K95", "L95", "M95" };
        private static readonly string[] Ends = { "K96", "A96", "L96", "B96", "A97" };
        private const string DisabledEnd = "K97";   // UseFlag = N
        private const string LegacyBegin = "Z95";   // T14 用：不在現行字母清單內
        private const string LegacyEnd = "A98";
        private const string MissingBegin = "K99";  // T14 用：oPort 不存在

        private static string[] AllStations()
            => Begins.Concat(Ends).Concat(new[] { DisabledEnd, LegacyBegin, LegacyEnd, MissingBegin }).ToArray();

        private cPair _cPair;

        [TestInitialize]
        public void Setup()
        {
            CleanupAreaGateData();
            for (int i = 0; i < Begins.Length; i++)
            {
                SeedPort(Begins[i], haveFlag: "3", useFlag: "Y");
                SeedPort(Ends[i], haveFlag: "0", useFlag: "Y");
            }
            SeedPort(DisabledEnd, haveFlag: "0", useFlag: "N");

            _cPair = new cPair();
            _cPair.Initial();
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_cPair != null) _cPair.EndPair();
            CleanupAreaGateData();
        }

        // ── P0 案例 ──────────────────────────────────────────────

        [DataTestMethod]
        [DataRow(0, DisplayName = "A 區起點")]
        [DataRow(1, DisplayName = "B 區起點")]
        [DataRow(2, DisplayName = "K 區起點")]
        [DataRow(3, DisplayName = "L 區起點")]
        [DataRow(4, DisplayName = "M 區起點")]
        public void GenerateoRequireByoNeed_FactoryOneAreaBegin_ProducesoRequireAndRegistersPath(int index)
        {
            // Arrange
            string begin = Begins[index], end = Ends[index];
            string t = SeedNeed(10 + index, begin, end, workOrder: "UTESTWO");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual(1, CountoRequire(t), begin + " 起點應產生一筆 oRequire");
            Assert.AreEqual("Y", NeedFlag(t), "oNeed 應標 Y");
            Assert.AreEqual(begin + ">" + end, BgnToEnd(begin), "起點應註冊路徑");
            Assert.AreEqual(begin + ">" + end, BgnToEnd(end), "終點應註冊路徑");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_EndStationDisabled_MarksNeedE()
        {
            // Arrange
            string t = SeedNeed(20, "K95", DisabledEnd, workOrder: "UTESTWO");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual("E", NeedFlag(t), "終點停用應標 E");
            Assert.AreEqual(0, CountoRequire(t), "不應產生 oRequire");
            Assert.AreEqual("", BgnToEnd("K95"), "起點不應註冊路徑");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_AreaABeginWithEmptyWorkOrder_MarksNeedC()
        {
            // Arrange
            string t = SeedNeed(21, "A95", "K96", workOrder: "");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual("C", NeedFlag(t), "A 區工單空白應標 C");
            Assert.AreEqual(0, CountoRequire(t), "不應產生 oRequire");
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void GenerateoRequireByoNeed_NonAreaABeginWithEmptyWorkOrder_PairsNormally()
        {
            // Arrange: K 區回送空板時工單本來就是空的
            string t = SeedNeed(22, "K95", "L96", workOrder: "");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual("Y", NeedFlag(t), "非 A 區工單空白仍應正常配對");
            Assert.AreEqual(1, CountoRequire(t));
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_ExistingoRequireForSameBegin_MarksNeedX()
        {
            // Arrange: oRequire 已有同起點的殘留資料
            string old = MakeTaskDateTime(23);
            ExecNonQuery("INSERT INTO oRequire(TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource) VALUES(@t, @s, 0, @s, @e, @src)",
                P("@t", old), P("@s", "M95"), P("@e", "A97"), P("@src", UTEST_SOURCE));
            string t = SeedNeed(24, "M95", "A97", workOrder: "UTESTWO");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual("X", NeedFlag(t), "oRequire 有殘留應標 X");
            Assert.AreEqual(0, CountoRequire(t), "不應再產生 oRequire");
        }

        // ── T14 行為變更案例（B5）────────────────────────────────

        [TestMethod]
        public void GenerateoRequireByoNeed_BeginOutsideLetterList_ProducesoRequire()
        {
            // Arrange: Z 不在舊字母清單；移除清單後應與其他區一樣配對（B5）
            SeedPort(LegacyBegin, haveFlag: "3", useFlag: "Y");
            SeedPort(LegacyEnd, haveFlag: "0", useFlag: "Y");
            string t = SeedNeed(30, LegacyBegin, LegacyEnd, workOrder: "UTESTWO");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual(1, CountoRequire(t), "字母清單外的起點應產生一筆 oRequire");
            Assert.AreEqual("Y", NeedFlag(t), "oNeed 應標 Y");
            Assert.AreEqual(LegacyBegin + ">" + LegacyEnd, BgnToEnd(LegacyBegin), "起點應註冊路徑");
        }

        [TestMethod]
        public void GenerateoRequireByoNeed_BeginStationNotInoPort_MarksNeedE()
        {
            // Arrange: 起點 K99 不存在於 oPort（終點 K96 存在且空閒）
            string t = SeedNeed(31, MissingBegin, "K96", workOrder: "UTESTWO");

            // Act
            _cPair.GenerateoRequireByoNeed();

            // Assert
            Assert.AreEqual("E", NeedFlag(t), "起點不存在應標 E");
            Assert.AreEqual(0, CountoRequire(t), "不應產生 oRequire");
            Assert.AreEqual("", BgnToEnd("K96"), "終點不應註冊路徑");
        }

        // ── fixtures ─────────────────────────────────────────────

        private void SeedPort(string stationNo, string haveFlag, string useFlag)
        {
            ExecNonQuery("INSERT INTO oPort(StationNo, Area, Block, Port, UseFlag, HaveFlag, Priority, BgnToEnd) VALUES(@s, 'UTEST', @b, @p, @u, @h, 50, NULL)",
                P("@s", stationNo), P("@b", stationNo.Substring(0, 1)), P("@p", int.Parse(stationNo.Substring(1))),
                P("@u", useFlag), P("@h", haveFlag));
        }

        private string SeedNeed(int idx, string begin, string end, string workOrder)
        {
            string t = MakeTaskDateTime(idx);
            ExecNonQuery("INSERT INTO oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) VALUES(@obj, 'R001', @wo, @end, @src, @t)",
                P("@obj", begin), P("@wo", workOrder), P("@end", end), P("@src", UTEST_SOURCE), P("@t", t));
            return t;
        }

        private int CountoRequire(string taskDateTime)
            => (int)Scalar("SELECT COUNT(*) FROM oRequire WHERE TaskDateTime = @t", P("@t", taskDateTime));

        private string NeedFlag(string taskDateTime)
            => (Scalar("SELECT AssignFlag FROM oNeed WHERE TaskDateTime = @t", P("@t", taskDateTime)) ?? "").ToString().Trim();

        private string BgnToEnd(string stationNo)
            => (Scalar("SELECT BgnToEnd FROM oPort WHERE StationNo = @s", P("@s", stationNo)) ?? "").ToString().Trim();

        private void CleanupAreaGateData()
        {
            foreach (var s in AllStations())
            {
                ExecNonQuery("DELETE FROM oRequire WHERE ObjStation = @s OR BeginStation = @s OR EndStation = @s", P("@s", s));
                ExecNonQuery("DELETE FROM oMission WHERE BeginStation = @s OR EndStation = @s", P("@s", s));
                ExecNonQuery("DELETE FROM oNeed    WHERE ObjStation = @s OR EndStation = @s", P("@s", s));
                ExecNonQuery("DELETE FROM oPort    WHERE StationNo = @s", P("@s", s));
            }
        }
    }
}
