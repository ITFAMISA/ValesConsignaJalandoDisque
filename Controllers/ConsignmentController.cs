using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using VoucherCapture.Models;
using VoucherCapture.ViewModel;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class ConsignmentController : Controller
    {
        private readonly string connectionStringSQL;

        public ConsignmentController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Index()
        {
            if (User.IsInRole("Administrador") || User.IsInRole("Almacen"))
            {
                return View();
            }
            TempData["Message_Consignment"] = HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder.");
            return RedirectToAction("Index", "Voucher");
        }

        [HttpPost]
        public IActionResult GetData(string microsipKey, string description, int consignmentStatus, int page)
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen"))
            {
                return Json(HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder."));
            }

            int offset = (page - 1) * 20;
            var lstConsignment = new List<ConsignmentItem_ViewModel>();

            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_ConsignmentItems_Sel", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@microsipKey", SqlDbType.VarChar).Value = string.IsNullOrEmpty(microsipKey) ? DBNull.Value : microsipKey.Trim();
                cmd.Parameters.Add("@description", SqlDbType.VarChar).Value = string.IsNullOrEmpty(description) ? DBNull.Value : description.Trim();
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstConsignment.Add(new ConsignmentItem_ViewModel()
                        {
                            IdSupply = Convert.ToInt32(rd["idSupply"]),
                            MicrosipKey = Convert.ToString(rd["microsipKey"]),
                            Description = Convert.ToString(rd["description"]),
                            UnitType = Convert.ToString(rd["unitType"]),
                            CurrentStock = Convert.ToDecimal(rd["currentStock"]),
                            MinimumStock = Convert.ToDecimal(rd["minimumStock"]),
                            MaximumStock = Convert.ToDecimal(rd["maximumStock"]),
                            IsConsignment = Convert.ToBoolean(rd["isConsignment"]),
                            LastUpdate = Convert.ToString(rd["lastUpdate"]),
                            SupplierName = Convert.ToString(rd["supplierName"])
                        });
                    }
                }
                cnn.Close();
            }

            int pages = CountPages(microsipKey, description, consignmentStatus);
            var result = HomeController.ControlPages(page, pages);
            ViewBag.ActualPage = page;
            ViewBag.MinPage = result.minPage;
            ViewBag.MaxPage = result.maxPage;
            ViewBag.Pages = pages;

            return PartialView("_PVConsignmentTable", lstConsignment);
        }

        private int CountPages(string microsipKey, string description, int consignmentStatus)
        {
            int pages = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_ConsignmentItems_Count", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@microsipKey", SqlDbType.VarChar).Value = string.IsNullOrEmpty(microsipKey) ? DBNull.Value : microsipKey.Trim();
                cmd.Parameters.Add("@description", SqlDbType.VarChar).Value = string.IsNullOrEmpty(description) ? DBNull.Value : description.Trim();

                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    decimal temp = (Convert.ToDecimal(rd["conteo"]) / 20);
                    pages = Convert.ToInt32(Math.Ceiling(temp));
                }
                cnn.Close();
            }
            return pages;
        }

        [HttpPost]
        public IActionResult UpdateStock(int idSupply, decimal newStock)
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen"))
            {
                return Json(new { success = false, message = "No tiene permisos para realizar esta acción" });
            }

            try
            {
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_ConsignmentItems_UpdateStock", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;
                    cmd.Parameters.Add("@newStock", SqlDbType.Decimal).Value = newStock;
                    cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();

                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        var success = Convert.ToBoolean(rd["success"]);
                        var message = Convert.ToString(rd["message"]);
                        return Json(new { success = success, message = message });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ToggleConsignment(int idSupply, bool isConsignment)
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen"))
            {
                return Json(new { success = false, message = "No tiene permisos para realizar esta acción" });
            }

            try
            {
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_ConsignmentItems_Toggle", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;
                    cmd.Parameters.Add("@isConsignment", SqlDbType.Bit).Value = isConsignment;
                    cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();

                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        var success = Convert.ToBoolean(rd["success"]);
                        var message = Convert.ToString(rd["message"]);
                        return Json(new { success = success, message = message });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SetMinMaxStock(int idSupply, decimal minStock, decimal maxStock)
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen"))
            {
                return Json(new { success = false, message = "No tiene permisos para realizar esta acción" });
            }

            if (minStock < 0 || maxStock < 0 || minStock > maxStock)
            {
                return Json(new { success = false, message = "Los valores de stock mínimo y máximo no son válidos" });
            }

            try
            {
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_ConsignmentItems_SetMinMax", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;
                    cmd.Parameters.Add("@minStock", SqlDbType.Decimal).Value = minStock;
                    cmd.Parameters.Add("@maxStock", SqlDbType.Decimal).Value = maxStock;
                    cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();

                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        var success = Convert.ToBoolean(rd["success"]);
                        var message = Convert.ToString(rd["message"]);
                        return Json(new { success = success, message = message });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public IActionResult GetConsignmentItems()
        {
            var lstItems = new List<object>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_ConsignmentItems_ForList", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstItems.Add(new
                        {
                            idSupply = Convert.ToInt32(rd["idSupply"]),
                            description = rd["description"].ToString(),
                            unitType = rd["unitType"].ToString(),
                            qtyTotal = Convert.ToDecimal(rd["currentStock"]),
                            microsipkey = rd["microsipKey"].ToString(),
                            isConsignment = true
                        });
                    }
                }
                cnn.Close();
            }
            return Json(lstItems);
        }
    }
}