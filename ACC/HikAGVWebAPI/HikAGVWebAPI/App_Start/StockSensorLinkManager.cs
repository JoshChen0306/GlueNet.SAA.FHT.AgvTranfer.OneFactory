using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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

    /// <summary>中光電來源站 Sensor ON 時登記「有貨架、無工單」的抽象，正式實作呼叫 SQLData.Update_oPortRackPresent。</summary>
    public interface IStockRegistrar
    {
        void MarkRackPresent(string stationNo);
    }

    /// <summary>中光電來源站的任務查詢與清理抽象（Sensor ON 防誤寫、Sensor OFF 處理已建任務）。</summary>
    public interface ISourceTaskStore
    {
        /// <summary>該站是否為已派出任務（oRequire / oMission 進行中）的起點。</summary>
        bool HasActiveTask(string stationNo);

        /// <summary>該站是否為 AGV 已開始執行（oMission OkFlag='R'）任務的起點：正常取貨時 sensor 會先於 outbin 熄滅。</summary>
        bool HasRunningMission(string stationNo);

        /// <summary>刪除該站為起點、尚未派出（AssignFlag 空）的 oNeed，回傳刪除筆數。</summary>
        int DeleteUndispatchedNeeds(string stationNo);
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

    /// <summary>正式登記實作：只在 HaveFlag=0 時改為 1（條件寫在 SQL，避免與其他程序競爭時覆蓋有工單的狀態）。</summary>
    public class SqlStockRegistrar : IStockRegistrar
    {
        private readonly SQLData _db;
        private readonly Log _log;

        public SqlStockRegistrar(SQLData db, Log log = null)
        {
            _db = db;
            _log = log;
        }

        public void MarkRackPresent(string stationNo)
        {
            try
            {
                _db.Update_oPortRackPresent(stationNo);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlStockRegistrar] 登記庫位 {stationNo} 有貨架失敗：{ex.Message}", Log.LogType.ERROR);
            }
        }
    }

    /// <summary>正式任務查詢與清理：走 SQLData。查詢失敗視為有任務（寧可不寫 HaveFlag）。</summary>
    public class SqlSourceTaskStore : ISourceTaskStore
    {
        private readonly SQLData _db;
        private readonly Log _log;

        public SqlSourceTaskStore(SQLData db, Log log = null)
        {
            _db = db;
            _log = log;
        }

        public bool HasActiveTask(string stationNo)
        {
            try
            {
                return _db.Exists_ActiveTaskAsBegin(stationNo);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlSourceTaskStore] 查詢 {stationNo} 進行中任務失敗：{ex.Message}", Log.LogType.WARN);
                return true;
            }
        }

        public bool HasRunningMission(string stationNo)
        {
            try
            {
                return _db.Exists_RunningMissionAsBegin(stationNo);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlSourceTaskStore] 查詢 {stationNo} 執行中任務失敗：{ex.Message}", Log.LogType.WARN);
                return false;
            }
        }

        public int DeleteUndispatchedNeeds(string stationNo)
        {
            try
            {
                return _db.Delete_oNeedUndispatched(stationNo);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[SqlSourceTaskStore] 刪除 {stationNo} 未派出 oNeed 失敗：{ex.Message}", Log.LogType.ERROR);
                return 0;
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

    /// <summary>WatchArrival 結果：只有 Watching 會進入空搬偵測。</summary>
    public enum ArrivalWatchResult
    {
        /// <summary>目的站有 sensor 對應，已登記等待到料。</summary>
        Watching,
        /// <summary>目的站不在 SensorMap（無 sensor），無法偵測空搬。</summary>
        NoSensor,
        /// <summary>目的站號為空（資料異常），不判定空搬。</summary>
        InvalidStation,
        /// <summary>掃描執行緒未在執行（PLC 連線建立失敗或已停止），無法偵測空搬。</summary>
        NotMonitoring,
    }

    /// <summary>一筆空搬判定（任務完成但目的站 sensor 連續未亮）。</summary>
    public class EmptyTransferInfo
    {
        public string EndStation { get; }
        public string TaskCode { get; }
        public string BeginStation { get; }
        public DateTime EndTime { get; }

        public EmptyTransferInfo(string endStation, string taskCode, string beginStation, DateTime endTime)
        {
            EndStation = endStation;
            TaskCode = taskCode;
            BeginStation = beginStation;
            EndTime = endTime;
        }
    }

    /// <summary>sensor 點位載入結果（T29）。Retry=true 表示暫時讀不到（例：DB 連不上），稍後重試；此時 Mappings 為 null。</summary>
    public sealed class SensorMappingLoad
    {
        public List<SensorMapping> Mappings { get; }
        /// <summary>實際來源與原因，供 Log 使用。</summary>
        public string Source { get; }
        /// <summary>使用設定檔 SensorMap（oPortPlc 不存在或沒有任何資料列）。</summary>
        public bool FellBackToConfig { get; }
        public bool Retry { get; }
        /// <summary>載入時略過的資料（例：重複位址），呼叫端記 WARN。</summary>
        public IReadOnlyList<string> Warnings { get; }

        public SensorMappingLoad(List<SensorMapping> mappings, string source, bool fellBackToConfig, bool retry, IReadOnlyList<string> warnings = null)
        {
            Mappings = mappings;
            Source = source;
            FellBackToConfig = fellBackToConfig;
            Retry = retry;
            Warnings = warnings ?? new string[0];
        }
    }

    /// <summary>
    /// 庫位 SENSOR 監控：定時掃描 PLC M 暫存器（平板偵測，ON=平板在 / OFF=平板不在）。
    /// 當 bit=OFF 且軟體仍認為庫位佔用（RackId / WorkOrder / HaveFlag 任一），連續達 ConfirmCount 輪後清空該庫位。
    /// 採 level + 去抖：避開 AGV 送料落位瞬間的短暫 OFF 誤清，並能於 ACC 重啟後補清殘留。
    /// 中光電來源站（SensorOnStations，如 D1、O1）另外處理：
    ///   bit=ON 且 HaveFlag=0、無進行中任務，連續達 ConfirmCount 輪 → 寫 HaveFlag=1（有貨架、無工單），供 SCP R1/R4 觸發；
    ///   bit=OFF 清空時 → 刪除未派出的 oNeed（Warning），已派出的任務只記 Error，由人員到 RCS 取消。
    /// 空搬偵測（T19）：end 回呼呼叫 WatchArrival 登記目的站，之後 bit=ON → 到料正常；
    ///   連續 ConfirmCount 輪 OFF → 判定空搬，寫 Warning 並觸發 EmptyTransferDetected（只寫 Log，不做畫面）。
    ///   不看 oPort.HaveFlag：end 回呼會無條件把目的站寫成有料。
    /// 掃描跑在自有的專屬 STA 背景執行緒上（ActUtlType 為 STA COM），全程同一條 thread，零跨執行緒 marshaling。
    /// </summary>
    public class StockSensorLinkManager
    {
        /// <summary>sensor 對應；以 loader 建立時於掃描執行緒載入後才指派（WatchArrival 在其他執行緒讀，以 Volatile 存取）。</summary>
        private IList<SensorMapping> _mappings;
        private readonly Func<SensorMappingLoad> _mappingLoader;
        private readonly Func<IBitReader> _readerFactory;
        private readonly IPortStateReader _portReader;
        private readonly IStockClearer _clearer;
        private readonly Log _log;
        private readonly int _pollIntervalMs;
        private readonly int _confirmCount;
        private readonly Dictionary<string, int> _counter;   // address → 連續(OFF 且佔用)輪數
        private readonly Dictionary<string, int> _onCounter; // address → 連續(ON 且來源站為空)輪數
        private readonly IStockRegistrar _registrar;
        private readonly ISourceTaskStore _taskStore;
        private readonly HashSet<string> _sensorOnStations;

        /// <summary>等待到料的目的站（StationNo → 任務資訊與連續 OFF 輪數）；end 回呼（ASP.NET 執行緒）寫、掃描執行緒讀，以 _arrivalLock 保護。</summary>
        private readonly Dictionary<string, PendingArrival> _arrivals = new Dictionary<string, PendingArrival>(StringComparer.OrdinalIgnoreCase);
        private readonly object _arrivalLock = new object();

        private class PendingArrival
        {
            public EmptyTransferInfo Info;
            public int OffCount;
            /// <summary>登記後第一次檢查只設為 true 不計數：該輪讀值可能早於登記（end 回呼夾在讀 bit 與檢查之間）。</summary>
            public bool Armed;
        }

        private IBitReader _reader;
        private Thread _thread;
        private volatile bool _stop;
        /// <summary>掃描執行緒已結束（PLC 連線建立失敗或停止）：WatchArrival 不再登記，避免永遠無法判定。</summary>
        private volatile bool _scanEnded;

        /// <summary>Sensor OFF 確認後清空庫位時觸發（參數：StationNo），供中光電 release 等後續動作掛載。</summary>
        public event Action<string> StockCleared;

        /// <summary>判定空搬時觸發（已寫 Warning Log），供日後擴充通知。</summary>
        public event Action<EmptyTransferInfo> EmptyTransferDetected;

        /// <summary>生產用：reader 由 factory 在 STA 執行緒上建立（PlcDevice 之 COM 須與讀取同一條 thread）。</summary>
        public StockSensorLinkManager(IList<SensorMapping> mappings, Func<IBitReader> readerFactory,
            IStockClearer clearer, IPortStateReader portReader, Log log,
            int pollIntervalMs = 1000, int confirmCount = 3,
            IStockRegistrar registrar = null, ISourceTaskStore taskStore = null, IEnumerable<string> sensorOnStations = null)
        {
            _mappings = mappings ?? new List<SensorMapping>();
            _readerFactory = readerFactory;
            _clearer = clearer;
            _portReader = portReader;
            _log = log;
            _pollIntervalMs = pollIntervalMs <= 0 ? 1000 : pollIntervalMs;
            _confirmCount = confirmCount <= 0 ? 1 : confirmCount;   // 至少確認 1 輪
            _registrar = registrar;
            _taskStore = taskStore;
            _sensorOnStations = new HashSet<string>(sensorOnStations ?? new string[0], StringComparer.OrdinalIgnoreCase);
            _counter = new Dictionary<string, int>();
            _onCounter = new Dictionary<string, int>();
            foreach (var m in _mappings)
            {
                _counter[m.Address] = 0;
                _onCounter[m.Address] = 0;
            }
        }

        /// <summary>
        /// 生產用（T29）：點位由 mappingLoader 於掃描執行緒上載入（DB oPortPlc 優先），
        /// 讀不到時每 MappingRetryMs 重試，不阻塞 ACC 啟動；載入後無啟用點位則不連 PLC、停止監控。
        /// </summary>
        public StockSensorLinkManager(Func<SensorMappingLoad> mappingLoader, Func<IBitReader> readerFactory,
            IStockClearer clearer, IPortStateReader portReader, Log log,
            int pollIntervalMs = 1000, int confirmCount = 3,
            IStockRegistrar registrar = null, ISourceTaskStore taskStore = null, IEnumerable<string> sensorOnStations = null)
            : this((IList<SensorMapping>)null, readerFactory, clearer, portReader, log, pollIntervalMs, confirmCount, registrar, taskStore, sensorOnStations)
        {
            _mappingLoader = mappingLoader;
        }

        /// <summary>點位讀不到（例：DB 尚未就緒）時的重試間隔。</summary>
        public int MappingRetryMs { get; set; } = 10000;

        /// <summary>
        /// 載入點位一次（無 loader 時直接視為已載入）。回傳 false＝暫時讀不到，需重試；true＝已套用（可能為 0 筆）。
        /// 只應在掃描執行緒（或測試）呼叫。
        /// </summary>
        public bool TryLoadMappings()
        {
            if (_mappingLoader == null)
            {
                return true;
            }

            SensorMappingLoad load;
            try
            {
                load = _mappingLoader();
            }
            catch (Exception ex)
            {
                LogLoadFailure($"載入 sensor 點位失敗：{ex.Message}");
                return false;
            }

            if (load == null || load.Retry || load.Mappings == null)
            {
                LogLoadFailure($"暫時無法載入 sensor 點位（{load?.Source}）");
                return false;
            }

            if (_loadFailures > 0)
            {
                _log?.TraceOut($"[StockSensorLink] sensor 點位已恢復載入（先前失敗 {_loadFailures} 次）", Log.LogType.WARN);
                _loadFailures = 0;
            }

            foreach (var warning in load.Warnings)
            {
                _log?.TraceOut($"[StockSensorLink] {warning}", Log.LogType.WARN);
            }

            ApplyMappings(load.Mappings);
            string list = string.Join(",", load.Mappings.Select(m => m.Address + ":" + m.StationNo));
            // 退回設定檔記 WARN：DB 點位未生效，現場改 oPortPlc 不會有作用
            _log?.TraceOut($"[StockSensorLink] sensor 點位來源：{load.Source}，共 {load.Mappings.Count} 筆：{list}",
                load.FellBackToConfig ? Log.LogType.WARN : Log.LogType.TRACE);
            return true;
        }

        /// <summary>點位載入連續失敗次數（只在掃描執行緒存取）。</summary>
        private int _loadFailures;

        /// <summary>DB 長時間連不上時避免洗 Log：第 1 次與之後每 30 次記一筆 WARN。</summary>
        private void LogLoadFailure(string reason)
        {
            _loadFailures++;
            if (_loadFailures == 1 || _loadFailures % 30 == 0)
            {
                _log?.TraceOut($"[StockSensorLink] {reason}（連續第 {_loadFailures} 次），{MappingRetryMs / 1000} 秒後重試，期間不監控 sensor", Log.LogType.WARN);
            }
        }

        private void ApplyMappings(IList<SensorMapping> mappings)
        {
            foreach (var m in mappings)
            {
                _counter[m.Address] = 0;
                _onCounter[m.Address] = 0;
            }
            Volatile.Write(ref _mappings, mappings);
        }

        /// <summary>可被 Stop 中斷的等待。</summary>
        private void SleepUnlessStopped(int totalMs)
        {
            for (int waited = 0; waited < totalMs && !_stop; waited += 200)
            {
                Thread.Sleep(200);
            }
        }

        /// <summary>測試 / 直接用：已有 reader 實例，可不經 Start() 直接呼叫 ScanOnce()。</summary>
        public StockSensorLinkManager(IList<SensorMapping> mappings, IBitReader reader,
            IStockClearer clearer, IPortStateReader portReader, Log log,
            int pollIntervalMs = 1000, int confirmCount = 3,
            IStockRegistrar registrar = null, ISourceTaskStore taskStore = null, IEnumerable<string> sensorOnStations = null)
            : this(mappings, () => reader, clearer, portReader, log, pollIntervalMs, confirmCount, registrar, taskStore, sensorOnStations)
        {
            _reader = reader;
        }

        public void Start()
        {
            // Stop 逾時時保留 _thread：舊執行緒仍在（例：卡在點位查詢）就不重入，避免兩條執行緒同時掃描
            if (_thread != null && _thread.IsAlive)
            {
                return;
            }

            _stop = false;
            _scanEnded = false;
            _thread = new Thread(Execute) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);   // ActUtlType 為 STA COM
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
            _log?.TraceOut("[StockSensorLink] 掃描執行緒 3 秒內未結束（可能正在查詢 DB），將於查詢返回後自行結束", Log.LogType.WARN);
        }

        private void Execute()
        {
            // 點位載入（T29）：DB 未就緒時重試，不阻塞 Application_Start；載入完成前不監控
            while (!_stop && !TryLoadMappings())
            {
                SleepUnlessStopped(MappingRetryMs);
            }

            if (_stop)
            {
                _scanEnded = true;
                return;
            }

            if (Volatile.Read(ref _mappings).Count == 0)
            {
                _scanEnded = true;
                _log?.TraceOut("[StockSensorLink] 沒有啟用的 sensor 點位，停止監控（不連 PLC）", Log.LogType.WARN);
                return;
            }

            try
            {
                _reader = _readerFactory();   // 於 STA 執行緒建立 PlcDevice（COM）
            }
            catch (Exception ex)
            {
                _scanEnded = true;
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

            _scanEnded = true;
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

            foreach (var map in Volatile.Read(ref _mappings))
            {
                // ① 讀 PLC 失敗 → 中斷去抖計數，不動作
                if (!_reader.TryReadBit(map.Address, out bool on))
                {
                    _counter[map.Address] = 0;
                    _onCounter[map.Address] = 0;
                    CancelArrivalOnReadFailure(map);
                    _log?.TraceOut($"[StockSensorLink] 讀取 {map.Address} 失敗，略過該輪", Log.LogType.WARN);
                    continue;
                }

                CheckArrival(map, on);

                // ② 平板在位（bit=ON）→ 計數歸 0
                if (on)
                {
                    _counter[map.Address] = 0;
                    ScanSourceStationOn(map);
                    continue;
                }

                _onCounter[map.Address] = 0;

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
                    HandleSourceStationCleared(map.StationNo);
                    RaiseStockCleared(map.StationNo);
                }
                else
                {
                    _counter[map.Address] = c;
                }
            }
        }

        /// <summary>
        /// 中光電來源站 bit=ON：HaveFlag=0 且無進行中任務才累積計數，達 ConfirmCount 寫 HaveFlag=1。
        /// 有進行中任務時不寫：outbin 已清空起點但貨架尚未離開 sensor，避免誤登記後又觸發派車。
        /// </summary>
        private void ScanSourceStationOn(SensorMapping map)
        {
            if (!_sensorOnStations.Contains(map.StationNo))
            {
                return;
            }

            var port = _portReader?.GetPort(map.StationNo);
            if (port == null || port.HaveFlag != "0" || (_taskStore != null && _taskStore.HasActiveTask(map.StationNo)))
            {
                _onCounter[map.Address] = 0;
                return;
            }

            int c = (_onCounter.TryGetValue(map.Address, out int cv) ? cv : 0) + 1;
            if (c >= _confirmCount)
            {
                _registrar?.MarkRackPresent(map.StationNo);
                _log?.TraceOut($"[StockSensorLink] {map.Address} 連續 {_confirmCount} 輪 ON 且庫位為空，登記 {map.StationNo} HaveFlag=1", Log.LogType.TRACE);
                _onCounter[map.Address] = 0;
            }
            else
            {
                _onCounter[map.Address] = c;
            }
        }

        /// <summary>
        /// 任務送達目的站（end 回呼）時登記等待到料。同站已有登記時以新任務取代並重新起算。
        /// 目的站不在 SensorMap → NoSensor（記 Trace）；站號為空 → InvalidStation（記資料異常 Warning），兩者都不判定空搬。
        /// </summary>
        public ArrivalWatchResult WatchArrival(string endStation, string taskCode, string beginStation, DateTime endTime)
        {
            if (string.IsNullOrWhiteSpace(endStation))
            {
                _log?.TraceOut($"[StockSensorLink] 空搬偵測略過：任務 {taskCode} 目的站號為空（資料異常），起點 {beginStation}", Log.LogType.WARN);
                return ArrivalWatchResult.InvalidStation;
            }

            string station = endStation.Trim();
            bool hasSensor = false;
            foreach (var m in Volatile.Read(ref _mappings))   // 點位尚未載入時為空清單 → NoSensor
            {
                if (string.Equals(m.StationNo, station, StringComparison.OrdinalIgnoreCase))
                {
                    hasSensor = true;
                    break;
                }
            }

            if (!hasSensor)
            {
                _log?.TraceOut($"[StockSensorLink] 空搬偵測略過：目的站 {station} 無 sensor 對應（任務 {taskCode}）", Log.LogType.TRACE);
                return ArrivalWatchResult.NoSensor;
            }

            if (_scanEnded)
            {
                _log?.TraceOut($"[StockSensorLink] 空搬偵測略過：sensor 掃描未執行（任務 {taskCode}，目的站 {station}）", Log.LogType.TRACE);
                return ArrivalWatchResult.NotMonitoring;
            }

            lock (_arrivalLock)
            {
                _arrivals[station] = new PendingArrival { Info = new EmptyTransferInfo(station, taskCode, beginStation, endTime) };
            }
            return ArrivalWatchResult.Watching;
        }

        /// <summary>
        /// 空搬偵測：目的站 bit=ON → 到料正常解除；登記後第一輪 OFF 不計（讀值可能早於登記），
        /// 之後連續 ConfirmCount 輪 OFF → 判定空搬並解除（只報一次）。
        /// </summary>
        private void CheckArrival(SensorMapping map, bool on)
        {
            EmptyTransferInfo detected = null;
            lock (_arrivalLock)
            {
                if (!_arrivals.TryGetValue(map.StationNo, out var pending))
                {
                    return;
                }

                if (on)
                {
                    _arrivals.Remove(map.StationNo);
                }
                else if (!pending.Armed)
                {
                    pending.Armed = true;
                }
                else if (++pending.OffCount >= _confirmCount)
                {
                    _arrivals.Remove(map.StationNo);
                    detected = pending.Info;
                }
            }

            if (detected == null)
            {
                return;
            }

            _log?.TraceOut($"[StockSensorLink] 疑似空搬：任務 {detected.TaskCode} {detected.BeginStation}→{detected.EndStation} "
                + $"於 {detected.EndTime:yyyy-MM-dd HH:mm:ss} 完成，目的站 sensor {map.Address} 連續 {_confirmCount} 輪未亮（可能有人為介入）", Log.LogType.WARN);
            try
            {
                EmptyTransferDetected?.Invoke(detected);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[StockSensorLink] {detected.EndStation} 空搬後續動作失敗：{ex.Message}", Log.LogType.WARN);
            }
        }

        /// <summary>
        /// 讀取失敗時取消該站的空搬偵測：保留登記會在 PLC 長時間斷線後、貨架早已正常取走時誤報舊任務。
        /// </summary>
        private void CancelArrivalOnReadFailure(SensorMapping map)
        {
            EmptyTransferInfo cancelled = null;
            lock (_arrivalLock)
            {
                if (_arrivals.TryGetValue(map.StationNo, out var pending))
                {
                    _arrivals.Remove(map.StationNo);
                    cancelled = pending.Info;
                }
            }

            if (cancelled != null)
            {
                _log?.TraceOut($"[StockSensorLink] {map.Address} 讀取失敗，取消任務 {cancelled.TaskCode}（{cancelled.BeginStation}→{cancelled.EndStation}）的空搬偵測", Log.LogType.WARN);
            }
        }

        private void RaiseStockCleared(string stationNo)
        {
            try
            {
                StockCleared?.Invoke(stationNo);
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[StockSensorLink] {stationNo} 清空後續動作失敗：{ex.Message}", Log.LogType.WARN);
            }
        }

        /// <summary>中光電來源站被清空後（貨架被移走）：刪除未派出 oNeed；已派出任務只記 Error，不自動取消 RCS 任務。</summary>
        private void HandleSourceStationCleared(string stationNo)
        {
            if (_taskStore == null || !_sensorOnStations.Contains(stationNo))
            {
                return;
            }

            int deleted = _taskStore.DeleteUndispatchedNeeds(stationNo);
            if (deleted > 0)
            {
                _log?.TraceOut($"[StockSensorLink] {stationNo} 貨架被移走，刪除未派出的 oNeed {deleted} 筆", Log.LogType.WARN);
            }

            if (_taskStore.HasRunningMission(stationNo))
            {
                // AGV 已開始執行：正常取貨時頂起貨架後 sensor 會先於 outbin 回呼熄滅，不視為異常
                _log?.TraceOut($"[StockSensorLink] {stationNo} sensor 熄滅且 AGV 任務執行中，視為取貨中（outbin 尚未回報）", Log.LogType.TRACE);
            }
            else if (_taskStore.HasActiveTask(stationNo))
            {
                _log?.TraceOut($"[StockSensorLink] {stationNo} 貨架被移走，但已有派出、AGV 尚未開始的任務，請人員至 RCS 取消", Log.LogType.ERROR);
            }
        }

        /// <summary>解析 "D1,O1" 為站號清單。空白與空片段略過。</summary>
        public static List<string> ParseStationList(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var part in raw.Split(','))
            {
                var token = part.Trim();
                if (token.Length > 0)
                {
                    result.Add(token);
                }
            }

            return result;
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

        /// <summary>oPortPlc 資料列轉 sensor 對應：只取 SensorUseFlag=Y 且站號與 SensorAddress 皆非空的列（T29）。</summary>
        public static List<SensorMapping> BuildSensorMappings(IEnumerable<oPortPlcModel> rows)
            => BuildSensorMappings(rows, out _);

        /// <summary>同上；同一 PLC 位址被多站使用時保留第一站、略過後者並回傳說明（一個 bit 的 OFF 不可同時清兩個庫位）。</summary>
        public static List<SensorMapping> BuildSensorMappings(IEnumerable<oPortPlcModel> rows, out List<string> skipped)
        {
            var result = new List<SensorMapping>();
            skipped = new List<string>();
            if (rows == null)
            {
                return result;
            }

            var usedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (row == null || !string.Equals(row.SensorUseFlag?.Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var station = row.StationNo?.Trim();
                var address = row.SensorAddress?.Trim();
                if (string.IsNullOrEmpty(station) || string.IsNullOrEmpty(address))
                {
                    continue;   // 例：只有三色燈、沒有 sensor 的站
                }

                string key = PlcAddress.Normalize(address);   // M010＝M10
                if (usedBy.TryGetValue(key, out string first))
                {
                    skipped.Add($"oPortPlc sensor 位址 {address} 重複：{first} 已使用，略過 {station}，請修正 DB");
                    continue;
                }

                usedBy[key] = station;
                result.Add(new SensorMapping(address, station));
            }

            return result;
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
    /// Enable!=true 或 Section 缺失時回傳 null（不啟動監控）；點位（DB oPortPlc 為準）由 manager 在掃描執行緒載入，
    /// 載入後沒有啟用點位則 manager 自行停止、不連 PLC（T29）。
    /// </summary>
    public static class StockSensorLinkBootstrap
    {
        /// <summary>執行中的監控（未啟動為 null）；供 end 回呼登記空搬偵測（比照 CgdWcsBootstrap.Current）。</summary>
        public static StockSensorLinkManager Current { get; set; }

        /// <summary>
        /// 決定 sensor 點位（T29，code review C1 修正後規則）：
        ///   oPortPlc 有任何資料列 → 以 DB 為準（全部停用就是不監控，不可退回設定檔把現場關掉的 sensor 重新打開）；
        ///   oPortPlc 不存在（SQL 錯誤 208）或沒有資料列 → 退回設定檔 SensorMap（升級過渡期：尚未執行部署 SQL）；
        ///   其他讀取失敗（例：DB 連不上）→ Retry，由呼叫端稍後重試，不用設定檔的舊點位頂替。
        /// </summary>
        public static SensorMappingLoad ResolveSensorMappings(Func<List<oPortPlcModel>> loadDb, string configSensorMap,
            Func<Exception, bool> isTableMissing = null)
        {
            List<oPortPlcModel> rows;
            try
            {
                rows = loadDb?.Invoke();
            }
            catch (Exception ex)
            {
                if ((isTableMissing ?? IsTableMissing)(ex))
                {
                    return FromConfig(configSensorMap, "oPortPlc 資料表不存在，請執行 Deploy_oPortPlc_ABBuilding1F.sql");
                }
                return new SensorMappingLoad(null, $"讀取 oPortPlc 失敗：{ex.Message}", false, retry: true);
            }

            if (rows == null || rows.Count == 0)
            {
                return FromConfig(configSensorMap, "oPortPlc 沒有資料");
            }

            var mappings = StockSensorLinkManager.BuildSensorMappings(rows, out List<string> skipped);
            return new SensorMappingLoad(mappings, $"DB oPortPlc（{rows.Count} 列，啟用 {mappings.Count} 筆）", false, false, skipped);
        }

        private static SensorMappingLoad FromConfig(string configSensorMap, string reason)
            => new SensorMappingLoad(StockSensorLinkManager.ParseSensorMap(configSensorMap),
                $"設定檔 SensorMap=\"{configSensorMap}\"（{reason}；DB 點位未生效）", true, false);

        /// <summary>例外鏈中是否有 SqlException 208（Invalid object name：資料表不存在）。</summary>
        public static bool IsTableMissing(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Data.SqlClient.SqlException sql && sql.Number == 208)
                {
                    return true;
                }
            }
            return false;
        }

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

            var dbSettings = (config.GetSection(nameof(SectionDB)) as SectionDB)?.DBSettings;
            var logSettings = (config.GetSection(nameof(SectionLog)) as SectionLog)?.LogSettings;

            Log log = logSettings != null ? new Log(logSettings.LogPath, "StockSensorLink") : new Log("StockSensorLink");
            if (logSettings != null)
            {
                log.KeepDate = logSettings.KeepDate;
            }

            var db = new SQLData(dbSettings?.DBName, dbSettings?.DBIP);

            // 點位以 DB oPortPlc 為準（T29），由掃描執行緒載入（DB 未就緒時重試），不在 Application_Start 同步查 DB
            string configSensorMap = settings.SensorMap;
            Func<SensorMappingLoad> mappingLoader = () => ResolveSensorMappings(db.Select_oPortPlc, configSensorMap);

            int station = settings.StationNumber;
            Func<IBitReader> readerFactory = () => new PlcDevice(station, log);
            IPortStateReader portReader = new SqlPortStateReader(db, log);
            IStockClearer clearer = new SqlStockClearer(db, log);
            IStockRegistrar registrar = new SqlStockRegistrar(db, log);
            ISourceTaskStore taskStore = new SqlSourceTaskStore(db, log);
            var sensorOnStations = StockSensorLinkManager.ParseStationList(settings.SensorOnStations);

            return new StockSensorLinkManager(mappingLoader, readerFactory, clearer, portReader, log,
                settings.PollIntervalMs, settings.ConfirmCount, registrar, taskStore, sensorOnStations);
        }
    }
}
