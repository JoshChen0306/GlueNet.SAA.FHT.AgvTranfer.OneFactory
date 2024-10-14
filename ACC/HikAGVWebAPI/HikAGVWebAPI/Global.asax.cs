using System.Web;
using System.Web.Http;

namespace HikAGVWebAPI
{
    public class WebApiApplication : HttpApplication
    {
        private static Dispatch dispatch;

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            dispatch = new Dispatch();
        }

        protected void Application_End()
        {
            // 在应用程序结束时，确保线程停止
            dispatch.Stop();
        }
    }
}