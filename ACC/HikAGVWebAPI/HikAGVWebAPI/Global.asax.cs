using System;
using System.Web;
using System.Web.Http;
using HikAGVWebAPI.App_Start;

namespace HikAGVWebAPI
{
    public class WebApiApplication : HttpApplication
    {
        private static Dispatch dispatch;
        private static StockSensorLinkManager stockSensorLink;

        private static readonly string ConfigFileName = string.Format("{0}\\Config\\FHtSetting.config",
            string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : AppDomain.CurrentDomain.RelativeSearchPath);

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            dispatch = new Dispatch();

            // 庫位 SENSOR 監控（設定 Enable=true 時才會啟動；未設定 / 關閉時回傳 null）
            stockSensorLink = StockSensorLinkBootstrap.Create(ConfigFileName);

            // 中光電儲位 API 介接（Enable=true 時才會啟動）；Sensor 清空中光電站點時通知 release
            CgdWcsBootstrap.Start(ConfigFileName);
            if (stockSensorLink != null)
            {
                stockSensorLink.StockCleared += stationNo => CgdWcsBootstrap.Current?.EnqueueRelease(stationNo);
            }

            stockSensorLink?.Start();
        }

        protected void Application_End()
        {
            // 在应用程序结束时，确保线程停止
            dispatch.Stop();
            stockSensorLink?.Stop();
            CgdWcsBootstrap.Stop();
        }
    }
}