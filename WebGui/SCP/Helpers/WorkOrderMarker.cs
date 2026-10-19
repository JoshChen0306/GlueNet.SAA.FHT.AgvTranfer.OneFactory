namespace SCP.Helpers
{
    /// <summary>
    /// WorkOrder 狀態標記（比照二廠 2F V Cut 區 ^VCUT^DONE）。
    /// 加工站區域（AreaRules:ProcessDoneAreas）登記物料時附加 ^DONE，表示人員已加工完成、可自動派送；
    /// 寫入 oNeed 與畫面顯示時移除，避免標記帶到中光電 Mapping 的 itemCode 或顯示給操作人員。
    /// </summary>
    public static class WorkOrderMarker
    {
        /// <summary>加工完成標記。</summary>
        public const string ProcessDone = "^DONE";

        /// <summary>二廠舊字樣（V Cut 已加工），結尾雖是 ^DONE 但不是本標記，原樣保留（前置清理計畫 B6）。</summary>
        private const string LegacyVcutDone = "^VCUT^DONE";

        /// <summary>附加加工完成標記（已有則不重複附加）。</summary>
        public static string MarkProcessDone(string workOrder) => Strip(workOrder) + ProcessDone;

        /// <summary>移除結尾的加工完成標記；條碼中間的同字串與舊字樣不動；null 回傳空字串。</summary>
        public static string Strip(string? workOrder)
        {
            var value = workOrder ?? "";
            return HasProcessDone(value) ? value.Substring(0, value.Length - ProcessDone.Length) : value;
        }

        /// <summary>結尾是否帶有加工完成標記（不含舊字樣 ^VCUT^DONE）。</summary>
        public static bool HasProcessDone(string? workOrder)
        {
            var value = workOrder ?? "";
            return value.EndsWith(ProcessDone, StringComparison.Ordinal)
                && !value.EndsWith(LegacyVcutDone, StringComparison.Ordinal);
        }
    }
}
