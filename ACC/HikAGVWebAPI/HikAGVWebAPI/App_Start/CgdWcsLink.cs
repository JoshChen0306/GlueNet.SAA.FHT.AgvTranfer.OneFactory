using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>中光電儲位 API 回應。Reached=false 代表連線失敗或逾時（沒有 code）。</summary>
    public class CgdWcsResult
    {
        public bool Reached { get; private set; }
        public int? Code { get; private set; }
        public string Message { get; private set; }
        public string ItemCode { get; private set; }

        public static CgdWcsResult FromCode(int code, string message, string itemCode = null)
            => new CgdWcsResult { Reached = true, Code = code, Message = message, ItemCode = itemCode };

        public static CgdWcsResult Unreachable(string message)
            => new CgdWcsResult { Reached = false, Message = message };

        public override string ToString()
            => Reached ? $"code={Code}, message={Message}, itemCode={ItemCode}" : $"連線失敗：{Message}";
    }

    /// <summary>中光電儲位 API（規格：中光電儲位交握api.pdf）。在背景執行緒同步呼叫。</summary>
    public interface ICgdWcsClient
    {
        /// <summary>2-1 GET /api/v1/wcs/locations/{binCode}/status</summary>
        CgdWcsResult GetStatus(string binCode);

        /// <summary>2-2 POST /api/v1/wcs/locations/{binCode}/Mapping { itemCode }</summary>
        CgdWcsResult Mapping(string binCode, string itemCode);

        /// <summary>2-3 POST /api/v1/wcs/locations/{binCode}/release</summary>
        CgdWcsResult Release(string binCode);
    }

    /// <summary>R1 來源站補資料：HaveFlag=1 改為 3 並寫入 WorkOrder=itemCode，正式實作呼叫 SQLData.Update_oPortMaterial。</summary>
    public interface IMaterialFiller
    {
        /// <summary>回傳是否實際寫入（SQL 限定 HaveFlag='1'；期間被清空或例外回 false）。</summary>
        bool FillMaterial(string stationNo, string itemCode);
    }

    /// <summary>中光電儲位 API 的 HTTP 實作（無認證，2026-10-08 定案；逾時由 HttpClient.Timeout 控制）。</summary>
    public class CgdWcsHttpClient : ICgdWcsClient
    {
        private const string LocationsRoute = "api/v1/wcs/locations/";
        private readonly HttpClient _http;

        public CgdWcsHttpClient(string baseUrl, int timeoutMs)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/"),
                Timeout = TimeSpan.FromMilliseconds(timeoutMs <= 0 ? 5000 : timeoutMs)
            };
        }

        public CgdWcsResult GetStatus(string binCode)
            => Send(() => _http.GetAsync(LocationsRoute + Uri.EscapeDataString(binCode) + "/status"));

        public CgdWcsResult Mapping(string binCode, string itemCode)
        {
            string body = JsonConvert.SerializeObject(new { itemCode });
            return Send(() => _http.PostAsync(LocationsRoute + Uri.EscapeDataString(binCode) + "/Mapping",
                new StringContent(body, Encoding.UTF8, "application/json")));
        }

        public CgdWcsResult Release(string binCode)
            => Send(() => _http.PostAsync(LocationsRoute + Uri.EscapeDataString(binCode) + "/release",
                new StringContent("", Encoding.UTF8, "application/json")));

        private static CgdWcsResult Send(Func<System.Threading.Tasks.Task<HttpResponseMessage>> request)
        {
            try
            {
                using (var response = request().GetAwaiter().GetResult())
                {
                    string text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return Parse(text, (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                // 逾時（TaskCanceledException）與連線失敗都視為未送達，由呼叫端重試
                return CgdWcsResult.Unreachable(ex.GetBaseException().Message);
            }
        }

        /// <summary>解析 { code, message, data?: { itemCode } }；itemCode 亦接受放在最外層。</summary>
        public static CgdWcsResult Parse(string text, int httpStatus)
        {
            JObject json;
            try
            {
                json = JObject.Parse(text ?? "");
            }
            catch (JsonException ex)
            {
                return CgdWcsResult.Unreachable($"HTTP {httpStatus} 回應不是 JSON：{ex.Message}");
            }

            int? code = json.Value<int?>("code");
            if (code == null)
            {
                return CgdWcsResult.Unreachable($"HTTP {httpStatus} 回應缺少 code");
            }

            string itemCode = json.SelectToken("data.itemCode")?.ToString() ?? json.Value<string>("itemCode");
            return CgdWcsResult.FromCode(code.Value, json.Value<string>("message"), itemCode);
        }
    }

    /// <summary>
    /// 中光電儲位 API 介接（T25）：
    ///   ① R1 來源站補資料：StatusStations（D1）為 HaveFlag=1 時呼叫 status，code=0 且有 itemCode → 寫回 HaveFlag=3＋WorkOrder；失敗下一輪再查。
    ///   ② outbin／Sensor 清空 → release；③ end → Mapping(itemCode=WorkOrder)。
    /// ②③ 排入佇列由背景執行緒送出，不阻塞 RCS 回呼；回 2 或連線失敗依 RetryDelays（預設 5s／15s／60s）重試，全失敗記 Error。
    /// Mapping 回 1（已佔用）記 Error 不重試；release 回 1（已釋放）視為成功。
    /// </summary>
    public class CgdWcsLink
    {
        private enum CommandKind { Release, Mapping }

        private class Command
        {
            public CommandKind Kind;
            public string StationNo;
            public string BinCode;
            public string ItemCode;
            public int Attempts;
            public DateTime DueAt;

            public override string ToString()
                => Kind == CommandKind.Mapping ? $"Mapping {StationNo}({BinCode}) itemCode={ItemCode}" : $"release {StationNo}({BinCode})";
        }

        private readonly ICgdWcsClient _client;
        private readonly Dictionary<string, string> _binCodeMap;
        private readonly List<string> _statusStations;
        private readonly IPortStateReader _portReader;
        private readonly IMaterialFiller _filler;
        private readonly Log _log;
        private readonly int[] _retryDelaysSec;
        private readonly Func<DateTime> _clock;
        private readonly TimeSpan _warnInterval;
        private readonly Dictionary<string, DateTime> _lastWarn = new Dictionary<string, DateTime>();
        private readonly List<Command> _pending = new List<Command>();
        private readonly object _lock = new object();

        private Thread _thread;
        private volatile bool _stop;

        public CgdWcsLink(ICgdWcsClient client, IDictionary<string, string> binCodeMap, IEnumerable<string> statusStations,
            IPortStateReader portReader, IMaterialFiller filler, Log log,
            int[] retryDelaysSec = null, Func<DateTime> clock = null, int warnIntervalMinutes = 10)
        {
            _client = client;
            _binCodeMap = new Dictionary<string, string>(binCodeMap ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            _statusStations = (statusStations ?? Enumerable.Empty<string>()).ToList();
            _portReader = portReader;
            _filler = filler;
            _log = log;
            _retryDelaysSec = retryDelaysSec ?? new[] { 5, 15, 60 };
            _clock = clock ?? (() => DateTime.Now);
            _warnInterval = TimeSpan.FromMinutes(warnIntervalMinutes);
        }

        public int PendingCount
        {
            get { lock (_lock) { return _pending.Count; } }
        }

        /// <summary>中光電站點被取走（outbin）或 Sensor 清空：排入 release。非中光電站點回傳 false。</summary>
        public bool EnqueueRelease(string stationNo)
        {
            if (!TryGetBinCode(stationNo, out string binCode))
            {
                return false;
            }

            Enqueue(new Command { Kind = CommandKind.Release, StationNo = stationNo, BinCode = binCode });
            return true;
        }

        /// <summary>SCP 加工完成標記（SCP Helpers.WorkOrderMarker.ProcessDone）。</summary>
        private const string ProcessDoneMarker = "^DONE";

        /// <summary>放貨到中光電站點（end）：排入 Mapping(itemCode=WorkOrder)。非中光電站點或無工單回傳 false。</summary>
        public bool EnqueueMapping(string stationNo, string itemCode)
        {
            if (!TryGetBinCode(stationNo, out string binCode))
            {
                return false;
            }

            // 防呆：SCP 加工完成標記 ^DONE 只用於派送判斷（建 oNeed 時已移除），萬一殘留也不送給中光電
            itemCode = itemCode ?? "";
            if (itemCode.EndsWith(ProcessDoneMarker, StringComparison.Ordinal) && !itemCode.EndsWith("^VCUT^DONE", StringComparison.Ordinal))
            {
                itemCode = itemCode.Substring(0, itemCode.Length - ProcessDoneMarker.Length);
            }

            if (string.IsNullOrEmpty(itemCode))
            {
                _log?.TraceOut($"[CgdWcs] {stationNo} 放貨完成但沒有工單，無法 Mapping，請人員確認", Log.LogType.ERROR);
                return false;
            }

            Enqueue(new Command { Kind = CommandKind.Mapping, StationNo = stationNo, BinCode = binCode, ItemCode = itemCode });
            return true;
        }

        /// <summary>
        /// 送出到期的 release／Mapping。由背景執行緒呼叫，測試可直接呼叫。
        /// 同一 binCode 先進先出：只送每個 binCode 最前面的一筆，前一筆成功或放棄前後面的不送，
        /// 避免 release 重試期間 Mapping 先送出，造成中光電帳務順序顛倒。
        /// </summary>
        public void ProcessDue()
        {
            List<Command> due;
            DateTime now = _clock();
            lock (_lock)
            {
                due = _pending
                    .GroupBy(c => c.BinCode, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Where(c => c.DueAt <= now)
                    .ToList();
            }

            foreach (var cmd in due)
            {
                cmd.Attempts++;
                var result = cmd.Kind == CommandKind.Release
                    ? _client.Release(cmd.BinCode)
                    : _client.Mapping(cmd.BinCode, cmd.ItemCode);

                if (result.Reached && (result.Code == 0 || (result.Code == 1 && cmd.Kind == CommandKind.Release)))
                {
                    _log?.TraceOut($"[CgdWcs] {cmd} 成功（{result}）", Log.LogType.TRACE);
                    Remove(cmd);
                    continue;
                }

                if (result.Reached && result.Code == 1)
                {
                    _log?.TraceOut($"[CgdWcs] {cmd} 回已佔用，兩邊物料資料不一致，請人員確認（{result}）", Log.LogType.ERROR);
                    Remove(cmd);
                    continue;
                }

                if (cmd.Attempts > _retryDelaysSec.Length)
                {
                    _log?.TraceOut($"[CgdWcs] {cmd} 重試 {_retryDelaysSec.Length} 次仍失敗，放棄，請人員確認中光電儲位資料（{result}）", Log.LogType.ERROR);
                    Remove(cmd);
                    continue;
                }

                int delay = _retryDelaysSec[cmd.Attempts - 1];
                lock (_lock)
                {
                    cmd.DueAt = _clock().AddSeconds(delay);
                }
                _log?.TraceOut($"[CgdWcs] {cmd} 失敗，{delay} 秒後重試（{result}）", Log.LogType.WARN);
            }
        }

        private void Remove(Command cmd)
        {
            lock (_lock)
            {
                _pending.Remove(cmd);
            }
        }

        /// <summary>R1 來源站補資料一輪：HaveFlag=1 才查 status，補成 3 後不再查。</summary>
        public void FillStatusOnce()
        {
            foreach (var stationNo in _statusStations)
            {
                var port = _portReader?.GetPort(stationNo);
                if (port == null || port.HaveFlag != "1")
                {
                    continue;
                }

                if (!TryGetBinCode(stationNo, out string binCode))
                {
                    WarnThrottled(stationNo + ":nobin", $"[CgdWcs] {stationNo} 未設定 binCode，無法查詢物料");
                    continue;
                }

                var result = _client.GetStatus(binCode);
                if (result.Reached && result.Code == 0 && !string.IsNullOrEmpty(result.ItemCode))
                {
                    if (_filler != null && _filler.FillMaterial(stationNo, result.ItemCode))
                    {
                        _log?.TraceOut($"[CgdWcs] {stationNo} 取得物料 itemCode={result.ItemCode}，寫回 HaveFlag=3", Log.LogType.TRACE);
                    }
                    else
                    {
                        WarnThrottled(stationNo + ":fill", $"[CgdWcs] {stationNo} 取得物料 itemCode={result.ItemCode} 但寫回失敗（庫位已非 HaveFlag=1 或 DB 例外），下一輪再查");
                    }
                    continue;
                }

                WarnThrottled(stationNo + ":status", $"[CgdWcs] {stationNo} 查詢物料失敗，不派車，下一輪再查（{result}）");
            }
        }

        public void Start(int statusPollMs)
        {
            if (_thread != null)
            {
                return;
            }

            _stop = false;
            int pollMs = statusPollMs <= 0 ? 10000 : statusPollMs;
            _thread = new Thread(() => Execute(pollMs)) { IsBackground = true, Name = "CgdWcsLink" };
            _thread.Start();
        }

        /// <summary>停止背景執行緒；尚未送出／重試中的指令逐筆記 Error，供人員手動補送。</summary>
        public void Stop()
        {
            _stop = true;
            _thread?.Join(15000);   // 須大於單次 HTTP 逾時，讓進行中的呼叫有機會完成
            _thread = null;

            List<Command> left;
            lock (_lock)
            {
                left = _pending.ToList();
                _pending.Clear();
            }

            foreach (var cmd in left)
            {
                _log?.TraceOut($"[CgdWcs] 程式停止時尚未送出：{cmd}（已嘗試 {cmd.Attempts} 次），請人員確認中光電儲位資料", Log.LogType.ERROR);
            }
        }

        private void Execute(int statusPollMs)
        {
            DateTime nextStatus = DateTime.MinValue;
            while (!_stop)
            {
                try
                {
                    ProcessDue();
                    if (DateTime.Now >= nextStatus)
                    {
                        FillStatusOnce();
                        nextStatus = DateTime.Now.AddMilliseconds(statusPollMs);
                    }
                }
                catch (Exception ex)
                {
                    _log?.TraceOut($"[CgdWcs] 背景執行例外：{ex.Message}", Log.LogType.WARN);
                }

                Thread.Sleep(1000);
            }
        }

        private void Enqueue(Command cmd)
        {
            cmd.DueAt = _clock();
            lock (_lock)
            {
                _pending.Add(cmd);
            }
            _log?.TraceOut($"[CgdWcs] 排入 {cmd}", Log.LogType.TRACE);
        }

        private bool TryGetBinCode(string stationNo, out string binCode)
        {
            binCode = null;
            return !string.IsNullOrEmpty(stationNo)
                && _binCodeMap.TryGetValue(stationNo, out binCode)
                && !string.IsNullOrEmpty(binCode);
        }

        private void WarnThrottled(string key, string message)
        {
            DateTime now = _clock();
            if (_lastWarn.TryGetValue(key, out DateTime last) && now - last < _warnInterval)
            {
                return;
            }

            _lastWarn[key] = now;
            _log?.TraceOut(message, Log.LogType.WARN);
        }

        /// <summary>解析 "D1:BIN-D,O1:BIN-A"（我方站號:中光電 binCode）。格式錯誤片段略過。</summary>
        public static Dictionary<string, string> ParseBinCodeMap(string raw)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in StockSensorLinkManager.ParseSensorMap(raw))
            {
                result[mapping.Address] = mapping.StationNo;
            }
            return result;
        }

        /// <summary>解析 "5,15,60"（秒）。空值或無有效數字時用預設 5／15／60。</summary>
        public static int[] ParseRetryDelays(string raw)
        {
            var delays = (raw ?? "").Split(',')
                .Select(s => int.TryParse(s.Trim(), out int v) ? v : -1)
                .Where(v => v >= 0)
                .ToArray();
            return delays.Length > 0 ? delays : new[] { 5, 15, 60 };
        }
    }

    /// <summary>正式補資料實作：呼叫 SQLData.Update_oPortMaterial（SQL 限定 HaveFlag='1'）。</summary>
    public class SqlMaterialFiller : IMaterialFiller
    {
        private readonly SQLData _db;
        private readonly Log _log;

        public SqlMaterialFiller(SQLData db, Log log = null)
        {
            _db = db;
            _log = log;
        }

        public bool FillMaterial(string stationNo, string itemCode)
        {
            try
            {
                return _db.Update_oPortMaterial(stationNo, itemCode) > 0;
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlMaterialFiller] 寫回 {stationNo} 物料失敗：{ex.Message}", Log.LogType.WARN);
                return false;
            }
        }
    }

    /// <summary>
    /// 中光電介接組裝點：讀 FHtSetting.config 的 SectionCgdWcs。Enable!=true 或缺設定回傳 null（不啟動）。
    /// Current 供 RCS 回呼（outbin／end）與 StockSensorLink 取用；未啟動時為 null。
    /// </summary>
    public static class CgdWcsBootstrap
    {
        public static CgdWcsLink Current { get; private set; }

        /// <summary>啟動中光電介接。任何設定錯誤只停用介接並記錄，不拋出（避免拖垮 ACC 啟動）。</summary>
        public static CgdWcsLink Start(string configFilePath)
        {
            try
            {
                return StartCore(configFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CgdWcs] 啟動失敗，停用中光電介接：{ex}");
                try
                {
                    new Log("CgdWcs").TraceOut($"[CgdWcs] 啟動失敗，停用中光電介接：{ex.Message}", Log.LogType.ERROR);
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Trace.WriteLine($"[CgdWcs] 寫入啟動失敗 Log 也失敗：{logEx.Message}");
                }
                Current = null;
                return null;
            }
        }

        private static CgdWcsLink StartCore(string configFilePath)
        {
            Configuration config;
            try
            {
                var map = new ExeConfigurationFileMap { ExeConfigFilename = configFilePath };
                config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[CgdWcs] 設定載入失敗：{ex.Message}");
                return null;
            }

            var settings = (config.GetSection(nameof(SectionCgdWcs)) as SectionCgdWcs)?.CgdWcsSettings;
            if (settings == null || !string.Equals(settings.Enable, "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var dbSettings = (config.GetSection(nameof(SectionDB)) as SectionDB)?.DBSettings;
            var logSettings = (config.GetSection(nameof(SectionLog)) as SectionLog)?.LogSettings;

            Log log = logSettings != null ? new Log(logSettings.LogPath, "CgdWcs") : new Log("CgdWcs");
            if (logSettings != null)
            {
                log.KeepDate = logSettings.KeepDate;
            }

            var db = new SQLData(dbSettings?.DBName, dbSettings?.DBIP);
            var link = new CgdWcsLink(
                new CgdWcsHttpClient(settings.BaseUrl, settings.TimeoutMs),
                CgdWcsLink.ParseBinCodeMap(settings.BinCodeMap),
                StockSensorLinkManager.ParseStationList(settings.StatusStations),
                new SqlPortStateReader(db, log),
                new SqlMaterialFiller(db, log),
                log,
                CgdWcsLink.ParseRetryDelays(settings.RetryDelaysSec));

            link.Start(settings.StatusPollMs);
            log.TraceOut($"[CgdWcs] 啟動：BaseUrl={settings.BaseUrl}, BinCodeMap={settings.BinCodeMap}, StatusStations={settings.StatusStations}", Log.LogType.TRACE);
            Current = link;
            return link;
        }

        public static void Stop()
        {
            Current?.Stop();
            Current = null;
        }
    }
}
