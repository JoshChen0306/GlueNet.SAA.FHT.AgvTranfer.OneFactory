using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace svrPair.Tests
{
    /// <summary>
    /// 一廠整合測試基礎建設：
    /// - 連線目標來自測試 bin 目錄的 Recipe.ini（應指向一廠測試庫 agvDB_1400004_1 @ 127.0.0.1）
    /// - 安全防呆（與二廠相反）：一廠測試庫本就在 127.0.0.1，故改以「DB 名稱白名單」判斷——
    ///   僅當 DbName == agvDB_1400004_1 才放行，其餘一律 Assert.Inconclusive 拒跑，避免誤觸現場庫。
    /// - 隔離：測試資料一律 TaskSource = 'UTEST'，測試專屬站號 A91/B92（毒筆）、A93/B94（正常筆）
    /// - TestCleanup 無論斷言成敗一律清除，跑幾次都乾淨、可重複
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected const string UTEST_SOURCE = "UTEST";
        protected const string TEST_DB_WHITELIST = "agvDB_1400004_1"; // 僅允許對此庫建刪測試資料
        protected const string TEST_BGN = "A91"; // 'A' 屬平板群組 → 會進 05.處理平板配對 → ProcessoNeedToRequire
        protected const string TEST_END = "B92";
        protected const string TEST_BGN2 = "A93"; // 第二組站號：正常筆，避免與毒筆站號碰撞被 UpdateoNeedAssignFlag 覆寫
        protected const string TEST_END2 = "B94";

        protected string ConnectionString { get; private set; }

        [TestInitialize]
        public void BaseSetup()
        {
            var (dbIp, dbName) = ReadRecipe();

            // ★ 安全防呆：一廠僅允許對測試庫 agvDB_1400004_1 建刪資料（一廠測試庫就在 127.0.0.1，不能用 IP 判斷）
            if (dbName.Trim() != TEST_DB_WHITELIST)
            {
                Assert.Inconclusive(
                    "整合測試僅允許對一廠測試庫 " + TEST_DB_WHITELIST + " 執行，目前 DbName = " + dbName + "，拒跑以保護現場資料。");
            }

            ConnectionString =
                $"Data Source={dbIp};Initial Catalog={dbName};Persist Security Info=True;User ID=mcs;Password=Zz123456;Connect Timeout=10";

            CleanupTestData();   // 先清殘留，確保乾淨起點
            SeedTestPort();      // 種測試站
        }

        [TestCleanup]
        public void BaseCleanup()
        {
            CleanupTestData();
        }

        private (string dbIp, string dbName) ReadRecipe()
        {
            // cPair.Initial() 讀的是 current directory 的 Recipe.ini，測試端讀同一份以保持一致
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Recipe.ini");
            if (!File.Exists(path))
                Assert.Inconclusive("找不到 Recipe.ini：" + path + "（測試專案需將 Recipe.ini 設為複製到輸出目錄）");

            string dbIp = "", dbName = "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith("DbIp=")) dbIp = line.Substring("DbIp=".Length).Trim();
                else if (line.StartsWith("DbName=")) dbName = line.Substring("DbName=".Length).Trim();
            }
            return (dbIp, dbName);
        }

        /// <summary>
        /// 種測試站 A91/B92/A93/B94 到 oPort：UseFlag='Y'、BgnToEnd 空，
        /// 使配對筆能通過 CheckoPortBgnToEndIsNullAndUseFlagAsY 進到 INSERT oRequire。
        /// oPort 僅 StationNo 為 NOT NULL，其餘可空；Area='UTEST' 僅供人眼識別。
        /// </summary>
        protected void SeedTestPort()
        {
            ExecNonQuery("INSERT INTO oPort(StationNo, Area, Block, Port, UseFlag, HaveFlag, Priority, BgnToEnd) VALUES(@s, 'UTEST', 'A', 91, 'Y', '1', 70, NULL)", P("@s", TEST_BGN));
            ExecNonQuery("INSERT INTO oPort(StationNo, Area, Block, Port, UseFlag, HaveFlag, Priority, BgnToEnd) VALUES(@s, 'UTEST', 'B', 92, 'Y', '0', 70, NULL)", P("@s", TEST_END));
            ExecNonQuery("INSERT INTO oPort(StationNo, Area, Block, Port, UseFlag, HaveFlag, Priority, BgnToEnd) VALUES(@s, 'UTEST', 'A', 93, 'Y', '1', 70, NULL)", P("@s", TEST_BGN2));
            ExecNonQuery("INSERT INTO oPort(StationNo, Area, Block, Port, UseFlag, HaveFlag, Priority, BgnToEnd) VALUES(@s, 'UTEST', 'B', 94, 'Y', '0', 70, NULL)", P("@s", TEST_END2));
        }

        /// <summary>
        /// 清除所有測試痕跡。注意：cPair 由 oNeed 衍生出的 oRequire/oMission 使用 TaskSource='MCS'（非 UTEST），
        /// 故必須同時以「哨兵站號」清除。A91-B94 為不存在的假站，不會誤刪正式資料。一廠無 oPortBinding。
        /// </summary>
        protected void CleanupTestData()
        {
            ExecNonQuery("DELETE FROM oRequire WHERE TaskSource = @src OR ObjStation IN (@a,@b,@a2,@b2) OR BeginStation IN (@a,@b,@a2,@b2) OR EndStation IN (@a,@b,@a2,@b2)",
                P("@src", UTEST_SOURCE), P("@a", TEST_BGN), P("@b", TEST_END), P("@a2", TEST_BGN2), P("@b2", TEST_END2));
            ExecNonQuery("DELETE FROM oMission WHERE TaskSource = @src OR BeginStation IN (@a,@b,@a2,@b2) OR EndStation IN (@a,@b,@a2,@b2)",
                P("@src", UTEST_SOURCE), P("@a", TEST_BGN), P("@b", TEST_END), P("@a2", TEST_BGN2), P("@b2", TEST_END2));
            ExecNonQuery("DELETE FROM oNeed    WHERE TaskSource = @src OR ObjStation IN (@a,@b,@a2,@b2) OR EndStation IN (@a,@b,@a2,@b2)",
                P("@src", UTEST_SOURCE), P("@a", TEST_BGN), P("@b", TEST_END), P("@a2", TEST_BGN2), P("@b2", TEST_END2));
            ExecNonQuery("DELETE FROM oPort    WHERE StationNo IN (@a,@b,@a2,@b2)",
                P("@a", TEST_BGN), P("@b", TEST_END), P("@a2", TEST_BGN2), P("@b2", TEST_END2));
        }

        // ── 測試用小工具（一律參數化）────────────────────────────

        protected static SqlParameter P(string name, object value) => new SqlParameter(name, value ?? DBNull.Value);

        protected int ExecNonQuery(string sql, params SqlParameter[] ps)
        {
            using (var c = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                c.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        protected object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var c = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, c))
            {
                if (ps != null) cmd.Parameters.AddRange(ps);
                c.Open();
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// 產生 UTEST 專屬 TaskDateTime（純數字）。idx(0~99) 作 3 位後綴。
        /// oNeed/oRequire.TaskDateTime 為 nvarchar(20)，須維持「17 碼時間 + 3 碼後綴 = 20 碼」。
        /// </summary>
        protected static string MakeTaskDateTime(int idx)
            => DateTime.Now.ToString("yyyyMMddHHmmssfff") + (100 + idx).ToString();
    }
}
