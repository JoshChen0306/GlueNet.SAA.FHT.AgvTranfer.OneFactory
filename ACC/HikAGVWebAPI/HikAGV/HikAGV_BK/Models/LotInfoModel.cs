using Newtonsoft.Json;
using System;

public class LotInfoModel
{
    private string _GUID = Guid.NewGuid().ToString();
    private string _Stocker = string.Empty;
    private string _BeginStation = string.Empty;
    private string _EndStation = string.Empty;
    private string _RackNo = string.Empty;
    private string _LotNo = string.Empty;
    private bool _IsEmpty;

    /// <summary>
    /// GUID 唯一碼
    /// </summary>
    public string GUID
    {
        get { return _GUID; }
        set { _GUID = value; }
    }

    public string Stocker
    {
        get
        {
            _Stocker = BeginStation.Substring(0, 1);
            return _Stocker;
        }
        set { _Stocker = value; }
    }

    /// <summary>
    /// 起點
    /// </summary>
    public string BeginStation
    {
        get { return _BeginStation; }
        set { _BeginStation = value; }
    }

    /// <summary>
    /// 終點
    /// </summary>
    public string EndStation
    {
        get { return _EndStation; }
        set { _EndStation = value; }
    }

    public string RackNo
    {
        get { return _RackNo; }
        set { _RackNo = value; }
    }

    /// <summary>
    /// 批號
    /// </summary>
    public string LotNo
    {
        get { return _LotNo; }
        set { _LotNo = value; }
    }

    internal string ModifiedTime { get; set; } = DateTime.Now.ToString("yyyyMMddHHmmss");

    /// <summary>
    /// 是否為空料
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            _IsEmpty = string.IsNullOrEmpty(_LotNo) ? true : false;
            return _IsEmpty;
        }
        set { _IsEmpty = value; }
    }

    public string ToJson()
    {
        LotInfoModel LotInfo = new LotInfoModel()
        {
            GUID = _GUID,
            Stocker = _Stocker,
            BeginStation = _BeginStation,
            EndStation = _EndStation,
            RackNo = _RackNo,
            LotNo = _LotNo,
            IsEmpty = _IsEmpty,
        };

        return JsonConvert.SerializeObject(LotInfo);
    }
}