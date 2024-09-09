using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCP.Models;

namespace SCP.ViewComponents
{
    public class SidebarAlarm:ViewComponent
    {
        private readonly  agvDB_1400004Context _DBContext;
        public SidebarAlarm(agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            //List<aAlarm> Alarm = await _DBContext.aAlarm.OrderByDescending(a=>a.AlarmOnTime).ToListAsync();
            

            return View();
        }
    }
}
