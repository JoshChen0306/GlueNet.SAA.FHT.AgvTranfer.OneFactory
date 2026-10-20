using ActUtlType64Lib;
using ActUtlTypeLib;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>
    /// 單一三菱 PLC 連線封裝（MX Component ActUtlType COM）。
    /// 只負責「以 Logical Station Number 連線、讀／寫單一 bit」，不碰庫位 / DB / 設定檔。
    /// 依行程位元（IntPtr.Size）自動選 32 位 ActUtlType 或 64 位 ActUtlType64，
    /// 須由呼叫端在同一條 STA 執行緒上建立與使用（COM 為 STA，避免跨執行緒 marshaling）。
    /// </summary>
    public class PlcDevice : IBitReader, IWordReader, IBitWriter, IPlcConnectionState, IDisposable
    {
        private readonly int _stationNumber;
        private readonly IActAdapter _act;
        private readonly Log _log;
        private bool _isOpen;
        /// <summary>連續 Open 失敗次數（PLC 離線時 Log 節流）。</summary>
        private int _openFailures;
        /// <summary>各點位連續讀寫失敗次數（key：R:位址 / W:位址），持續性錯誤時 Log 節流。</summary>
        private readonly Dictionary<string, int> _opFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 最近一次讀寫是否因「連不上 PLC（Open 失敗）」而失敗。
        /// 位址或裝置錯誤（SetDevice2/GetDevice2 回錯誤碼）不算：呼叫端只在此為 true 時中止整輪（2026-10-20 code review）。
        /// </summary>
        public bool LastOpenFailed { get; private set; }

        public PlcDevice(int stationNumber, Log log = null)
        {
            _stationNumber = stationNumber;
            _log = log;
            _act = IntPtr.Size == 8
                ? (IActAdapter)new Act64Adapter(stationNumber)
                : new Act86Adapter(stationNumber);
        }

        /// <summary>確保連線已開啟；已開啟則不重複 Open。Open 失敗或例外都計入 Open 失敗次數。</summary>
        private void EnsureConnected()
        {
            if (_isOpen)
            {
                return;
            }

            int rc;
            try
            {
                rc = _act.Open();
            }
            catch (Exception ex)
            {
                OnOpenFailed($"Open 例外：{ex.Message}");
                return;
            }

            if (rc == 0)
            {
                _isOpen = true;
                if (_openFailures > 0)
                {
                    _log?.TraceOut($"[PlcDevice] 已恢復連線，站號 {_stationNumber}（先前 Open 失敗 {_openFailures} 次）", Log.LogType.WARN);
                    _openFailures = 0;
                }
                return;
            }

            OnOpenFailed($"Open 失敗，回傳碼 0x{rc:X8}");
        }

        private void OnOpenFailed(string reason)
        {
            ResetConnection();
            _openFailures++;
            if (_openFailures == 1 || _openFailures % 30 == 0)
            {
                _log?.TraceOut($"[PlcDevice] {reason}，站號 {_stationNumber}（連續第 {_openFailures} 次）", Log.LogType.WARN);
            }
        }

        /// <summary>
        /// 讀寫失敗後重置連線：必須真的 Close COM 物件，否則下次 Open 會回「已開啟」而失敗，
        /// 連帶讓下一個點位（例：下一站的燈）也失敗（2026-10-20 code review C2）。
        /// </summary>
        private void ResetConnection()
        {
            _isOpen = false;
            try
            {
                _act.Close();
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[PlcDevice] 重置連線時 Close 例外：{ex.Message}", Log.LogType.WARN);
            }
        }

        /// <summary>點位讀寫失敗：重置連線（下次重連），Log 依點位節流（第 1 次與每 30 次）。</summary>
        private void OnOperationFailed(string key, string message)
        {
            ResetConnection();
            int n = (_opFailures.TryGetValue(key, out int c) ? c : 0) + 1;
            _opFailures[key] = n;
            if (n == 1 || n % 30 == 0)
            {
                _log?.TraceOut($"[PlcDevice] {message}（連續第 {n} 次）", Log.LogType.WARN);
            }
        }

        private void OnOperationSucceeded(string key)
        {
            if (_opFailures.TryGetValue(key, out int n))
            {
                _opFailures.Remove(key);
                _log?.TraceOut($"[PlcDevice] {key} 已恢復（先前失敗 {n} 次）", Log.LogType.WARN);
            }
        }

        /// <summary>讀取單一 bit。回傳 false 代表讀取失敗（呼叫端不應更新基準、不應清空）。</summary>
        public bool TryReadBit(string address, out bool on)
        {
            bool ok = TryGetDevice(address, out short value);
            on = ok && value != 0;
            return ok;
        }

        /// <summary>讀取單一 word（T30，例：CPU 運行狀態 SD203）。回傳 false 代表讀取失敗。</summary>
        public bool TryReadWord(string address, out short value) => TryGetDevice(address, out value);

        /// <summary>
        /// 單點讀取用 GetDevice2（讀單一裝置：bit device 回 0/1，word device 回 16 位元值）。
        /// 不可用 ReadDeviceBlock2 —— 它讀 word 區塊、需 word 對齊，對 bit 位址（如 M10）會回錯誤碼。
        /// </summary>
        private bool TryGetDevice(string address, out short value)
        {
            value = 0;
            string key = "R:" + address;
            try
            {
                EnsureConnected();
                LastOpenFailed = !_isOpen;
                if (!_isOpen)
                {
                    return false;
                }

                int rc = _act.GetDevice2(address, out short data);

                if (rc != 0)
                {
                    OnOperationFailed(key, $"讀取 {address} 失敗，回傳碼 0x{rc:X8}");
                    return false;
                }

                OnOperationSucceeded(key);
                value = data;
                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed(key, $"讀取 {address} 例外：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 寫入單一 bit（T17，三色燈用）。回傳 false 代表寫入失敗（呼叫端下輪重寫）。
        /// 單點寫入用 SetDevice2（bit device 寫 0/1），與讀取的 GetDevice2 對稱；不用 WriteDeviceBlock2（word 區塊、需 word 對齊）。
        /// </summary>
        public bool TryWriteBit(string address, bool on)
        {
            string key = "W:" + address;
            try
            {
                EnsureConnected();
                LastOpenFailed = !_isOpen;
                if (!_isOpen)
                {
                    return false;
                }

                int rc = _act.SetDevice2(address, on ? (short)1 : (short)0);
                if (rc != 0)
                {
                    OnOperationFailed(key, $"寫入 {address}={(on ? 1 : 0)} 失敗，回傳碼 0x{rc:X8}");
                    return false;
                }

                OnOperationSucceeded(key);
                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed(key, $"寫入 {address} 例外：{ex.Message}");
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (_isOpen)
                {
                    _act.Close();
                }
            }
            catch (Exception ex)
            {
                _log?.TraceOut($"[PlcDevice] Close 例外：{ex.Message}", Log.LogType.WARN);
            }
            finally
            {
                _isOpen = false;
            }
        }

        public void Dispose()
        {
            Close();
        }

        // --- 內部 COM 配接：32 / 64 位共用同一介面 ---
        private interface IActAdapter
        {
            int Open();
            int Close();
            int GetDevice2(string device, out short data);
            int SetDevice2(string device, short data);
        }

        private sealed class Act86Adapter : IActAdapter
        {
            private readonly ActUtlTypeClass _act;
            public Act86Adapter(int station) => _act = new ActUtlTypeClass { ActLogicalStationNumber = station };
            public int Open() => _act.Open();
            public int Close() => _act.Close();
            public int GetDevice2(string device, out short data) => _act.GetDevice2(device, out data);
            public int SetDevice2(string device, short data) => _act.SetDevice2(device, data);
        }

        private sealed class Act64Adapter : IActAdapter
        {
            private readonly ActUtlType64Class _act;
            public Act64Adapter(int station) => _act = new ActUtlType64Class { ActLogicalStationNumber = station };
            public int Open() => _act.Open();
            public int Close() => _act.Close();
            public int GetDevice2(string device, out short data) => _act.GetDevice2(device, out data);
            public int SetDevice2(string device, short data) => _act.SetDevice2(device, data);
        }
    }
}
