using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using Microsoft.AspNetCore.Authorization;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class WorkerController : Controller
    {
        private readonly string connectionStringSQL;
        public WorkerController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        [HttpPost]
        public JsonResult GetWorkNameByEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Json(new { name = "", role = "", department = "", empNumber = "" });
            }
            string name, role, department, empNumber;
            name = role = department = empNumber = "";
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("HumanResource.sp_Worker_SelNameByEmailFromUser", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@email", SqlDbType.VarChar).Value = email;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    name = rd["name"].ToString();
                    department = rd["department"].ToString();
                    role = rd["role"].ToString();
                    empNumber = rd["empNumber"].ToString();
                }
                cnn.Close();
            }
            return Json(new { name = name, role = role, department = department, empNumber = empNumber });
        }

        [HttpPost]
        public JsonResult GetWorkerName(string empNumber)
        {
            if (string.IsNullOrEmpty(empNumber))
            {
                return Json("");
            }
            string name = "";
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("HumanResource.sp_Worker_SelNameByEmpNumber", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = empNumber;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    name = rd["name"].ToString();
                }
                cnn.Close();
            }
            return Json(name);
        }
    }
}
