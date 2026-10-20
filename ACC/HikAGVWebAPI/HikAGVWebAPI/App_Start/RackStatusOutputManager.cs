using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>一次讀多站 HaveFlag（查不到的站不在回傳中；讀取失敗丟例外）。正式實作 SqlHaveFlagReader。</summary>
    public interface IHaveFlagReader
    {
        IDictionary<string, string> GetHaveFlags(IList<string> stationNos);
    }

    /// <summary>正式實作：SQLData.Select_oPortHaveFlag 一次查詢（每輪一次，不逐站查）。</summary>
    public class SqlHaveFlagReader : IHaveFlagReader
    {
        private readonly SQLData _db;

        public SqlHaveFlagReader(SQLData db)
        {
            _db = db;
        }

        public IDictionary<string, string> GetHaveFlags(IList<string> stationNos) => _db.Select_oPortHaveFlag(stationNos);
    }

    /// <summary>三色燈顏色；Unknown＝狀態無法判定，本輪不寫。</summary>
    public enum LampColor
    {
        Unknown,
        Green,
        Yellow,
        Red,
    }

    /// <summary>一站三色燈的 PLC 位址。</summary>
    public class LampMapping
    {
        public string StationNo { get; }
        public string Green { get; }
        public string Yellow { get; }
        public string Red { get; }

        public LampMapping(string stationNo, string green, string yellow, string red)
        {
            StationNo = stationNo;
            Green = green;
            Yellow = yellow;
            Red = red;
        }

        public string AddressOf(LampColor color)
        {
            switch (color)
            {
                case LampColor.Green: return Green;
                case LampColor.Yellow: return Yellow;
                case LampColor.Red: return Red;
                default: return null;
            }
        }
    }

    /// <summary>三色燈點位載入結果；Retry=true（例：DB 連不上）時 Mappings 為 null。</summary>
    public sealed class LampMappingLoad
    {
        public List<LampMapping> Mappings { get; }
        public string Source { get; }
        public bool Retry { get; }
        public IReadOnlyList<string> Warnings { get; }

        public LampMappingLoad(List<LampMapping> mappings, string source, bool retry, IReadOnlyList<string> warnings = null)
        {
            Mappings = mappings;
            Source = source;
            Retry = retry;
            Warnings = warnings ?? new string[0];
        }
    }

    /// <summary>
    /// 貨架狀態輸出（三色燈，T18）：定時讀 oPort.HaveFlag，依 T16 暫定規格寫 PLC（每站綠/黃/紅 3 個 M bit，同時只亮一個）。
    ///   HaveFlag 0 → 綠（可放貨）、1/2 → 黃（有貨架無工單）、3 → 紅（有料），任務指派中仍依貨架現況。
    /// 顏色改變才寫；寫入失敗不記錄狀態，下輪重寫（自癒）；PLC 斷線時中止整輪；
    /// 每 FullRefreshRounds 輪全量重寫一次（PLC 重開機或 bit 被他人改掉時燈號能追上，code review C1）。
    /// 點位來自 oPortPlc（LampUseFlag=Y），於掃描執行緒載入；跑在專屬 STA 執行緒（ActUtlType 為 STA COM）。
    /// </summary>
    public class RackStatusOutputManager
    {
        private IList<LampMapping> _mappings;
        private readonly Func<LampMappingLoad> _mappingLoader;
        private readonly Func<IBitWriter> _writerFactory;
        private readonly IHaveFlagReader _flagReader;
        private readonly Log _log;
        private readonly int _pollIntervalMs;

        // 以下只在掃描執行緒存取
        /// <summary>StationNo → 上次成功寫入的顏色。</summary>
        private readonly Dictionary<string, LampColor> _written = new Dictionary<string, LampColor>(StringComparer.OrdinalIgnoreCase);
        /// <summary>StationNo → 連續寫入失敗次數（Log 節流用）。</summary>
        private readonly Dictionary<string, int> _writeFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private int _loadFailures;
        private int _readFailures;
        private int _offlineRounds;
        private int _round;

        private IBitWriter _writer;
        private Thread _thread;
        private volatile bool _stop;

        /// <summary>點位讀不到（例：DB 尚未就緒）時的重試間隔。</summary>
        public int MappingRetryMs { get; set; } = 10000;

        /// <summary>每幾輪全量重寫一次（0＝不做）；預設 30 輪（PollIntervalMs=2000 時約 1 分鐘，11 站約 33 次寫入）。</summary>
        public int FullRefreshRounds { get; set; } = 30;

        /// <summary>生產用：點位與 PLC 連線都在掃描執行緒建立。</summary>
        public RackStatusOutputManager(Func<LampMappingLoad> mappingLoader, Func<IBitWriter> writerFactory,
            IHaveFlagReader flagReader, Log log, int pollIntervalMs = 2000)
        {
            _mappingLoader = mappingLoader;
            _writerFactory = writerFactory;
            _flagReader = flagReader;
            _log = log;
            _pollIntervalMs = pollIntervalMs <= 0 ? 2000 : pollIntervalMs;
            _mappings = new List<LampMapping>();
        }

        /// <summary>測試 / 直接用：已有點位與 writer，可不經 Start() 直接呼叫 ScanOnce()。</summary>
        public RackStatusOutputManager(IList<LampMapping> mappings, IBitWriter writer, IHaveFlagReader flagReader, Log log, int pollIntervalMs = 2000)
            : this((Func<LampMappingLoad>)null, () => writer, flagReader, log, pollIntervalMs)
        {
            _mappings = mappings ?? new List<LampMapping>();
            _writer = writer;
        }

        /// <summary>輸出執行緒是否仍在執行（0 站或建立 PLC 連線失敗時會自行結束）。</summary>
        public bool IsRunning => _thread?.IsAlive == true;

        public void Start()
        {
            if (_thread != null && _thread.IsAlive)
            {
                return;
            }

            _stop = false;
            _thread = new Thread(Execute) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            if (_thread == null || _thread.Join(3000))
            {
                _thread = null;
                return;
            }
            _log?.TraceOut("[RackLamp] 輸出執行緒 3 秒內未結束（可能正在查詢 DB 或連線 PLC），將自行結束", Log.LogType.WARN);
        }

        private void Execute()
        {
            while (!_stop && !TryLoadMappings())
            {
                SleepUnlessStopped(MappingRetryMs);
            }

            if (_stop)
            {
                return;
            }

            if (_mappings.Count == 0)
            {
                _log?.TraceOut("[RackLamp] 沒有啟用的三色燈點位（oPortPlc.LampUseFlag），停止輸出（不連 PLC）", Log.LogType.WARN);
                return;
            }

            try
            {
                _writer = _writerFactory();   // 於 STA 執行緒建立 PlcDevice（COM）
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[RackLamp] 建立 PLC 連線失敗：{ex.Message}", Log.LogType.ERROR);
                return;
            }

            while (!_stop)
            {
                try
                {
                    ScanOnce();
                }
                catch (Exception ex)
                {
                    _log?.TraceOut($"[RackLamp] 輸出例外：{ex.Message}", Log.LogType.WARN);
                }

                SleepUnlessStopped(_pollIntervalMs);
            }

            (_writer as IDisposable)?.Dispose();
        }

        private bool TryLoadMappings()
        {
            if (_mappingLoader == null)
            {
                return true;
            }

            LampMappingLoad load;
            try
            {
                load = _mappingLoader();
            }
            catch (Exception ex)
            {
                LogThrottled(ref _loadFailures, $"載入三色燈點位失敗：{ex.Message}，{MappingRetryMs / 1000} 秒後重試");
                return false;
            }

            if (load == null || load.Retry || load.Mappings == null)
            {
                LogThrottled(ref _loadFailures, $"暫時無法載入三色燈點位（{load?.Source}），{MappingRetryMs / 1000} 秒後重試");
                return false;
            }

            if (_loadFailures > 0)
            {
                _log?.TraceOut($"[RackLamp] 三色燈點位已恢復載入（先前失敗 {_loadFailures} 次）", Log.LogType.WARN);
                _loadFailures = 0;
            }

            foreach (var warning in load.Warnings)
            {
                _log?.TraceOut($"[RackLamp] {warning}", Log.LogType.WARN);
            }

            _mappings = load.Mappings;
            _log?.TraceOut($"[RackLamp] 三色燈點位來源：{load.Source}，共 {load.Mappings.Count} 站：{string.Join(",", load.Mappings.Select(m => m.StationNo))}", Log.LogType.TRACE);
            return true;
        }

        /// <summary>連續失敗計數 +1，第 1 次與之後每 30 次記一筆 WARN。</summary>
        private void LogThrottled(ref int counter, string message)
        {
            counter++;
            if (counter == 1 || counter % 30 == 0)
            {
                _log?.TraceOut($"[RackLamp] {message}（連續第 {counter} 次）", Log.LogType.WARN);
            }
        }

        private void SleepUnlessStopped(int totalMs)
        {
            for (int waited = 0; waited < totalMs && !_stop; waited += 50)
            {
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// 一輪輸出：一次讀所有站 HaveFlag → 顏色有變才寫；單站寫入失敗不影響其他站，PLC 斷線則中止本輪。可由測試直接呼叫。
        /// </summary>
        public void ScanOnce()
        {
            if (_writer == null || _mappings.Count == 0)
            {
                return;
            }

            _round++;
            if (FullRefreshRounds > 0 && _round % FullRefreshRounds == 0)
            {
                _written.Clear();   // 定期全量重寫：PLC 重開機清掉 M bit 時燈號能追上
            }

            IDictionary<string, string> flags;
            try
            {
                flags = _flagReader?.GetHaveFlags(_mappings.Select(m => m.StationNo).ToList()) ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                LogThrottled(ref _readFailures, $"讀取 oPort HaveFlag 失敗，本輪不更新燈號（維持現況）：{ex.Message}");
                return;
            }

            if (_readFailures > 0)
            {
                _log?.TraceOut($"[RackLamp] oPort 讀取已恢復（先前失敗 {_readFailures} 次）", Log.LogType.WARN);
                _readFailures = 0;
            }

            foreach (var lamp in _mappings)
            {
                flags.TryGetValue(lamp.StationNo, out string haveFlag);
                var color = ColorFor(haveFlag);
                if (color == LampColor.Unknown)
                {
                    continue;   // 查無該站或 HaveFlag 非預期值：維持現有燈號，下輪再判斷
                }

                if (_written.TryGetValue(lamp.StationNo, out var last) && last == color)
                {
                    continue;
                }

                if (WriteLamp(lamp, color))
                {
                    _written[lamp.StationNo] = color;
                    _writeFailures.Remove(lamp.StationNo);
                    _log?.TraceOut($"[RackLamp] {lamp.StationNo} 燈號 → {color}（HaveFlag={haveFlag}）", Log.LogType.TRACE);
                    continue;
                }

                _written.Remove(lamp.StationNo);   // 狀態不確定，下輪重寫
                int failures = (_writeFailures.TryGetValue(lamp.StationNo, out int f) ? f : 0) + 1;
                _writeFailures[lamp.StationNo] = failures;
                if (failures == 1 || failures % 30 == 0)
                {
                    _log?.TraceOut($"[RackLamp] {lamp.StationNo} 燈號寫入 {color} 失敗（連續第 {failures} 次），下輪重寫", Log.LogType.WARN);
                }

                if (_writer is IPlcConnectionState state && state.LastOpenFailed)
                {
                    // PLC 斷線：其餘站不再逐一嘗試（每點都會等一次 Open 逾時），下輪再試
                    LogThrottled(ref _offlineRounds, "PLC 未連線，本輪中止三色燈輸出");
                    return;
                }
            }

            _offlineRounds = 0;
        }

        /// <summary>先關另外兩燈、再開目標燈（避免瞬間兩燈同亮）；任一 bit 失敗即回 false。</summary>
        private bool WriteLamp(LampMapping lamp, LampColor color)
        {
            try
            {
                foreach (var other in new[] { LampColor.Green, LampColor.Yellow, LampColor.Red })
                {
                    if (other != color && !_writer.TryWriteBit(lamp.AddressOf(other), false))
                    {
                        return false;
                    }
                }
                return _writer.TryWriteBit(lamp.AddressOf(color), true);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[RackLamp] {lamp.StationNo} 寫入例外：{ex.Message}", Log.LogType.WARN);
                return false;
            }
        }

        /// <summary>T16 暫定燈號規則：HaveFlag 0 → 綠、1/2 → 黃、3 → 紅；其他值或查無資料 → Unknown（不寫）。</summary>
        public static LampColor ColorFor(string haveFlag)
        {
            switch (haveFlag?.Trim())
            {
                case "0": return LampColor.Green;
                case "1":
                case "2": return LampColor.Yellow;
                case "3": return LampColor.Red;
                default: return LampColor.Unknown;
            }
        }

        /// <summary>燈號只允許 bit 裝置：M／L（十進位）、B（十六進位）。D、W 等 word 裝置寫入會默默覆寫資料暫存器（code review W2）。</summary>
        private static readonly Regex LampAddressPattern = new Regex(@"^(?:[ML]\d+|B[0-9A-F]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


        /// <summary>
        /// oPortPlc 轉三色燈點位：只取 LampUseFlag=Y、三個位址齊全且為 bit 裝置的列；
        /// 位址與任何 sensor 位址（不論是否啟用）或其他燈號重複時略過該站並回傳說明
        /// （寫到 sensor bit 會讓 StockSensorLink 誤判清空庫位）。
        /// </summary>
        public static List<LampMapping> BuildLampMappings(IEnumerable<oPortPlcModel> rows, out List<string> skipped)
        {
            var result = new List<LampMapping>();
            skipped = new List<string>();
            if (rows == null)
            {
                return result;
            }

            var rowList = rows.Where(r => r != null).ToList();
            var usedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rowList)
            {
                var sensor = PlcAddress.Normalize(row.SensorAddress);
                if (!string.IsNullOrEmpty(sensor) && !usedBy.ContainsKey(sensor))
                {
                    usedBy[sensor] = $"{row.StationNo?.Trim()} sensor";
                }
            }

            foreach (var row in rowList)
            {
                if (!string.Equals(row.LampUseFlag?.Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var station = row.StationNo?.Trim();
                if (string.IsNullOrEmpty(station))
                {
                    continue;
                }

                var addresses = new[] { row.LampGreen?.Trim(), row.LampYellow?.Trim(), row.LampRed?.Trim() };
                if (addresses.Any(string.IsNullOrEmpty))
                {
                    skipped.Add($"oPortPlc {station} 三色燈位址不齊（綠/黃/紅需皆有值），略過");
                    continue;
                }

                var invalid = addresses.FirstOrDefault(a => !LampAddressPattern.IsMatch(a));
                if (invalid != null)
                {
                    skipped.Add($"oPortPlc {station} 三色燈位址 {invalid} 不是 bit 裝置（只允許 M/L/B），略過");
                    continue;
                }

                var keys = addresses.Select(PlcAddress.Normalize).ToArray();
                var dupInRow = keys.GroupBy(k => k).FirstOrDefault(g => g.Count() > 1)?.Key;
                var conflict = dupInRow ?? keys.FirstOrDefault(k => usedBy.ContainsKey(k));
                if (conflict != null)
                {
                    string owner = dupInRow != null ? $"{station} 本身" : usedBy[conflict];
                    skipped.Add($"oPortPlc 三色燈位址 {conflict} 重複：{owner} 已使用，略過 {station}，請修正 DB");
                    continue;
                }

                foreach (var k in keys)
                {
                    usedBy[k] = $"{station} 燈號";
                }
                result.Add(new LampMapping(station, addresses[0], addresses[1], addresses[2]));
            }

            return result;
        }
    }

    /// <summary>
    /// 三色燈輸出的組裝點：讀 FHtSetting.config 的 SectionLampOutput（預設 Enable=false），
    /// 點位讀 DB oPortPlc（LampUseFlag=Y）；Enable 非 true 或 Section 缺失時回傳 null。
    /// </summary>
    public static class RackStatusOutputBootstrap
    {
        /// <summary>
        /// 三色燈點位：資料表不存在 → 0 站（不輸出，不重試）；其他讀取失敗 → Retry；否則依 LampUseFlag 篩選。
        /// 三色燈沒有設定檔備援（不像 sensor 有 SensorMap）。
        /// </summary>
        public static LampMappingLoad ResolveLampMappings(Func<List<oPortPlcModel>> loadDb, Func<Exception, bool> isTableMissing = null)
        {
            List<oPortPlcModel> rows;
            try
            {
                rows = loadDb?.Invoke();
            }
            catch (Exception ex)
            {
                if ((isTableMissing ?? StockSensorLinkBootstrap.IsTableMissing)(ex))
                {
                    return new LampMappingLoad(new List<LampMapping>(), "oPortPlc 資料表不存在，請執行 Deploy_oPortPlc_ABBuilding1F.sql", false);
                }
                return new LampMappingLoad(null, $"讀取 oPortPlc 失敗：{ex.Message}", retry: true);
            }

            var mappings = RackStatusOutputManager.BuildLampMappings(rows, out var skipped);
            return new LampMappingLoad(mappings, $"DB oPortPlc（{rows?.Count ?? 0} 列，啟用 {mappings.Count} 站）", false, skipped);
        }

        public static RackStatusOutputManager Create(string configFilePath)
        {
            Configuration config;
            try
            {
                var map = new ExeConfigurationFileMap { ExeConfigFilename = configFilePath };
                config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[RackLamp] 設定載入失敗：{ex.Message}");
                return null;
            }

            var settings = (config.GetSection(nameof(SectionLampOutput)) as SectionLampOutput)?.LampOutputSettings;
            if (settings == null || !string.Equals(settings.Enable, "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;   // 未設定或關閉 → 不輸出三色燈
            }

            var dbSettings = (config.GetSection(nameof(SectionDB)) as SectionDB)?.DBSettings;
            var logSettings = (config.GetSection(nameof(SectionLog)) as SectionLog)?.LogSettings;

            Log log = logSettings != null ? new Log(logSettings.LogPath, "RackLamp") : new Log("RackLamp");
            if (logSettings != null)
            {
                log.KeepDate = logSettings.KeepDate;
            }

            var db = new SQLData(dbSettings?.DBName, dbSettings?.DBIP);
            int station = settings.StationNumber;
            Func<IBitWriter> writerFactory = () => new PlcDevice(station, log);
            Func<LampMappingLoad> loader = () => ResolveLampMappings(db.Select_oPortPlc);

            return new RackStatusOutputManager(loader, writerFactory, new SqlHaveFlagReader(db), log, settings.PollIntervalMs);
        }
    }
}
