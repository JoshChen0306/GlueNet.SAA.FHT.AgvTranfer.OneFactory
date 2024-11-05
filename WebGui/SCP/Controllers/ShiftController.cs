using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize(Roles = "1")]
    public class ShiftController : Controller
    {
        private readonly agvDB_1400004Context _DBContext;

        public ShiftController(agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
        }
        
        public IActionResult Index()
        {
            var query = _DBContext.pShift;
            return View(query);
        }

        [HttpPost]
        public IActionResult DataChange([FromBody] DataChange data)
        {
            var insertdata = data.insertdata;
            var updatedata = data.updatedata;
            var deletedata = data.deletedata;

            if (insertdata.Count>0)
            {
                foreach (var item in insertdata)
                {
                    pShift? shift = JsonConvert.DeserializeObject<pShift>(item.Value.ToString());
                    InsertShift(shift);

                }
            }


            if (updatedata.Count>0)
            {
                foreach (var item in updatedata)
                {
                    pShift? shift = JsonConvert.DeserializeObject<pShift>(item.Value.ToString());
                    UpdateShift(shift);

                }
            }

            if (deletedata != null)
            {

                DeleteShift(deletedata);
            }

            return Ok();
        }

        private void InsertShift(pShift shift)
        {

            try
            {
                string sql = "INSERT INTO pShift (ShiftCode, ShiftName, BeginDateTime, EndDateTime,EffectDateTime, ModifiedTime) VALUES ({0}, {1}, {2}, {3},{4},{5})";
                _DBContext.Database.ExecuteSqlRaw(sql, shift.ShiftCode, shift.ShiftName, shift.BeginDateTime, shift.EndDateTime,shift.EffectDateTime,DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            catch (Exception ex)
            {

            }
        }

        private void UpdateShift(pShift shift)
        {
            var currentTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            try
            {
                _DBContext.pShift
                    .Where(s => s.ShiftCode == shift.ShiftCode)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(s => s.ShiftName, shift.ShiftName)
                        .SetProperty(s => s.BeginDateTime, shift.BeginDateTime)
                        .SetProperty(s => s.EndDateTime, shift.EndDateTime)
                        .SetProperty(s => s.ModifiedTime, currentTime));
            }
            catch (Exception ex)
            {

            }
        }

        private void DeleteShift(List<string> deletedata)
        {
            try
            {
                var query = _DBContext.pShift.Where(s => deletedata.Contains(s.ShiftCode));
                query.ExecuteDelete();

            }
            catch (Exception ex)
            {

            }
        }
    }
}
