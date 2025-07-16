using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using VoucherCapture.Models;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using VoucherCapture.ViewModel;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class CostCenterController : Controller
    {
        private readonly string connectionStringSQL;
        public CostCenterController(IConfiguration config) {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Index()
        {
            ViewBag.CostCenterStatus = CostCenterStatusList();
            ViewBag.Customers = CustomersList();
            return View();
        }

        public IActionResult Create()
        {
            if (User.IsInRole("Administrador") || User.IsInRole("CentroCosto"))
            {
                return PartialView("_Create");
            }
            return Json(HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto"));
        }

        [HttpPost]
        public IActionResult Create(CostCenterUser_Model ccuModel)
        {
            if (User.IsInRole("Administrador") || User.IsInRole("CentroCosto"))
            {
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_Ins", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = ccuModel.IdCostCenter;
                    cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = ccuModel.EmpNumber;
                    cmd.Parameters.Add("@createdBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        TempData["Message_CCU"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                    }
                    cnn.Close();
                }
                return Json(new { RedirectUrl = Url.Action("Index", "CostCenter") });
            }
            TempData["Message_CCU"] = HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto");
            return RedirectToAction("Index", "CostCenter");
        }

        public IActionResult Update(int idCostCenter)
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("CentroCosto"))
            {
            return Json(HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto"));

            }
            ViewBag.CostCenterStatus = CostCenterStatusList();
            ViewBag.Customers = CustomersList();
            var costCenterModel = new CostCenter_ViewModel();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenter_SelByIdCostCenter", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = idCostCenter;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    costCenterModel.IdCostCenter = Convert.ToInt32(rd["idCostCenter"]);
                    costCenterModel.Description = Convert.ToString(rd["description"]);
                    costCenterModel.IdCostCenterStatus = Convert.ToInt32(rd["idCostCenterStatus"]);
                    costCenterModel.Status = Convert.ToString(rd["status"]);
                    costCenterModel.ExpiredDate = Convert.ToString(rd["expiredDate"]) == "-" ? null : Convert.ToString(rd["expiredDate"]);
                    costCenterModel.Customer = Convert.ToString(rd["customer"]);
                    costCenterModel.IdCustomer = rd["idCustomer"] == DBNull.Value ? 0 : Convert.ToInt32(rd["idCustomer"]);
                }
                cnn.Close();
            }
            return PartialView("_Update", costCenterModel);
        }

        [HttpPost]
        public IActionResult Update(CostCenter_Model costCenterModel)
        {
            if (User.IsInRole("Administrador") || User.IsInRole("CentroCosto"))
            {
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_CostCenter_Upd", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = costCenterModel.IdCostCenter;
                    cmd.Parameters.Add("@idCostCenterStatus", SqlDbType.Int).Value = costCenterModel.IdCostCenterStatus;
                    cmd.Parameters.Add("@expiredDate", SqlDbType.Date).Value = string.IsNullOrEmpty(costCenterModel.ExpiredDate) ? DBNull.Value : costCenterModel.ExpiredDate;
                    cmd.Parameters.Add("@idCustomer", SqlDbType.Int).Value = costCenterModel.IdCustomer == 0 ? DBNull.Value : costCenterModel.IdCustomer;
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        TempData["Message_CCU"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                    }
                    cnn.Close();
                }
                return RedirectToAction("Index");
            }
            TempData["Message_CCU"] = HomeController.ShowAlert("danger", "No cuenta con los permisos suficientes para esto");
            return RedirectToAction("Index", "CostCenter");  
        }

        public static CostCenter_ViewModel GetOne(string connection, int idCostCenter)
        {
            var cCVM = new CostCenter_ViewModel();
            using (var cnn = new SqlConnection(connection))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenter_SelByIdCostCenter", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idCostCenter", SqlDbType.Int).Value = idCostCenter;
                string expiredDate = "-";
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    if(rd["expiredDate"].ToString().Length > 2)
                    {
                        string[] date = rd["expiredDate"].ToString().Split('-');
                         expiredDate = date[2] + "-" + date[1] + "-" + date[0];
                    }
                    cCVM.IdCostCenter = Convert.ToInt32(rd["idCostCenter"]);
                    cCVM.Description = Convert.ToString(rd["description"]);
                    cCVM.Status = Convert.ToString(rd["status"]);
                    cCVM.ExpiredDate = expiredDate;
                    cCVM.IdCostCenterStatus = Convert.ToInt32(rd["idCostCenterStatus"]);
                    cCVM.CreatedDate = Convert.ToString(rd["createdDate"]);
                }
                cnn.Close();
            }
            return cCVM;
        }

        [HttpPost]
        public IActionResult GetData(string costCenter, int idCostCenterStatus, int idCustomer, int page)
        {
            int offset = (page - 1) * 18;
            var lstCostCenterVM = new List<CostCenter_ViewModel>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenter_Sel", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@costCenter", SqlDbType.VarChar).Value = string.IsNullOrEmpty(costCenter) ? DBNull.Value : costCenter;
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                cmd.Parameters.Add("@idCostCenterStatus", SqlDbType.Int).Value = idCostCenterStatus == 0 ? DBNull.Value : idCostCenterStatus;
                cmd.Parameters.Add("@idCustomer", SqlDbType.Int).Value = idCustomer == 0 ? DBNull.Value : idCustomer;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = User.IsInRole("Operacional") ? User.FindFirst("empNumber").Value.ToString() : DBNull.Value;                
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstCostCenterVM.Add(new CostCenter_ViewModel()
                        {
                            IdCostCenter = Convert.ToInt32(rd["idCostCenter"]),
                            Description = Convert.ToString(rd["description"]),
                            MicrosipKey = Convert.ToString(rd["microsipKey"]),
                            CreatedDate = Convert.ToString(rd["createdDate"]),
                            ExpiredDate = Convert.ToString(rd["expiredDate"]),
                            Status = Convert.ToString(rd["status"]),
                            IdCostCenterStatus = Convert.ToInt32(rd["idCostCenterStatus"])
                        });
                    }
                }
                cnn.Close();
            }
            int pages = CountPages(costCenter, idCostCenterStatus, idCustomer);
            var result = HomeController.ControlPages(page, pages);
            ViewBag.ActualPage = page;
            ViewBag.MinPage = result.minPage;
            ViewBag.MaxPage = result.maxPage;
            ViewBag.Pages = pages;
            return PartialView("_PVCostCenterTbl", lstCostCenterVM);
        }

        private int CountPages(string costCenter, int idCostCenterStatus, int idCustomer)
        {
            int pages = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenter_Count", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@costCenter", SqlDbType.VarChar).Value = string.IsNullOrEmpty(costCenter) ? DBNull.Value : costCenter;
                cmd.Parameters.Add("@idCostCenterStatus", SqlDbType.Int).Value = idCostCenterStatus == 0 ? DBNull.Value : idCostCenterStatus;
                cmd.Parameters.Add("@idCustomer", SqlDbType.Int).Value = idCustomer == 0 ? DBNull.Value : idCustomer;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = User.IsInRole("Operacional") ? User.FindFirst("empNumber").Value.ToString() : DBNull.Value;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    decimal temp = (Convert.ToDecimal(rd["conteo"]) / 18);
                    pages = Convert.ToInt32(Math.Ceiling(temp));
                }
                cnn.Close();
            }
            return pages;
        }

        private List<SelectListItem> CustomersList()
        {
            var items = new List<SelectListItem>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from Sales.Customer where status = 1 order by name", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        items.Add(new SelectListItem()
                        {
                            Value = Convert.ToString(rd["idCustomer"]),
                            Text = Convert.ToString(rd["name"])
                        });
                    }
                }
                    cnn.Close();
            }
            items.Insert(0, new SelectListItem()
            {
                Text = "-- Seleccionar cliente --",
                Value = "0",
                Selected = true
            });
            return items;
        }

        private List<SelectListItem> CostCenterStatusList()
        {
            var items = new List<SelectListItem>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * From VoucherRequest.CostCenterStatus", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        items.Add(new SelectListItem()
                        {
                            Value = Convert.ToString(rd["idCostCenterStatus"]),
                            Text = Convert.ToString(rd["description"])
                        });
                    }
                }
                cnn.Close();
            }
            items.Insert(0, new SelectListItem()
            {
                Text = "-- Seleccionar estatus --",
                Value = "0",
                Selected = true
            });
            return items;
        }

        public List<CostCenter_Model> CostCenterList()
        {
            var lstCc = new List<CostCenter_Model>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_CostCenterUser_SelByEmpNumber", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = User.IsInRole("Operacional") ? User.FindFirst("empNumber").Value.ToString() : DBNull.Value;
                cmd.Parameters.Add("@status", SqlDbType.Bit).Value = User.IsInRole("Operacional") ? 1 : DBNull.Value;
                using (var rd = cmd.ExecuteReader())
                {
                    while(rd.Read()){
                        lstCc.Add(new CostCenter_Model(){
                            IdCostCenter = Convert.ToInt32(rd["idCostCenter"]),
                            Description = Convert.ToString(rd["costCenter"])
                        });
                    }
                }
                cnn.Close();
            }
            return lstCc;
        }
    }
}
