using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using VoucherCapture.ViewModel;
using Microsoft.AspNetCore.Authorization;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class CostCenterUserController : Controller
    {
        private readonly string connectionStringSQL;
        public CostCenterUserController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Delete(int idCostCenter, string empNumber)
        {
            if (User.IsInRole("Administrador") || User.IsInRole("CentroCosto"))
            {
                var ccuModel = new CostCenter_ViewModel();
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_SelByIdCcAndEmpNumber", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = idCostCenter;
                    cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = empNumber;
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        ccuModel.Description = Convert.ToString(rd["description"]);
                        ccuModel.IdCostCenter = Convert.ToInt32(rd["idCostCenter"]);
                        ccuModel.EmpNumber = Convert.ToString(rd["empNumber"]);
                        ccuModel.User = Convert.ToString(rd["user"]);
                    }
                    cnn.Close();
                }
                return PartialView("_Delete", ccuModel);
            }
            return Json(HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto"));
        }

        [HttpPost]
        public IActionResult Delete(CostCenter_ViewModel ccuVM)
        {
            if (User.IsInRole("Administrador") || User.IsInRole("CentroCosto"))
            {
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_Del", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = ccuVM.IdCostCenter;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = ccuVM.EmpNumber;
                cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    TempData["Message_CCU"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                }
                cnn.Close();
            }
                return RedirectToAction("Index", "CostCenter");
            }
            TempData["Message_CCU"] = HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto");
            return RedirectToAction("Index", "CostCenter");
        }

        public IActionResult InfoModal(int idCostCenter)
        {
            var lstCCVM = new List<CostCenter_ViewModel>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_SelByIdCcAndEmpNumber", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = idCostCenter;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = DBNull.Value;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstCCVM.Add(new CostCenter_ViewModel()
                        {
                            User = rd["user"].ToString(),
                            AddedDate = rd["addedDate"].ToString()
                        });
                    }
                }
                cnn.Close();
            }
                ViewBag.CostCenterModel = CostCenterController.GetOne(connectionStringSQL, idCostCenter);
            return PartialView("_CCUInfo", lstCCVM);
        }

        [HttpPost]
        public IActionResult GetData(string user, string costCenter, int idCostCenterStatus, int page)
        {
            if (User.IsInRole("Operacional") || User.IsInRole("Firma"))
            {
                return Json(HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto"));
            }
            int offset = (page - 1) * 18;
            var lstCCVM = new List<CostCenter_ViewModel>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_Sel", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = string.IsNullOrEmpty(user) ? DBNull.Value : user;
                cmd.Parameters.Add("@costCenter", SqlDbType.VarChar).Value = string.IsNullOrEmpty(costCenter) ? DBNull.Value : costCenter;
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                cmd.Parameters.Add("@idCostCenterStatus", SqlDbType.Int).Value = idCostCenterStatus == 0 ? DBNull.Value : idCostCenterStatus;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstCCVM.Add(new CostCenter_ViewModel()
                        {
                            IdCostCenter = Convert.ToInt32(rd["idCostCenter"]),
                            Description = Convert.ToString(rd["description"]),
                            Status = Convert.ToString(rd["status"]),
                            IdCostCenterStatus = Convert.ToInt32(rd["idCostCenterStatus"]),
                            EmpNumber = Convert.ToString(rd["empNumber"]),
                            User = rd["user"].ToString(),
                            AddedDate = rd["addedDate"].ToString()
                        });
                    }
                }
                cnn.Close();
            }
            int pages = CountPages(user, costCenter, idCostCenterStatus);
            var result = HomeController.ControlPages(page, pages);
            ViewBag.ActualPage = page;
            ViewBag.MinPage = result.minPage;
            ViewBag.MaxPage = result.maxPage;
            ViewBag.Pages = pages;
            return PartialView("_PVCostCenterUserTbl", lstCCVM);
        }

        private int CountPages(string user, string costCenter, int idCostCenterStatus)
        {
            int pages = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_Count", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = string.IsNullOrEmpty(user) ? DBNull.Value : user;
                cmd.Parameters.Add("@costCenter", SqlDbType.VarChar).Value = string.IsNullOrEmpty(costCenter) ? DBNull.Value : costCenter;
                cmd.Parameters.Add("@idCostCenterStatus", SqlDbType.Int).Value = idCostCenterStatus == 0 ? DBNull.Value : idCostCenterStatus;
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


    }
}
