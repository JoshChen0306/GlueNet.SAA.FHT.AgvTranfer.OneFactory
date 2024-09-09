using System.Web;
using System.Web.Http;

namespace HikAGVWebAPI
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            Dispatch dispatch = new Dispatch();
        }
    }
}