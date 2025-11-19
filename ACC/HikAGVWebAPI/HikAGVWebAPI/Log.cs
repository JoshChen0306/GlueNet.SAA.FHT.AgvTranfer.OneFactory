using System;
using System.Globalization;
using System.IO;
using System.Text;

public class Log : IDisposable
{
    private string m_ErrMessage = "";
    private string m_RootPath = "";
    private string m_LastLogPath = "";
    private string m_NowLogPath = "";
    private string m_LastLogName = "";
    private string m_NowLogName = "";
    private DateTime m_NowLogDT;
    private StreamWriter m_StreamWriter;
    private ACHIVETYPE m_AchiveType;
    private bool m_shortDateTime = false;
    private bool m_NoDatetime = false;
    private string m_header = string.Empty;
    private int m_KeepDate = 30;

    public enum ACHIVETYPE
    {
        HOUR,
        DAY,
        DAY_NODATE
    }

    public enum LogType
    {
        DEBUG,
        TRACE,
        WARN,
        ERROR,
        NONE
    }

    public enum TrxType
    {
        IN,
        OUT,
    }

    private void ForceDirectories(string ADir)
    {
        if (ADir.Length < 3)
        {
            throw new IOException(string.Format("無法建立 {0} 資料夾 !", ADir));
        }
        else
        {
            Directory.CreateDirectory(ADir);
        }
    }

    public Log()
    {
        m_ErrMessage = "";
        m_AchiveType = ACHIVETYPE.HOUR;
        m_shortDateTime = false;

        try
        {
            string AutoDir = string.Format("{0}Log\\", Directory.GetCurrentDirectory().Substring(0, 3));
            m_RootPath = AutoDir;

            // Folder Create 
            if (!Directory.Exists(m_RootPath))
            {
                ForceDirectories(m_RootPath);
            }
        }
        catch (Exception ex)
        {
            m_ErrMessage = ex.ToString();
        }
    }

    public Log(string EqpName)
    {
        m_ErrMessage = "";
        m_AchiveType = ACHIVETYPE.HOUR;
        m_shortDateTime = false;

        try
        {
            m_RootPath = string.Format("{0}\\{1}\\", Directory.GetCurrentDirectory(), EqpName);
            //----更新到Log資料夾
            m_RootPath = m_RootPath.Replace("Release", "LOG");
            // Folder Create 
            if (!Directory.Exists(m_RootPath))
            {
                ForceDirectories(m_RootPath);
            }
        }
        catch (Exception ex)
        {
            m_ErrMessage = ex.ToString();
        }
    }

    public Log(string WorkPath, string EqpName)
    {
        m_ErrMessage = "";
        m_AchiveType = ACHIVETYPE.HOUR;
        m_shortDateTime = false;

        try
        {
            m_RootPath = string.Format("{0}\\{1}\\", WorkPath, EqpName);
            //----更新到Log資料夾
            m_RootPath = m_RootPath.Replace("Release", "LOG");
            // Folder Create 
            if (!Directory.Exists(m_RootPath))
            {
                ForceDirectories(m_RootPath);
            }
        }
        catch (Exception ex)
        {
            m_ErrMessage = ex.ToString();
        }
    }

    public void TraceOut(string message, LogType lt)
    {
        string type = "";

        switch (lt)
        {
            case LogType.TRACE:
                type = " [ Trace ] ";
                break;
            case LogType.ERROR:
                type = " [ Error ] ";
                break;
            case LogType.WARN:
                type = " [ Warn  ] ";
                break;
            case LogType.DEBUG:
                type = " [ Debug ] ";
                break;
            case LogType.NONE:
                type = " [       ] ";
                break;
            default:
                break;
        }

        DateTime dt = DateTime.Now;
        SaveLog(dt, type, message, m_shortDateTime);
    }

    public void Dispose()
    {
        this.m_StreamWriter.Close();
        GC.SuppressFinalize(this);
    }

