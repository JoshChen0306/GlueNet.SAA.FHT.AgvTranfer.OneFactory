using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SCP.Helpers
{
    /// <summary>
    /// 讀取 appsettings 的 "AreaRules"，提供區域規則查詢：
    /// RegisterAreas（物料登記區）、RackIdRequiredAreas（貨架條碼必填區）、
    /// ReleaseRoutes（回送區 → 目的區清單，依序找空位）、AreaNames（訊息用顯示名稱，可省略）。
    /// 區域代號一律去空白並轉大寫後比對；區段缺漏視為空規則並記 Warning，不拋例外。
    /// </summary>
    public class AreaRuleProvider
    {
        private const string SectionName = "AreaRules";

        private readonly HashSet<string> _registerAreas;
        private readonly HashSet<string> _rackIdRequiredAreas;
        private readonly Dictionary<string, IReadOnlyList<string>> _releaseRoutes;
        private readonly Dictionary<string, string> _areaNames;

        public AreaRuleProvider(IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);
            if (!section.Exists())
            {
                SCP.LogMgt.Logger?.Warn($"[AreaRuleProvider] 找不到 {SectionName} 設定區段，視為空規則");
            }

            _registerAreas = ReadAreaList(section.GetSection("RegisterAreas")).ToHashSet();
            _rackIdRequiredAreas = ReadAreaList(section.GetSection("RackIdRequiredAreas")).ToHashSet();

            // 同一區域若因大小寫重複設定，以後出現者為準
            _releaseRoutes = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var route in section.GetSection("ReleaseRoutes").GetChildren())
            {
                var source = Normalize(route.Key);
                if (source.Length == 0) continue;
                _releaseRoutes[source] = ReadAreaList(route).ToList();
            }

            _areaNames = new Dictionary<string, string>();
            foreach (var name in section.GetSection("AreaNames").GetChildren())
            {
                var key = Normalize(name.Key);
                if (key.Length == 0 || string.IsNullOrWhiteSpace(name.Value)) continue;
                _areaNames[key] = name.Value.Trim();
            }

            ClickableAreas = _registerAreas.Union(_releaseRoutes.Keys).ToList();
        }

        /// <summary>地圖上可點選的區域 = 物料登記區 ∪ 回送區。</summary>
        public IReadOnlyList<string> ClickableAreas { get; }

        public bool IsRegisterArea(string area) => _registerAreas.Contains(Normalize(area));

        public bool IsReleaseArea(string area) => _releaseRoutes.ContainsKey(Normalize(area));

        public bool IsRackIdRequired(string area) => _rackIdRequiredAreas.Contains(Normalize(area));

        /// <summary>回送目的區清單，依設定順序；未設定則回傳空清單。</summary>
        public IReadOnlyList<string> GetReleaseTargets(string area)
            => _releaseRoutes.TryGetValue(Normalize(area), out var targets) ? targets : Array.Empty<string>();

        /// <summary>訊息用區域標籤：有設定 AreaNames 時為「L區（3F下料區）」，否則為「L區」。</summary>
        public string GetAreaLabel(string area)
        {
            var key = Normalize(area);
            return _areaNames.TryGetValue(key, out var name) ? $"{key}區（{name}）" : $"{key}區";
        }

        // 以 GetChildren 讀陣列，不依賴 configuration binder（比照 ChargingStationProvider）
        private static IEnumerable<string> ReadAreaList(IConfigurationSection section)
            => section.GetChildren()
                      .Select(c => Normalize(c.Value))
                      .Where(v => v.Length > 0)
                      .Distinct();

        private static string Normalize(string? area) => (area ?? "").Trim().ToUpperInvariant();
    }
}
