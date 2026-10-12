namespace SCP.Services
{
    /// <summary>
    /// 自動派送 Warning 節流：同一個 key（路線＋站點＋原因）在間隔內只記一次。
    /// 防呆狀態會持續到有人處理為止，每輪（預設 30 秒）都記會灌爆 Log。
    /// 由 AutoDispatchService 持有（跨輪保存），引擎每輪重建不能自己保存。
    /// </summary>
    public class AutoDispatchWarningThrottle
    {
        private readonly TimeSpan _interval;
        private readonly Dictionary<string, DateTime> _lastLogged = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();

        public AutoDispatchWarningThrottle(TimeSpan interval)
        {
            _interval = interval;
        }

        /// <summary>距離上次記錄已超過間隔（或從未記錄）時回傳 true 並更新時間。</summary>
        public bool ShouldLog(string key, DateTime now)
        {
            lock (_lock)
            {
                if (_lastLogged.TryGetValue(key, out var last) && now - last < _interval)
                {
                    return false;
                }
                _lastLogged[key] = now;
                return true;
            }
        }
    }
}
