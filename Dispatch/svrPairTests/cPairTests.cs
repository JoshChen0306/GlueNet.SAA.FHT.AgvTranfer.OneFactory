using Microsoft.VisualStudio.TestTools.UnitTesting;
using svrPair;
using System;
using System.Data;

namespace svrPair.Tests
{
    [TestClass()]
    public class cPairTests
    {
        private cPair _cPair;

        [TestInitialize]
        public void Setup()
        {
            // 需要先準備測試資料庫環境
            _cPair = new cPair();
            _cPair.Initial();
            // 注意: 這需要 Initial() 方法是 public 或透過 BgnPair() 初始化
            // _cPair.BgnPair(); // 這會啟動執行緒,不適合單元測試
        }

        [TestMethod()]
        public void GetoPort_NoRack_NoPair_CanWork_Sort_ByBlockTest_SingleBlock()
        {
            // Arrange
            string blockA = "'A'";

            // Act
            DataTable result = _cPair.GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock(blockA);

            Assert.IsTrue(result.Rows.Count == 0);
            // Assert
            Assert.IsNotNull(result, "返回的 DataTable 不應為 null");
            
            // 檢查資料表結構
            Assert.IsTrue(result.Columns.Contains("StationNo"), "應包含 StationNo 欄位");
            Assert.IsTrue(result.Columns.Contains("UseFlag"), "應包含 UseFlag 欄位");
            Assert.IsTrue(result.Columns.Contains("HaveFlag"), "應包含 HaveFlag 欄位");
            Assert.IsTrue(result.Columns.Contains("BgnToEnd"), "應包含 BgnToEnd 欄位");
            Assert.IsTrue(result.Columns.Contains("Block"), "應包含 Block 欄位");
            Assert.IsTrue(result.Columns.Contains("Priority"), "應包含 Priority 欄位");

            // 檢查資料條件
            foreach (DataRow row in result.Rows)
            {
                Assert.AreEqual("Y", row["UseFlag"].ToString(), "UseFlag 應為 Y");
                Assert.AreEqual("0", row["HaveFlag"].ToString(), "HaveFlag 應為 0 (沒有 Rack)");
                Assert.AreEqual("A", row["Block"].ToString(), "Block 應為 A");
                
                string bgnToEnd = row["BgnToEnd"].ToString().Trim();
                Assert.IsTrue(string.IsNullOrEmpty(bgnToEnd), "BgnToEnd 應為空或 null (未被註冊)");
            }

            // 檢查排序 (Priority DESC)
            if (result.Rows.Count > 1)
            {
                for (int i = 0; i < result.Rows.Count - 1; i++)
                {
                    int currentPriority = Convert.ToInt32(result.Rows[i]["Priority"]);
                    int nextPriority = Convert.ToInt32(result.Rows[i + 1]["Priority"]);
                    Assert.IsTrue(currentPriority >= nextPriority, "應按 Priority 降冪排序");
                }
            }
        }

        [TestMethod()]
        public void GetoPort_NoRack_NoPair_CanWork_Sort_ByBlockTest_MultipleBlocks()
        {
            // Arrange
            string blocks = "'A','B','D'";

            // Act
            DataTable result = _cPair.GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock(blocks);

            // Assert
            Assert.IsNotNull(result);

            foreach (DataRow row in result.Rows)
            {
                string block = row["Block"].ToString();
                Assert.IsTrue(
                    block == "A" || block == "B" || block == "D",
                    $"Block 應為 A、B 或 D,但實際為 {block}"
                );
            }
        }

        [TestMethod()]
        public void GetoPort_NoRack_NoPair_CanWork_Sort_ByBlockTest_EmptyResult()
        {
            // Arrange
            string blockZ = "'Z'"; // 不存在的區域

            // Act
            DataTable result = _cPair.GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock(blockZ);

            // Assert
            Assert.IsNotNull(result, "即使沒有資料,也應返回空的 DataTable");
            Assert.AreEqual(0, result.Rows.Count, "不存在的區域應返回 0 筆資料");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // 清理資源
            if (_cPair != null)
            {
                _cPair.EndPair();
            }
        }
    }
}