    public bool SaveLog(DateTime dt, string ATarget, string AResult, bool shortDateTime)
    {
        try
        {

            m_NowLogDT = dt;
            switch (m_AchiveType)
            {
                case ACHIVETYPE.HOUR:
                    m_NowLogName = m_NowLogDT.ToString("yyyyMMddHH") + ".log";
                    break;
                case ACHIVETYPE.DAY:
                case ACHIVETYPE.DAY_NODATE:
                    m_NowLogName = m_NowLogDT.ToString("yyyyMMdd") + ".log";
                    break;
            }

            bool writeHeader = false;
            switch (m_AchiveType)
            {
                case ACHIVETYPE.HOUR:
                    m_NowLogPath = string.Format(@"{0}\{1:0000}-{2:00}-{3:00}\\",
                                                 m_RootPath, m_NowLogDT.Year, m_NowLogDT.Month, m_NowLogDT.Day);

                    break;
                case ACHIVETYPE.DAY:
                case ACHIVETYPE.DAY_NODATE:
                    m_NowLogPath = m_RootPath;
                    break;
            }

            // Folder Create 
            if (!Directory.Exists(m_NowLogPath))
            {
                ForceDirectories(m_NowLogPath);
            }

            if (m_header != string.Empty)
            {
                if (!File.Exists(m_NowLogPath + m_NowLogName))
                {
                    writeHeader = true;
                }
            }

            #region 刪除 m_KeepDate 天以前資料夾
            DateTime dtDelete = dt.AddDays(-m_KeepDate);
            string[] sDir = Directory.GetDirectories(m_RootPath);

            try
            {
                foreach (string dir in sDir)
                {
                    string folderName = new DirectoryInfo(dir).Name;
                    DateTime drFolder;
                    bool bResult = DateTime.TryParseExact(folderName, "yyyy-MM-dd", null, DateTimeStyles.None, out drFolder);
                    if (bResult)
                    {
                        int result = dtDelete.CompareTo(drFolder);
                        if (result > 0)
                        {
                            // 判斷文件夾是否存在
                            if (Directory.Exists(dir))
                            {
                                // 刪除文件夾
                                Directory.Delete(dir, true); // 第二個參數為 true 表示刪除文件夾以及其中的所有文件和子文件夾
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            #endregion 刪除 m_KeepDate 天以前資料夾

            lock (this)
            {
                using (m_StreamWriter = new StreamWriter(m_NowLogPath + m_NowLogName, true, Encoding.Unicode))
                {
                    m_StreamWriter.AutoFlush = true;
                    if (writeHeader) m_StreamWriter.WriteLine(m_header);
                    m_LastLogPath = m_NowLogPath;
                    m_LastLogName = m_NowLogName;

                    try
                    {
                        if (m_NoDatetime)
                        {
                            m_StreamWriter.WriteLine(string.Format("{0}{1}", ATarget, AResult));
                        }
                        else
                        {
                            if (shortDateTime == false)
                            {
                                m_StreamWriter.WriteLine(string.Format("{0}{1}{2}",
                                                                       dt.ToString("yyyy/MM/dd HH:mm:ss.fff"),
                                                                       ATarget,
                                                                       AResult));
                            }
                            else
                            {
                                m_StreamWriter.WriteLine(string.Format("{0}{1}{2}",
                                                                       dt.ToString("yyyy/MM/dd HH:mm:ss"),
                                                                       ATarget,
                                                                       AResult));
                            }
                        }

                        m_StreamWriter.Close();
                        return true;
                    }
                    catch (IOException e)
                    {
                        this.m_ErrMessage = e.ToString();
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.StackTrace}\n{ex.Message}");
            return false;
        }
    }

    public string LogName
    {
        get
        {
            return m_NowLogName;
        }
    }

    /// <summary>
    /// Log 保留時間
    /// </summary>
    public int KeepDate
    {
        get
        {
            return m_KeepDate;
        }
        set
        {
            m_KeepDate = value;
        }
    }
}