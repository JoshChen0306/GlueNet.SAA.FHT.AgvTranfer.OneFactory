using System;
using ActUtlType64Lib;
using ActUtlTypeLib;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>
    /// 單一三菱 PLC 連線封裝（MX Component ActUtlType COM）。
    /// 只負責「以 Logical Station Number 連線、讀單一 bit」，不碰庫位 / DB / 設定檔。
    /// 依行程位元（IntPtr.Size）自動選 32 位 ActUtlType 或 64 位 ActUtlType64，
    /// 須由呼叫端在同一條 STA 執行緒上建立與使用（COM 為 STA，避免跨執行緒 marshaling）。
    /// </summary>
    public class PlcDevice : IBitReader, IDisposable
    {
        private readonly int _stationNumber;
        private readonly IActAdapter _act;
        private readonly Log _log;
        private bool _isOpen;

        public PlcDevice(int stationNumber, Log log = null)
        {
            _stationNumber = stationNumber;
            _log = log;
            _act = IntPtr.Size == 8
                ? (IActAdapter)new Act64Adapter(stationNumber)
                : new Act86Adapter(stationNumber);
        }

        /// <summary>確保連線已開啟；已開啟則不重複 Open。</summary>
        private void EnsureConnected()
        {
            if (_isOpen)
            {
                return;
            }

            if (_act.Open() == 0)
            {
                _isOpen = true;
            }
            else
            {
                _log?.TraceOut($"[PlcDevice] Open 失敗，站號 {_stationNumber}", Log.LogType.WARN);
            }
        }

        /// <summary>讀取單一 bit。回傳 false 代表讀取失敗（呼叫端不應更新基準、不應清空）。</summary>
        public bool TryReadBit(string address, out bool on)
        {
            on = false;
            try
            {
                EnsureConnected();
                if (!_isOpen)
                {
                    return false;
                }

                // 單點讀取：lSize = 1；bit device 回 0/1
                int rc = _act.ReadDeviceBlock2(address, 1, out short value);
                if (rc != 0)
                {
                    _isOpen = false;   // 失敗重置，下輪重連
                    _log?.TraceOut($"[PlcDevice] 讀取 {address} 失敗，回傳碼 {rc}", Log.LogType.WARN);
                    return false;
                }

                on = value != 0;
                return true;
            }
            catch (Exception ex)
            {
                _isOpen = false;
                _log?.TraceOut($"[PlcDevice] 讀取 {address} 例外：{ex.Message}", Log.LogType.WARN);
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
            int ReadDeviceBlock2(string device, int size, out short data);
        }

        private sealed class Act86Adapter : IActAdapter
        {
            private readonly ActUtlTypeClass _act;
            public Act86Adapter(int station) => _act = new ActUtlTypeClass { ActLogicalStationNumber = station };
            public int Open() => _act.Open();
            public int Close() => _act.Close();
            public int ReadDeviceBlock2(string device, int size, out short data) => _act.ReadDeviceBlock2(device, size, out data);
        }

        private sealed class Act64Adapter : IActAdapter
        {
            private readonly ActUtlType64Class _act;
            public Act64Adapter(int station) => _act = new ActUtlType64Class { ActLogicalStationNumber = station };
            public int Open() => _act.Open();
            public int Close() => _act.Close();
            public int ReadDeviceBlock2(string device, int size, out short data) => _act.ReadDeviceBlock2(device, size, out data);
        }
    }
}
