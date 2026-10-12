namespace SCP.Services
{
    /// <summary>
    /// 自動派送路線規則（appsettings AutoDispatch:Routes[]）。
    /// 來源區有物料、目的區有空位時，引擎依規則建立 oNeed。
    /// </summary>
    public class AutoDispatchRoute
    {
        /// <summary>路線名稱（Log 用）。</summary>
        public string Name { get; set; } = "";

        /// <summary>來源區 Block 清單。</summary>
        public List<string> SourceBlocks { get; set; } = new List<string>();

        /// <summary>目的區 Block 清單。</summary>
        public List<string> TargetBlocks { get; set; } = new List<string>();

        /// <summary>
        /// 路線6 M→K（重構前寫死的唯一路線）。設定檔沒有 Routes 區段時沿用此路線，維持重構前行為。
        /// </summary>
        public static AutoDispatchRoute LegacyMToK() => new AutoDispatchRoute
        {
            Name = "路線6 M→K",
            SourceBlocks = new List<string> { "M" },
            TargetBlocks = new List<string> { "K" },
        };
    }
}
