using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SCP.Tests.Config
{
    /// <summary>
    /// T3 一廠 AB 棟 1F 區域設定檢查（ToDo/20260726_一廠AB棟1F新路線與邏輯，對應 spec.md AC-1「新區域代碼與既有 Block 無衝突」）
    /// 直接讀取 SCP 專案的 appsettings.json，防止日後改設定時區域重複或漏掉新區域。
    /// 區域代碼依據：區域代碼對照表.md（T1）。
    /// 命名規則：MethodName_Scenario_ExpectedResult
    /// </summary>
    [TestClass]
    [TestCategory("ABBuilding1F")]
    public class AppSettingsAreaConfigTests
    {
        /// <summary>AB 棟新區域：中光電 A/B/C/D = O/P/Z/D，迅得 2~4 = E，迅得 5~7 = F。</summary>
        private static readonly string[] AbBuildingBlocks = { "D", "E", "F", "O", "P", "Z" };

        /// <summary>ACC FHtSetting.config 電梯點字母，站點不可同名。</summary>
        private static readonly string[] ElevatorLetters = { "U", "V", "W", "X", "Y" };

        private static IConfiguration LoadScpAppSettings()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCP", "appsettings.json")))
            {
                dir = dir.Parent;
            }
            Assert.IsNotNull(dir, "找不到 SCP/appsettings.json");

            return new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(dir!.FullName, "SCP", "appsettings.json"), optional: false)
                .Build();
        }

        private static Dictionary<string, string[]> FloorAreas(IConfiguration config)
            => config.GetSection("FloorSettings").GetChildren()
                .ToDictionary(f => f.Key, f => f.GetSection("Areas").Get<string[]>() ?? Array.Empty<string>());

        [TestMethod]
        public void FloorSettings_AbBuildingBlocks_AllOn1F()
        {
            var floors = FloorAreas(LoadScpAppSettings());

            CollectionAssert.IsSubsetOf(AbBuildingBlocks, floors["FHT1-1F"]);
        }

        [TestMethod]
        public void FloorSettings_EachBlock_BelongsToOnlyOneFloor()
        {
            var floors = FloorAreas(LoadScpAppSettings());

            var duplicated = floors.SelectMany(f => f.Value)
                .GroupBy(a => a)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.AreEqual(0, duplicated.Count, "重複的區域：" + string.Join(",", duplicated));
        }

        [TestMethod]
        public void FloorSettings_AllBlocks_SingleLetterAndNotElevatorPoint()
        {
            var floors = FloorAreas(LoadScpAppSettings());

            foreach (var block in floors.SelectMany(f => f.Value))
            {
                Assert.AreEqual(1, block.Length, $"區域 {block} 不是單一字母（站號第一個字元即區域）");
                CollectionAssert.DoesNotContain(ElevatorLetters, block, $"區域 {block} 與電梯點字母相同");
            }
        }

        [TestMethod]
        public void AreaSection_DispatchSourceAreas_AllDefinedInFloorSettings()
        {
            var config = LoadScpAppSettings();
            var allBlocks = FloorAreas(config).SelectMany(f => f.Value).ToHashSet();
            var dispatchAreas = config.GetSection("Area").Get<Dictionary<string, string>>()!;

            foreach (var area in dispatchAreas.Values)
            {
                Assert.IsTrue(allBlocks.Contains(area), $"派送區域 {area} 未列在 FloorSettings");
            }
            // R1、R2、R4 的起點區（R3 起點 B 為既有區域）
            CollectionAssert.IsSubsetOf(new[] { "D", "E", "O" }, dispatchAreas.Values.ToList());
        }

        [TestMethod]
        public void AutoDispatchRoutes_AllBlocksDefinedAndMToKKeptEnabled()
        {
            var config = LoadScpAppSettings();
            var allBlocks = FloorAreas(config).SelectMany(f => f.Value).ToHashSet();
            var routes = config.GetSection("AutoDispatch:Routes").Get<List<SCP.Services.AutoDispatchRoute>>()!;

            foreach (var route in routes)
            {
                foreach (var block in route.SourceBlocks.Concat(route.TargetBlocks))
                {
                    Assert.IsTrue(allBlocks.Contains(block), $"路線 {route.Name} 的區域 {block} 未列在 FloorSettings");
                }
            }
            var mToK = routes.Single(r => r.SourceBlocks.SequenceEqual(new[] { "M" }));
            Assert.IsTrue(mToK.Enabled);
            CollectionAssert.AreEqual(new[] { "K" }, mToK.TargetBlocks);
            Assert.AreEqual(0, mToK.SourceHaveFlags.Count, "M→K 須維持預設只認 HaveFlag=3");
            Assert.IsFalse(mToK.SensorGuard, "M→K 不開防呆，維持重構前行為");
        }

        /// <summary>
        /// 2026-10-12 定案：R1 物料須先由 status 補成 HaveFlag=3＋工單才派；R4 空盤不查 status，HaveFlag=1 即派。
        /// </summary>
        [TestMethod]
        public void AutoDispatchRoutes_R1OnlyHaveFlag3_R4OnlyHaveFlag1()
        {
            var routes = LoadScpAppSettings().GetSection("AutoDispatch:Routes").Get<List<SCP.Services.AutoDispatchRoute>>()!;

            var r1 = routes.Single(r => r.SourceBlocks.SequenceEqual(new[] { "D" }));
            CollectionAssert.AreEqual(new[] { "E" }, r1.TargetBlocks);
            CollectionAssert.AreEqual(new[] { "3" }, r1.SourceHaveFlags, "R1 須等 status 補完工單（HaveFlag=3）才派");
            Assert.IsTrue(r1.SensorGuard);

            var r4 = routes.Single(r => r.SourceBlocks.SequenceEqual(new[] { "O" }));
            CollectionAssert.AreEqual(new[] { "F" }, r4.TargetBlocks);
            CollectionAssert.AreEqual(new[] { "1" }, r4.SourceHaveFlags, "R4 空盤無工單，HaveFlag=1 即派");
            Assert.IsTrue(r4.SensorGuard);
        }

        [TestMethod]
        public void DispatchRoutes_R2R3Configured_AndAllAreasDefined()
        {
            var config = LoadScpAppSettings();
            var allBlocks = FloorAreas(config).SelectMany(f => f.Value).ToHashSet();
            var rules = new SCP.Helpers.AreaRuleProvider(config);

            CollectionAssert.AreEqual(new[] { "Z" }, rules.GetDispatchTargets("E").ToArray(), "R2 迅得2/3/4 → 中光電C");
            CollectionAssert.AreEqual(new[] { "P" }, rules.GetDispatchTargets("B").ToArray(), "R3 迅得1 → 中光電B");
            foreach (var route in rules.DispatchRoutes)
            {
                Assert.IsTrue(allBlocks.Contains(route.Key), $"DispatchRoutes 起點 {route.Key} 未列在 FloorSettings");
                foreach (var target in route.Value)
                {
                    Assert.IsTrue(allBlocks.Contains(target), $"DispatchRoutes 目的 {target} 未列在 FloorSettings");
                }
            }
        }

        [TestMethod]
        public void AreaNames_AbBuildingBlocks_AllHaveDisplayName()
        {
            var names = LoadScpAppSettings().GetSection("AreaRules:AreaNames").Get<Dictionary<string, string>>()!;

            foreach (var block in AbBuildingBlocks)
            {
                Assert.IsTrue(names.ContainsKey(block) && !string.IsNullOrWhiteSpace(names[block]), $"區域 {block} 缺少 AreaNames");
            }
        }
    }
}
