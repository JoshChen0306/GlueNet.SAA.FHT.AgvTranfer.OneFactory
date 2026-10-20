/// <summary>
/// 站點 PLC 點位（oPortPlc，一站一列）：sensor 位址與三色燈綠/黃/紅位址。
/// 2026-10-20 位址由我方暫定，迅得正式位址到位後改 DB、重啟 ACC 生效（T29）。
/// sensor 與三色燈各自有啟用旗標：暫定位址未經迅得確認前不可啟用（未接線的 sensor 讀到 OFF 會誤清庫位；寫入未規劃的 M bit 會干擾 PLC 程式）。
/// </summary>
public class oPortPlcModel
{
    public string StationNo { get; set; }
    public string SensorAddress { get; set; }
    /// <summary>Y＝監控 sensor（StockSensorLink）。</summary>
    public string SensorUseFlag { get; set; }
    public string LampGreen { get; set; }
    public string LampYellow { get; set; }
    public string LampRed { get; set; }
    /// <summary>Y＝輸出三色燈（RackStatusOutput）。</summary>
    public string LampUseFlag { get; set; }
    public string Remark { get; set; }
}
