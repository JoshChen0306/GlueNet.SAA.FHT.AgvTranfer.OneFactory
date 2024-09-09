using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using SCP.Models;

namespace SCP.Controllers
{
    public class MainFunController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public MainFunController(ILogger<HomeController> logger, agvDB_1400004Context DBContext)
        {
            _logger = logger;
            _DBContext = DBContext;
        }

        public IActionResult Index()
        {
            //Get Group
            var query = _DBContext.pFunction.OrderBy(x => x.FunctionNo);
            return View(query);
        }

        [HttpPost]
        public IActionResult DataChange([FromBody] DataChange data)
        {
            var insertdata = data.insertdata;
            var updatedata = data.updatedata;
            var deletedata = data.deletedata;

            if (insertdata.Count > 0)
            {
                foreach (var item in insertdata)
                {
                    pFunction? function = JsonConvert.DeserializeObject<pFunction>(item.Value.ToString());
                    InsertFunction(function);

                }
            }

            if (updatedata.Count > 0)
            {
                foreach (var item in updatedata)
                {
                    int key = int.Parse(item.Key);
                    pFunction? function = JsonConvert.DeserializeObject<pFunction>(item.Value.ToString());
                    UpdateFunction(key,function);

                }
            }

            if (deletedata != null)
            {

                DeleteFunction(deletedata);
            }

            return Ok();
        }

        private void InsertFunction(pFunction function)
        {

            try
            {
                string sql = "Insert into pFunction (ColumnNo, RowNo,FunctionNo, FunctionType,FunctionEnglishName,FunctionChineseName, ControlFlag,WebUrl,WebIcon)" +
                    " VALUES ({0}, {1}, {2}, {3},{4}, {5}, {6},{7}, {8})";
                _DBContext.Database.ExecuteSqlRaw(sql, function.ColumnNo, function.RowNo, function.FunctionNo, function.FunctionType, function.FunctionEnglishName,
                    function.FunctionChineseName, function.ControlFlag, function.WebUrl, function.WebIcon);
            }
            catch (Exception ex)
            {

            }
        }
        private void UpdateFunction(int key,pFunction function)
        {
            try
            {
                _DBContext.pFunction
                    .Where(f => f.FunctionNo == key)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(f => f.ColumnNo, function.ColumnNo)
                        .SetProperty(f => f.RowNo, function.RowNo)
                        .SetProperty(f => f.FunctionNo, function.FunctionNo)
                        .SetProperty(f => f.FunctionType, function.FunctionType)
                        .SetProperty(f => f.FunctionEnglishName, function.FunctionEnglishName)
                        .SetProperty(f => f.FunctionChineseName, function.FunctionChineseName)
                        .SetProperty(f => f.ControlFlag, function.ControlFlag)
                        .SetProperty(f => f.WebUrl, function.WebUrl)
                        .SetProperty(f => f.WebIcon, function.WebIcon));
            }
            catch (Exception ex)
            {

            }
        }

        private void DeleteFunction(List<string> deletedata)
        {
            try
            {
                var query = _DBContext.pFunction.Where(f => deletedata.Contains(f.FunctionNo.ToString()));
                query.ExecuteDelete();

            }
            catch (Exception ex)
            {

            }
        }

    }
}