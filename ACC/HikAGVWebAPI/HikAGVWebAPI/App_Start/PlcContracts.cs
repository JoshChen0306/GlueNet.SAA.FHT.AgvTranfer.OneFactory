using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HikAGVWebAPI.App_Start
{
    /// <summary>PLC bit 寫入抽象（T17），解耦 MX Component COM，方便單元測試。</summary>
    public interface IBitWriter
    {
        /// <summary>寫入單一 bit。回傳 false 代表寫入失敗（呼叫端下輪重寫）。</summary>
        bool TryWriteBit(string address, bool on);
    }

    /// <summary>
    /// PLC 連線狀態（PlcDevice 實作）：最近一次讀寫因 Open 失敗（連不上 PLC）而失敗時為 true，呼叫端中止整輪，避免每點都等 Open 逾時。
    /// 位址或裝置錯誤不算（否則一個寫不進去的位址會讓之後所有站每輪都被跳過）。
    /// </summary>
    public interface IPlcConnectionState
    {
        bool LastOpenFailed { get; }
    }

    /// <summary>三菱 PLC 位址工具。</summary>
    public static class PlcAddress
    {
        /// <summary>十六進位編號的裝置前綴（長的在前，避免 SB 被當成 S+B）；其餘裝置編號為十進位。</summary>
        private static readonly string[] HexDevicePrefixes = { "SB", "SW", "DX", "DY", "B", "W", "X", "Y" };
        private static readonly Regex DecimalDevicePattern = new Regex(@"^([A-Z]+)(\d+)$", RegexOptions.CultureInvariant);

        /// <summary>位址正規化供重複比對：大寫、編號去前導零（M0200＝M200、B0A＝BA）；無法解析時原樣大寫。</summary>
        public static string Normalize(string address)
        {
            var a = address?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(a))
            {
                return a;
            }

            foreach (var prefix in HexDevicePrefixes)
            {
                if (a.Length > prefix.Length && a.StartsWith(prefix, StringComparison.Ordinal)
                    && long.TryParse(a.Substring(prefix.Length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long hexValue))
                {
                    return prefix + hexValue.ToString("X");
                }
            }

            var m = DecimalDevicePattern.Match(a);
            if (m.Success && long.TryParse(m.Groups[2].Value, out long value))
            {
                return m.Groups[1].Value + value;
            }

            return a;
        }
    }
}
