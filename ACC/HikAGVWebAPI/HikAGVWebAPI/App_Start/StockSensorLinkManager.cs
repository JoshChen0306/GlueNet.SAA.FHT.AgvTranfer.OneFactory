using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>PLC bit 讀取抽象，解耦 MX Component COM，方便單元測試。</summary>
    public interface IBitReader
    {
        /// <summary>讀取單一 bit。回傳 false 代表讀取失敗（呼叫端不應清空，並中斷去抖計數）。</summary>
        bool TryReadBit(string address, out bool on);
    }

    /// <summary>讀取庫位 oPort 狀態（RackId / WorkOrder / HaveFlag）的抽象，正式實作走 SQLData.Select_oPort。</summary>
    public interface IPortStateReader
    {
        oPortModel GetPort(string stationNo);
    }

    /// <summary>清空庫位工單與狀態的抽象，正式實作呼叫 SQLData.Update_oPortEmpty。</summary>
    public interface IStockClearer
    {
        void ClearStock(string stationNo);
    }

    /// <summary>正式清空實作：呼叫既有 SQLData.Update_oPortEmpty（清 RackId / WorkOrder、HaveFlag 設回 0）。</summary>
    public class SqlStockClearer : IStockClearer
    {
        private readonly SQLData _db;
        private readonly Log _log;

        public SqlStockClearer(SQLData db, Log log = null)
        {
            _db = db;
            _log = log;
        }

        public void ClearStock(string stationNo)
        {
            try
            {
                _db.Update_oPortEmpty(stationNo);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlStockClearer] 清空庫位 {stationNo} 失敗：{ex.Message}", Log.LogType.ERROR);
            }
        }
    }

    /// <summary>正式 oPort 狀態讀取：走既有 SQLData.Select_oPort，回該庫位第一筆；查不到或失敗回 null。</summary>
    public class SqlPortStateReader : IPortStateReader
    {
        private readonly SQLData _db;
        private readonly Log _log;

        public SqlPortStateReader(SQLData db, Log log = null)
        {
            _db = db;
            _log = log;
        }

        public oPortModel GetPort(string stationNo)
        {
            try
            {
                var list = _db.Select_oPort(stationNo);
                return list != null && list.Count > 0 ? list[0] : null;
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlPortStateReader] 讀取庫位 {stationNo} 失敗：{ex.Message}", Log.LogType.WARN);
                return null;
            }
        }
    }

    /// <summary>一筆「PLC 位址 ↔ 庫位 StationNo」對應。</summary>
    public class SensorMapping
    {
        public string Address { get; }
        public string StationNo { get; }

        public SensorMapping(string address, string stationNo)
        {
            Address = address;
            StationNo = stationNo;
        }
    }

    /// <summary>
    /// 庫位 SENSOR 監控：定時掃描 PLC M 暫存器（平板偵測，ON=平板在 / OFF=平板不在）。
    /// 當 bit=OFF 且軟體仍認為庫位佔用（RackId / WorkOrder / HaveFlag 任一），連續達 ConfirmCount 輪後清空該庫位。
    /// 採 level + 去抖：避開 AGV 送料落位瞬間的短暫 OFF 誤清，並能於 ACC 重啟後補清殘留。
    /// 掃描跑在自有的專屬 STA 背景執行緒上（ActUtlType 為 STA COM），全程同一條 thread，零跨執行緒 marshaling。
    /// </summary>
    public class StockSensorLinkManager
    {
        private readonly IList<SensorMapping> _mappings;
        private readonly Func<IBitReader> _readerFactory;
        private readonly IPortStateReader _portReader;
        private readonly IStockClearer _clearer;
        private readonly Log _log;
        private readonly int _pollIntervalMs;
        private readonly int _confirmCount;
        private readonly Dictionary<string, int> _counter;   // address → 連續(OFF 且佔用)輪數

        private IBitReader _reader;
        private Thread _thread;
        private volatile bool _stop;

        /// <summary>生產用：reader 由 factory 在 STA 執行緒上建立（PlcDevice 之 COM 須與讀取同一條 thread）。</summary>
        public StockSensorLinkManager(IList<SensorMapping> mappings, Func<IBitReader> readerFactory,
            IStockClearer clearer, IPortStateReader portReader, Log log,
            int pollIntervalMs = 1000, int confirmCount = 3)
        {
            _mappings = mappings ?? new List<SensorMapping>();
            _readerFactory = readerFactory;
            _clearer = clearer;
            _portReader = portReader;
            _log = log;
            _pollIntervalMs = pollIntervalMs <= 0 ? 1000 : pollIntervalMs;
            _confirmCount = confirmCount <= 0 ? 1 : confirmCount;   // 至少確認 1 輪
            _counter = new Dictionary<string, int>();
            foreach (var m in _mappings)
            {
                _counter[m.Address] = 0;
            }
        }

        /// <summary>測試 / 直接用：已有 reader 實例，可不經 Start() 直接呼叫 ScanOnce()。</summary>
        public StockSensorLinkManager(IList<SensorMapping> mappings, IBitReader reader,
            IStockClearer clearer, IPortStateReader portReader, Log log,
            int pollIntervalMs = 1000, int confirmCount = 3)
            : this(mappings, () => reader, clearer, portReader, log, pollIntervalMs, confirmCount)
        {
            _reader = reader;
        }

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _stop = false;
            _thread = new Thread(Execute) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);   // ActUtlType 為 STA COM
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            _thread?.Join(3000);
            _thread = null;
        }

        private void Execute()
        {
            try
            {
                _reader = _readerFactory();   // 於 STA 執行緒建立 PlcDevice（COM）
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[StockSensorLink] 建立 PLC 連線失敗：{ex.Message}", Log.LogType.ERROR);
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
                    _log?.TraceOut($"[StockSensorLink] 掃描例外：{ex.Message}", Log.LogType.WARN);
                }

                Thread.Sleep(_pollIntervalMs);
            }

            (_reader as IDisposable)?.Dispose();
        }

        /// <summary>
        /// 一輪掃描：逐筆讀 bit →
        ///   讀取失敗 / bit=ON / 軟體未佔用 → 計數歸 0；
        ///   bit=OFF 且軟體仍佔用 → 計數 +1，達 ConfirmCount 即清空並歸 0。
        /// 可由測試直接呼叫。
        /// </summary>
        public void ScanOnce()
        {
            if (_reader == null)
            {
                return;
            }

            foreach (var map in _mappings)
            {
                // ① 讀 PLC 失敗 → 中斷去抖計數，不動作
                if (!_reader.TryReadBit(map.Address, out bool on))
                {
                    _counter[map.Address] = 0;
                    _log?.TraceOut($"[StockSensorLink] 讀取 {map.Address} 失敗，略過該輪", Log.LogType.WARN);
                    continue;
                }

                // ② 平板在位（bit=ON）→ 計數歸 0
                if (on)
                {
                    _counter[map.Address] = 0;
                    continue;
                }

                // ③ bit=OFF：讀庫位狀態，判斷軟體是否仍佔用
                var port = _portReader?.GetPort(map.StationNo);
                if (!IsOccupied(port))
                {
                    _counter[map.Address] = 0;   // 軟體本來就空，沒東西可清
                    continue;
                }

                // ④ bit=OFF 且軟體仍佔用 → 累積去抖計數
                int c = (_counter.TryGetValue(map.Address, out int cv) ? cv : 0) + 1;
                if (c >= _confirmCount)
                {
                    _clearer?.ClearStock(map.StationNo);
                    _log?.TraceOut($"[StockSensorLink] {map.Address} 連續 {_confirmCount} 輪 OFF 且佔用，清空庫位 {map.StationNo}", Log.LogType.TRACE);
                    _counter[map.Address] = 0;
                }
                else
                {
                    _counter[map.Address] = c;
                }
            }
        }

        /// <summary>軟體端是否仍認為該庫位「有平板/料」。任一指標成立即視為佔用。</summary>
        public static bool IsOccupied(oPortModel port)
        {
            if (port == null)
            {
                return false;   // 查不到該庫位 → 視為未佔用，不清
            }

            bool hasRack = !string.IsNullOrEmpty(port.RackId);                 // 平板（貨架）仍綁定
            bool hasWorkOrder = !string.IsNullOrEmpty(port.WorkOrder);         // 工單仍掛著
            bool haveMaterial = port.HaveFlag == "1" || port.HaveFlag == "3";  // 有料狀態（1=有料無工單, 3=有料有工單）

            return hasRack || hasWorkOrder || haveMaterial;
        }

        /// <summary>解析 "M10:B1,M11:B2" 為對應清單。格式錯誤的片段略過。</summary>
        public static List<SensorMapping> ParseSensorMap(string raw)
        {
            var result = new List<SensorMapping>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var part in raw.Split(','))
            {
                var token = part.Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                var kv = token.Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }

                var addr = kv[0].Trim();
                var station = kv[1].Trim();
                if (addr.Length == 0 || station.Length == 0)
                {
                    continue;
                }

                result.Add(new SensorMapping(addr, station));
            }

            return result;
        }
    }

    /// <summary>
    /// 庫位 SENSOR 監控的組裝點（composition root）：
    /// 讀 FHtSetting.config 的 SectionSensorClear / SectionDB / SectionLog，
    /// 組出 Log + SQLData + PlcDevice factory + SqlPortStateReader + SqlStockClearer，回傳設定好的 manager。
    /// Enable!=true、Section 缺失或無有效對應時回傳 null（不啟動監控）。
    /// </summary>
    public static class StockSensorLinkBootstrap
    {
        public static StockSensorLinkManager Create(string configFilePath)
        {
            Configuration config;
            try
            {
                var map = new ExeConfigurationFileMap { ExeConfigFilename = configFilePath };
                config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[StockSensorLink] 設定載入失敗：{ex.Message}");
                return null;
            }

            var settings = (config.GetSection(nameof(SectionSensorClear)) as SectionSensorClear)?.SensorClearSettings;
            if (settings == null)
            {
                return null;   // 未設定 → 不啟動
            }

            if (!string.Equals(settings.Enable, "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;   // 明確關閉 → 不啟動
            }

            var mappings = StockSensorLinkManager.ParseSensorMap(settings.SensorMap);
            if (mappings.Count == 0)
            {
                System.Diagnostics.Trace.WriteLine("[StockSensorLink] SensorMap 無有效對應，停用監控");
                return null;
            }

            var dbSettings = (config.GetSection(nameof(SectionDB)) as SectionDB)?.DBSettings;
            var logSettings = (config.GetSection(nameof(SectionLog)) as SectionLog)?.LogSettings;

            Log log = logSettings != null ? new Log(logSettings.LogPath, "StockSensorLink") : new Log("StockSensorLink");
            if (logSettings != null)
            {
                log.KeepDate = logSettings.KeepDate;
            }

            var db = new SQLData(dbSettings?.DBName, dbSettings?.DBIP);

            int station = settings.StationNumber;
            Func<IBitReader> readerFactory = () => new PlcDevice(station, log);
            IPortStateReader portReader = new SqlPortStateReader(db, log);
            IStockClearer clearer = new SqlStockClearer(db, log);

            return new StockSensorLinkManager(mappings, readerFactory, clearer, portReader, log,
                settings.PollIntervalMs, settings.ConfirmCount);
        }
    }
}
