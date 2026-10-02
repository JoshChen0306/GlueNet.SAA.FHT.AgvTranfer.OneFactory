using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SCP.Tests.Helpers
{
    /// <summary>
    /// T11 AreaRuleProvider 測試骨架（ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-3。Red 階段依實際型別（SCP.Helpers.AreaRuleProvider）補齊 Arrange/Act；
    /// 設定以 ConfigurationBuilder.AddInMemoryCollection 建立，比照 ChargingStationProviderTests。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class AreaRuleProviderTests
    {
        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void GetReleaseTargets_ConfiguredArea_ReturnsConfiguredTargets()
        {
            // Arrange: AreaRules:ReleaseRoutes:K:0 = "L"
            // Act:     GetReleaseTargets("K")
            // Assert:  ["L"]
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }

        [TestMethod]
        public void GetReleaseTargets_MultipleTargets_KeepsConfiguredOrder()
        {
            // Arrange: AreaRules:ReleaseRoutes:X = ["M", "Q", "R"]
            // Act:     GetReleaseTargets("X")
            // Assert:  順序為 M、Q、R
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }

        [TestMethod]
        public void GetReleaseTargets_UnconfiguredArea_ReturnsEmptyAndIsNotReleaseArea()
        {
            // Arrange: 一廠設定（K、M、B）
            // Act:     GetReleaseTargets("Z")、IsReleaseArea("Z")
            // Assert:  空清單；false
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }

        [TestMethod]
        public void IsRegisterAreaAndIsRackIdRequired_FactoryOneConfig_MatchCurrentBehavior()
        {
            // Arrange: RegisterAreas = [A, L]；RackIdRequiredAreas = [K, L]
            // Act:     逐區查詢
            // Assert:  A、L 為登記區；K、L 貨架條碼必填；其餘皆否
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void ClickableAreas_FactoryOneConfig_IsUnionOfRegisterAndReleaseAreas()
        {
            // TODO: 一廠設定 → A、L、K、M、B；不含 C
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }

        [TestMethod]
        public void Constructor_MissingAreaRulesSection_ReturnsEmptyRulesWithoutThrowing()
        {
            // TODO: 設定無 AreaRules 區段 → 所有查詢回傳空或 false，不拋例外
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }

        [TestMethod]
        public void Lookup_LowerCaseOrPaddedValues_AreNormalized()
        {
            // TODO: 設定值為 " k " 或查詢 "k" → 與 "K" 視為相同
            Assert.Inconclusive("TODO: T11 Red 階段補實作");
        }
    }
}
