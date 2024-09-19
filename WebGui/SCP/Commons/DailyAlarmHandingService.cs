using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SCP.Models;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SCP.Commons
{
    public class DailyAlarmHandingService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private Timer _timer;

        public DailyAlarmHandingService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            DateTime now = DateTime.Now;
            DateTime nextRunTime = new DateTime(now.Year, now.Month, now.Day,0, 0, 0);
            if (now > nextRunTime)
            {
                nextRunTime = nextRunTime.AddDays(1); // 如果當前時間已經過了午夜零點，則設置為第二天零點
            }
            TimeSpan delay = nextRunTime - now;
            _timer = new Timer(ExecuteSql, null, delay, TimeSpan.FromDays(1)); // 每天執行一次

            return Task.CompletedTask;
        }

        private void ExecuteSql(object? state)
        {
            //int result = 0;
            //int deleteresult = 0;
            //using (IServiceScope scope = _serviceProvider.CreateScope())
            //{
            //    atcDB_SCPContext _DBContext = scope.ServiceProvider.GetRequiredService<atcDB_SCPContext>();

            //    DateTime now = DateTime.Today;
            //    List<string> values = new List<string>();
            //    List<aAlarm> aAlarms = _DBContext.aAlarm.ToList();

            //    string sql = "Insert into aAlarmHis (AlarmOnTime,ObjStation,EquipmentType,AlarmType,AlarmCode,AlarmText,OnTimeSend,AlarmOffTime,HandleWay,HandleMan,HandleDesc,OffTimeSend) values ";
            //    string deletesql = $"delete from aAlarm where AlarmOffTime <'{now.ToString("yyyyMMdd")}'";
               
            //    foreach (var item in aAlarms)
            //    {
            //        //var targetDate = DateTime.ParseExact(item.AlarmOffTime, "yyyyMMddHHmmssffffff", null);
            //        if (!string.IsNullOrEmpty(item.AlarmOffTime))
            //        {
            //            if (DateTime.TryParseExact(item.AlarmOffTime, "yyyyMMddHHmmssffffff", null, DateTimeStyles.None, out DateTime parsedDateTime) && parsedDateTime < now)
            //            {
            //                values.Add($"('{item.AlarmOnTime}','{item.ObjStation}','{item.EquipmentType}','{item.AlarmType}','{item.AlarmCode}','{item.AlarmText}'," +
            //                    $"'{item.OnTimeSend}','{item.AlarmOffTime}','{item.HandleWay}','{item.HandleMan}','{item.HandleDesc}','{item.OffTimeSend}')");
            //            }
            //        }

            //    }

            //    sql += string.Join(", ", values);
            //    try
            //    {
            //        result = _DBContext.Database.ExecuteSqlRaw(sql);                 
            //        if (result > 0)
            //        {
            //            Log.Information("警報資料已備份至歷史區");
            //            deleteresult = _DBContext.Database.ExecuteSqlRaw(deletesql);
            //            if (deleteresult > 0)
            //                Log.Information("aAlarm警報表資料已清除");
            //        }
            //    }
            //    catch (Exception ex)
            //    {

            //    }
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
    }
}
