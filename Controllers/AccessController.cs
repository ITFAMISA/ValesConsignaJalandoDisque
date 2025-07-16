using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using VoucherCapture.ViewModel;

namespace VoucherCapture.Controllers
{
    public class AccessController : Controller
    {
        private readonly string connectionStringSQL;
        public AccessController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Message_Access"] = HomeController.ShowAlert("danger", "Favor de ingresar todos los datos");
                return RedirectToAction("Index");
            }
            string message, color;
            message = color = "";
            var lstUser = new List<User_ViewModel>();
            try
            {
                using (var conn = new SqlConnection(connectionStringSQL))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SystemAdmon.sp_Login", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                    cmd.Parameters.Add("@email", SqlDbType.VarChar).Value = email;
                    cmd.Parameters.Add("@password", SqlDbType.VarChar).Value = password;

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            if (!string.IsNullOrEmpty(rd["empNumber"].ToString()))
                            {
                                lstUser.Add(new User_ViewModel()
                                {
                                    EmpNumber = rd["empNumber"].ToString(),
                                Name = rd["user"].ToString(),
                                Permission = rd["permission"].ToString(),
                                MatPrima = rd["MatPrima"].ToString()
                            });
                            }
                            else
                            {
                                lstUser = null;
                                message = rd["message"].ToString();
                                color = rd["color"].ToString();
                            }
                        }
                    }
                    conn.Close();
                }
                if (lstUser != null)
                {                   
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, lstUser[0].Name),
                        new Claim("empNumber", lstUser[0].EmpNumber),
                       new Claim("canAccessMateriaPrima", lstUser[0].MatPrima.ToString())
                    };
                    foreach(var item in lstUser)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, item.Permission));
                    }
                    var claimsIndetity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIndetity));
                    return RedirectToAction("Index", "Voucher");
                }
                TempData["Message_Access"] = HomeController.ShowAlert(color, message);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Message_Access"] = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde. \r\nError: " + ex.ToString());
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Message_Access"] = HomeController.ShowAlert("success", "Sesión finalizada");
            return RedirectToAction("Index");
        }
    }
}
