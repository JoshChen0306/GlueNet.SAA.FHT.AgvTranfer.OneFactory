using Microsoft.EntityFrameworkCore;
using SCP.Models;
using Serilog;
using System.Reflection;
using System.Text;

namespace SCP.Commons
{
    public class ExportToCSVService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private Timer _timer;

        public ExportToCSVService(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(ExecuteSql, null, TimeSpan.Zero, TimeSpan.FromDays(1)); // 每天執行一次
            return Task.CompletedTask;
        }

        private void ExecuteSql(object? state)
        {
            //try
            //{
            //    using (IServiceScope scope = _serviceProvider.CreateScope())
            //    {
            //        atcDB_SCPContext _DBContext = scope.ServiceProvider.GetRequiredService<atcDB_SCPContext>();
            //        DateTime now = DateTime.Today;
            //        List<string> values = new List<string>();
            //        List<aAlarmHis> aAlarmsHis = _DBContext.aAlarmHis.ToList();
            //        List<string> lstHisMonth = aAlarmsHis.Select(x => x.AlarmOnTime.Substring(0, 6)).Distinct().ToList();

            //        foreach (string Date in lstHisMonth)
            //        {
            //            if (Date == now.ToString("yyyyMM") || Date == now.AddMonths(-1).ToString("yyyyMM"))
            //                continue;

            //            List<aAlarmHis> AlarmsHis = _DBContext.aAlarmHis.Where(x => x.AlarmOnTime.Substring(0, 6) == Date).ToList();
            //            string deletesql = $"delete from aAlarmHis where SUBSTRING(AlarmOnTime, 1, 6) = '{Date}'";

            //            if (AlarmsHis.Count > 0)
            //            {
            //                string sPath = _configuration.GetSection("MyConfig")["CSVPath"] + $"\\{Date}.csv";
            //                ExportToCSV(sPath, AlarmsHis);
            //                int result = _DBContext.Database.ExecuteSqlRaw(deletesql);
            //                Log.Information($"歷史警報資料已存入文字檔! [檔案路徑] : {sPath}; [刪除筆數] : {result}");
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //}
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        /// <summary>
        /// CSV Generator
        /// </summary>
        /// <param name="genColumn">output data property name</param>
        /// <param name="FilePath">target CSV path</param>
        /// <param name="data"> List of T</param>
        private void ExportToCSV<T>(string FilePath, List<T> data)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string sDirectory = Path.GetDirectoryName(FilePath);
                string finalPath = Path.Combine(FilePath);
                string header = string.Empty;
                PropertyInfo[] info = typeof(T).GetProperties();

                if (!Directory.Exists(sDirectory))
                    Directory.CreateDirectory(sDirectory);

                if (!File.Exists(finalPath))
                {
                    FileStream file = File.Create(finalPath);
                    file.Close();
                    foreach (PropertyInfo prop in typeof(T).GetProperties())
                    {
                        header += prop.Name + ",";
                    }

                    header = header.Substring(0, header.Length - 2);
                    sb.AppendLine(header);
                    TextWriter sw = new StreamWriter(finalPath, true);
                    sw.Write(sb.ToString());
                    sw.Close();
                }

                foreach (T obj in data)
                {
                    sb = new StringBuilder();
                    string line = string.Empty;
                    foreach (PropertyInfo prop in info)
                    {
                        line += prop.GetValue(obj, null) + ",";
                    }

                    line = line.Substring(0, line.Length - 2);
                    sb.AppendLine(line);
                    TextWriter sw = new StreamWriter(finalPath, true);
                    sw.Write(sb.ToString());
                    sw.Close();
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}