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

        /// <summary>是否啟用（false 時整條路線跳過）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 來源站視為「有貨可派」的 HaveFlag 清單；未設定時只認 3（重構前行為）。
        /// R1/R4 設 ["1","3"]：中光電貨架 Sensor ON 時 ACC 寫 HaveFlag=1（2026-10-12 定案）。
        /// </summary>
        public List<string> SourceHaveFlags { get; set; } = new List<string>();

        /// <summary>
        /// sensor 防呆：HaveFlag 與登記資料不一致的站點不派車並記 Warning。
        /// 來源 HaveFlag=3 但沒有 WorkOrder、目的 HaveFlag=0 但仍有 RackId/WorkOrder 殘留。
        /// 預設關閉，M→K 行為不變。
        /// </summary>
        public bool SensorGuard { get; set; }

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
