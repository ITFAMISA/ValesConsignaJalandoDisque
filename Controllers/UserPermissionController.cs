using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data.SqlClient;
using System.Data;
using VoucherCapture.Models;
using VoucherCapture.ViewModel;
using Microsoft.AspNetCore.Authorization;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class UserPermissionController : Controller
    {
        private readonly string connectionStringSQL;

        public UserPermissionController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Index()
        {
            if (User.IsInRole("Administrador") || User.IsInRole("Lectura"))
            {
                ViewBag.PermissionList = ListPermissions();
                return View();
            }
            TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder.");
            return RedirectToAction("Index", "Voucher");
        }

        public IActionResult Create()
        {
            if (User.IsInRole("Administrador"))
            {
                ViewBag.PermissionList = GetPermissions();
                return PartialView("_Create");
            }
            return Json(HomeController.ShowAlert("danger", "Error: no tiene suficientes permisos."));
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserPermission_ViewModel userModel)
        {
            if (userModel == null)
            {
                TempData["Message_UsserPermission"] = HomeController.ShowAlert("danger", "Sucedió un error, favor de intentarlo más tarde.");
                return RedirectToAction("Index");
            }
            string msg = "";
            if (User.IsInRole("Administrador"))
            {
                try
                {
                    foreach (var item in userModel.Permissions)
                    {
                        using (var cnn = new SqlConnection(connectionStringSQL))
                        {
                            cnn.Open();
                            var cmd = new SqlCommand("SystemAdmon.sp_UserPermission_Ins", cnn)
                            {
                                CommandType = CommandType.StoredProcedure
                            };
                            cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = userModel.EmpNumber;
                            cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                            cmd.Parameters.Add("@idPermission", SqlDbType.Int).Value = item.IdPermission;
                            cmd.Parameters.Add("@createdBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                            if (userModel.matPrimOtros == 1)
                            {
                                cmd.Parameters.Add("@matPrimaOtros", SqlDbType.Int).Value = 1;
                            }
                            else
                            {
                                cmd.Parameters.Add("@matPrimaOtros", SqlDbType.Int).Value = 0;
                            }
                            

                            using (var rd = cmd.ExecuteReader())
                            {
                                rd.Read();
                                msg = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                            }
                            cnn.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    msg = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde. <br>" + ex.ToString());
                }
            }
            else
            {
                msg = HomeController.ShowAlert("danger", "Error: no tiene suficientes permisos.");
            }
            TempData["Message_UsserPermission"] = msg;
            return Json(new { RedirectUrl = Url.Action("Index", "UserPermission") });
        }

        public IActionResult Update(string empNumber)
        {
            if (User.IsInRole("Administrador"))
            {
                ViewBag.PermissionList = GetPermissions();
                return PartialView("_Update", SelectOne(empNumber));
            }
            return Json(HomeController.ShowAlert("danger", "Error: no tiene suficientes permisos."));
        }

        [HttpPost]
        public IActionResult Update(UserPermission_ViewModel userModel)
        {
            if (userModel == null)
            {
                TempData["Message_UsserPermission"] = HomeController.ShowAlert("danger", "Sucedió un error, favor de intentarlo más tarde.");
                return RedirectToAction("Index");
            }
            string msg = "";
            if (User.IsInRole("Administrador"))
            {
                try
                {
                    foreach (var item in userModel.Permissions)
                    {
                        using (var cnn = new SqlConnection(connectionStringSQL))
                        {
                            cnn.Open();
                            var cmd = new SqlCommand("SystemAdmon.sp_UserPermission_Upd", cnn)
                            {
                                CommandType = CommandType.StoredProcedure
                            };
                            cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = userModel.EmpNumber;
                            cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                            cmd.Parameters.Add("@idPermission", SqlDbType.Int).Value = item.IdPermission;
                            cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                            cmd.Parameters.Add("@status", SqlDbType.Bit).Value = item.Status;                            
                            if (item.IdPermission == 2)
                            {
                                if (userModel.matPrimOtros == 1)
                                {
                                    cmd.Parameters.Add("@matPrimaOtros", SqlDbType.Int).Value = 1;
                                }
                                else
                                {
                                    cmd.Parameters.Add("@matPrimaOtros", SqlDbType.Int).Value = 0;
                                }
                            }
                            else
                            {
                                cmd.Parameters.Add("@matPrimaOtros", SqlDbType.Int).Value = 0;
                            }


                            using (var rd = cmd.ExecuteReader())
                            {
                                rd.Read();
                                msg = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                                if (rd["color"].ToString() == "danger")
                                {
                                    cnn.Close();
                                    break;
                                }
                            }
                            cnn.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    msg = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde. <br>" + ex.ToString());
                }
            }
            else
            {
                msg = HomeController.ShowAlert("danger", "Error: no tiene suficientes permisos.");
            }
            TempData["Message_UsserPermission"] = msg;
            return Json(new { RedirectUrl = Url.Action("Index", "UserPermission") });
        }

        public IActionResult Delete(string empNumber)
        {
            if (!User.IsInRole("Administrador"))
            {
                return Json(HomeController.ShowAlert("danger", "Error: no tiene suficientes permisos."));
            }
            return PartialView("_Delete", SelectOne(empNumber));
        }

        [HttpPost]
        public IActionResult Delete(UserPermission_ViewModel userModel)
        {
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("SystemAdmon.sp_UserPermission_Del", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = userModel.EmpNumber;
                cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@idPermission", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    TempData["Message_UsserPermission"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                }
                cnn.Close();
            }
            return RedirectToAction("Index");
        }

        private UserPermission_ViewModel SelectOne(string empNumber)
        {
            var userModel = new UserPermission_ViewModel();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("SystemAdmon.sp_UserPermission_SelForSys", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = DBNull.Value;
                cmd.Parameters.Add("@idPermission", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@department", SqlDbType.VarChar).Value = DBNull.Value;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = empNumber;
                cmd.Parameters.Add("@status", SqlDbType.Bit).Value = 1;
                cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = 0;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    userModel.EmpNumber = Convert.ToString(rd["empNumber"]);
                    userModel.User = Convert.ToString(rd["user"]);
                    userModel.Department = Convert.ToString(rd["department"]);
                    userModel.Role = Convert.ToString(rd["role"]);
                    userModel.Email = Convert.ToString(rd["email"]);
                    userModel.matPrimOtros = Convert.ToInt32(rd["matPrimOtros"]);
                }
                cnn.Close();
            }
            userModel.Permissions = GetPermissionByUser(userModel.EmpNumber);
            return userModel;
        }

        [HttpPost]
        public IActionResult GetData(string user, int idPermission, string department, int status, int page)
        {
            var lstUsers = new List<UserPermission_ViewModel>();
            int offset = (page - 1) * 12;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("SystemAdmon.sp_UserPermission_SelForSys", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = string.IsNullOrEmpty(user) ? DBNull.Value : user.Trim();
                cmd.Parameters.Add("@idPermission", SqlDbType.Int).Value = idPermission == 0 ? DBNull.Value : idPermission;
                cmd.Parameters.Add("@department", SqlDbType.VarChar).Value = string.IsNullOrEmpty(department) ? DBNull.Value : department.Trim();
                cmd.Parameters.Add("@status", SqlDbType.Bit).Value = status;
                cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = DBNull.Value;
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstUsers.Add(new UserPermission_ViewModel()
                        {
                            EmpNumber = Convert.ToString(rd["empNumber"]),
                            User = Convert.ToString(rd["user"]),
                            Department = Convert.ToString(rd["department"]),
                            Role = Convert.ToString(rd["role"]),
                            Email = Convert.ToString(rd["email"])
                        });
                    }
                }
                cnn.Close();
            }
            foreach (var item in lstUsers)
            {
                item.Permissions = GetPermissionByUser(item.EmpNumber);
            }
            int pages = CountPages(user, idPermission, department, status);
            var result = HomeController.ControlPages(page, pages);
            ViewBag.ActualPage = page;
            ViewBag.MinPage = result.minPage;
            ViewBag.MaxPage = result.maxPage;
            ViewBag.Pages = pages;
            return PartialView("_PVUserPermissionsTbl", lstUsers);
        }

        private int CountPages(string user, int idPermission, string department, int status)
        {
            int pages = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("SystemAdmon.sp_UserPermission_CountForSys", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = string.IsNullOrEmpty(user) ? DBNull.Value : user.Trim();
                cmd.Parameters.Add("@idPermission", SqlDbType.Int).Value = idPermission == 0 ? DBNull.Value : idPermission;
                cmd.Parameters.Add("@department", SqlDbType.VarChar).Value = string.IsNullOrEmpty(department) ? DBNull.Value : department.Trim();
                cmd.Parameters.Add("@status", SqlDbType.Bit).Value = status;
                cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    decimal temp = (Convert.ToDecimal(rd["conteo"]) / 12);
                    pages = Convert.ToInt32(Math.Ceiling(temp));
                }
                cnn.Close();
            }
            return pages;
        }

        private List<Permission_Model> GetPermissionByUser(string empNumber)
        {
            var lstPermission = new List<Permission_Model>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("SystemAdmon.sp_Permission_SelByUser", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = empNumber;
                cmd.Parameters.Add("@idSystem", SqlDbType.Int).Value = 1;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstPermission.Add(new Permission_Model()
                        {
                            IdPermission = Convert.ToInt32(rd["idPermission"]),
                            Name = Convert.ToString(rd["permission"])
                        });
                    }
                }
                cnn.Close();
            }
            return lstPermission;
        }

        private List<Permission_Model> GetPermissions()
        {
            var lstPermission = new List<Permission_Model>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from SystemAdmon.VW_Permission order by name;", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstPermission.Add(new Permission_Model()
                        {
                            IdPermission = Convert.ToInt32(rd["idPermission"]),
                            Name = rd["name"].ToString()
                        });
                    }
                }
                cnn.Close();
            }
            return lstPermission;
        }

        private List<SelectListItem> ListPermissions()
        {
            var items = new List<SelectListItem>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from SystemAdmon.VW_Permission;", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        items.Add(new SelectListItem()
                        {
                            Value = Convert.ToString(rd["idPermission"]),
                            Text = Convert.ToString(rd["name"]),
                            Selected = false
                        });
                    }
                }
                cnn.Close();
            }
            items.Insert(0, new SelectListItem()
            {
                Text = "-- Seleccionar permiso --",
                Value = "0",
                Selected = true
            });
            return items;
        }
    }
}
