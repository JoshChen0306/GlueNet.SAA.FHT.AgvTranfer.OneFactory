using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Helpers;

namespace SCP.Tests.Helpers
{
    /// <summary>
    /// T11 AreaRuleProvider 測試（ToDo/20261001_清除二廠與舊專案殘留區域代號）
    /// 對應 spec.md AC-3。設定以 ConfigurationBuilder.AddInMemoryCollection 建立，比照 ChargingStationProviderTests。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("LegacyCleanup")]
    public class AreaRuleProviderTests
    {
        /// <summary>一廠 appsettings 的 AreaRules。</summary>
        private static Dictionary<string, string?> FactoryOneRules() => new Dictionary<string, string?>
        {
            ["AreaRules:RegisterAreas:0"] = "A",
            ["AreaRules:RegisterAreas:1"] = "L",
            ["AreaRules:RackIdRequiredAreas:0"] = "K",
            ["AreaRules:RackIdRequiredAreas:1"] = "L",
            ["AreaRules:ReleaseRoutes:K:0"] = "L",
            ["AreaRules:ReleaseRoutes:M:0"] = "A",
            ["AreaRules:ReleaseRoutes:B:0"] = "A",
        };

        private static AreaRuleProvider Build(Dictionary<string, string?> dict)
            => new AreaRuleProvider(new ConfigurationBuilder().AddInMemoryCollection(dict).Build());

        // ── P0 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void GetReleaseTargets_ConfiguredArea_ReturnsConfiguredTargets()
        {
            // Arrange
            var provider = Build(FactoryOneRules());

            // Act
            var targets = provider.GetReleaseTargets("K");

            // Assert
            CollectionAssert.AreEqual(new[] { "L" }, targets.ToArray());
            Assert.IsTrue(provider.IsReleaseArea("K"));
        }

        [TestMethod]
        public void GetReleaseTargets_MultipleTargets_KeepsConfiguredOrder()
        {
            // Arrange
            var rules = FactoryOneRules();
            rules["AreaRules:ReleaseRoutes:X:0"] = "M";
            rules["AreaRules:ReleaseRoutes:X:1"] = "Q";
            rules["AreaRules:ReleaseRoutes:X:2"] = "R";
            var provider = Build(rules);

            // Act
            var targets = provider.GetReleaseTargets("X");

            // Assert
            CollectionAssert.AreEqual(new[] { "M", "Q", "R" }, targets.ToArray());
        }

        [TestMethod]
        public void GetReleaseTargets_UnconfiguredArea_ReturnsEmptyAndIsNotReleaseArea()
        {
            // Arrange
            var provider = Build(FactoryOneRules());

            // Act
            var targets = provider.GetReleaseTargets("Z");

            // Assert
            Assert.AreEqual(0, targets.Count);
            Assert.IsFalse(provider.IsReleaseArea("Z"));
            Assert.IsFalse(provider.IsReleaseArea("A"));
        }

        [TestMethod]
        public void IsRegisterAreaAndIsRackIdRequired_FactoryOneConfig_MatchCurrentBehavior()
        {
            // Arrange
            var provider = Build(FactoryOneRules());

            // Act & Assert
            Assert.IsTrue(provider.IsRegisterArea("A"));
            Assert.IsTrue(provider.IsRegisterArea("L"));
            Assert.IsFalse(provider.IsRegisterArea("K"));
            Assert.IsFalse(provider.IsRegisterArea("C"));

            Assert.IsTrue(provider.IsRackIdRequired("K"));
            Assert.IsTrue(provider.IsRackIdRequired("L"));
            Assert.IsFalse(provider.IsRackIdRequired("A"));
            Assert.IsFalse(provider.IsRackIdRequired("M"));
        }

        // ── P1 案例 ──────────────────────────────────────────────

        [TestMethod]
        public void ClickableAreas_FactoryOneConfig_IsUnionOfRegisterAndReleaseAreas()
        {
            // Arrange
            var provider = Build(FactoryOneRules());

            // Act
            var clickable = provider.ClickableAreas;

            // Assert
            CollectionAssert.AreEquivalent(new[] { "A", "L", "K", "M", "B" }, clickable.ToArray());
            CollectionAssert.DoesNotContain(clickable.ToArray(), "C");
        }

        [TestMethod]
        public void Constructor_MissingAreaRulesSection_ReturnsEmptyRulesWithoutThrowing()
        {
            // Arrange
            var provider = Build(new Dictionary<string, string?>());

            // Act & Assert
            Assert.AreEqual(0, provider.GetReleaseTargets("K").Count);
            Assert.IsFalse(provider.IsReleaseArea("K"));
            Assert.IsFalse(provider.IsRegisterArea("A"));
            Assert.IsFalse(provider.IsRackIdRequired("L"));
            Assert.AreEqual(0, provider.ClickableAreas.Count);
        }

        [TestMethod]
        public void GetAreaLabel_WithAndWithoutConfiguredName_FormatsLabel()
        {
            // Arrange: 只有 L 設定顯示名稱
            var rules = FactoryOneRules();
            rules["AreaRules:AreaNames:L"] = "3F下料區";
            var provider = Build(rules);

            // Act & Assert
            Assert.AreEqual("L區（3F下料區）", provider.GetAreaLabel("l"));
            Assert.AreEqual("A區", provider.GetAreaLabel("A"));
        }

        [TestMethod]
        public void Lookup_LowerCaseOrPaddedValues_AreNormalized()
        {
            // Arrange
            var provider = Build(new Dictionary<string, string?>
            {
                ["AreaRules:RegisterAreas:0"] = " a ",
                ["AreaRules:RackIdRequiredAreas:0"] = "l",
                ["AreaRules:ReleaseRoutes:k:0"] = " l ",
            });

            // Act & Assert
            Assert.IsTrue(provider.IsRegisterArea("A"));
            Assert.IsTrue(provider.IsRackIdRequired(" L"));
            Assert.IsTrue(provider.IsReleaseArea("K"));
            CollectionAssert.AreEqual(new[] { "L" }, provider.GetReleaseTargets("k").ToArray());
        }
    }
}
