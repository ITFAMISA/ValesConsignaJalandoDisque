using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using VoucherCapture.ViewModel;
using VoucherCapture.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class VoucherController : Controller
    {
        private readonly string connectionStringSQL;
        public VoucherController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Index()
        {
            ViewBag.Years = YearList();
            return View("Index");
        }

        public IActionResult Create(int voucherType)
        {
            ViewBag.VoucherType = voucherType;
            ViewBag.GroupSupplyLines = StockController.ListGroupSupplyLine(connectionStringSQL, voucherType);
            return View();
        }

        [HttpPost]
        public JsonResult Create(string cc, string empNumber, List<VoucherDetail_Model> lstVD, int rawMaterial)
        {            
            if (string.IsNullOrEmpty(cc) || lstVD.Count == 0)
            {
                return Json(HomeController.ShowAlert("danger", "Ha sucedido un error, Favor de ingresar todos los datos requeridos para el vale."));
            }

            int idVoucher = 0;
            string msg = "";
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Ins", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idCostCenter", SqlDbType.VarChar).Value = string.IsNullOrEmpty(cc) ? DBNull.Value : cc.Trim();
                cmd.Parameters.Add("@requestedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                cmd.Parameters.Add("@pickedUpBy", SqlDbType.VarChar).Value = string.IsNullOrEmpty(empNumber) ? User.FindFirst("empNumber").Value.ToString() : empNumber.Trim();
                cmd.Parameters.Add("@requestedDate", SqlDbType.DateTime).Value = DBNull.Value;
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = CreateVoucherNumber();
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    idVoucher = Convert.ToInt32(rd["idVoucher"]);
                }
                if (idVoucher <= 0)
                {
                    cnn.Close();
                    switch (idVoucher)
                    {
                        case 0: msg = "No se logró insertar correctamente el Vale, favor de intentarlo más tarde. Error 0."; break;
                        case -1: msg = "No se logró insertar correctamente el Vale, favor de intentarlo más tarde. Error -1."; break;
                        case -2: msg = "El trabajador ingresado para recoger los insumos del Vale no se encuentra activo, favor de ingresar otro trabajador"; break;
                    }
                    return Json(new { msg = HomeController.ShowAlert("danger", msg), idVoucher = idVoucher });
                }
                foreach (var item in lstVD)
                {
                    var cmdVD = new SqlCommand("VoucherRequest.sp_VoucherDetail_Ins", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmdVD.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucher;
                    cmdVD.Parameters.Add("@idSupply", SqlDbType.Int).Value = item.IdSupply;
                    cmdVD.Parameters.Add("@qtyRequested", SqlDbType.Decimal).Value = item.QtyRequested;
                    cmdVD.Parameters.Add("@idSupplySurplus", SqlDbType.Int).Value = item.IdSupplySurplus == 0 ? DBNull.Value : item.IdSupplySurplus;
                    using (var rd = cmdVD.ExecuteReader())
                    {
                        rd.Read();
                        msg = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                    }
                }
                cnn.Close();
            }
            return Json(new { redirectUrl = Url.Action("Index", "Voucher"), idVoucher = idVoucher, msg = msg });
        }

        private string CreateVoucherNumber()
        {
            var character = new Dictionary<int, string>()
            {
                {1, "0" }, {2, "1" }, {3, "2" }, {4, "3" }, {5, "4" }, {6, "5" }, {7, "6" }, {8, "7" },
                {9, "8" }, {10, "9" }, {11, "A" }, {12, "B" }, {13, "C" }, {14, "D" }, {15, "E" }, {16, "F" },
                {17, "G" }, {18, "H" }, {19, "I" }, {20, "J" }, {21, "K" }, {22, "L" }, {23, "M" }, {24, "N" },
                {25, "O" }, {26, "P" }, {27, "Q" }, {28, "R" }, {29, "S" }, {30, "T" }, {31, "U" }, {32, "V" },
            };
            string voucherNumberOld, prefix;
            DateTime now = DateTime.Now;
            string day = now.ToString("dd"), month = now.ToString("MM"), year = now.ToString("yy");
            prefix = character[Convert.ToInt32(year)] + character[Convert.ToInt32(month)] + character[Convert.ToInt32(day)];
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_LastVoucherNumb", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@prefix", SqlDbType.VarChar).Value = prefix;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    voucherNumberOld = rd["voucherNumber"].ToString();
                }
                cnn.Close();
            }
            int numb = voucherNumberOld == "Wrong" ? 0 : Convert.ToInt32(voucherNumberOld.Substring(3, 3)) + 1;
            string newNumb = numb.ToString().Length == 3 ? numb.ToString() : numb.ToString().Length == 2 ? "0" + numb.ToString() : "00" + numb.ToString();
            string voucherNumber = prefix + newNumb;
            int res = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_CheckVouchNum", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = voucherNumber;
                using(var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    res = Convert.ToInt32(rd["res"]);
                }
                cnn.Close();
            }
            //if (res == 1)
            //{
            //    CreateVoucherNumber();
            //}
            return voucherNumber;
        }

        public IActionResult Delete(string voucherNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(voucherNumber))
                {
                    return Json(HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde"));

                }
                if (!User.IsInRole("Operacional") && !User.IsInRole("CentroCosto"))
                {
                    return Json(HomeController.ShowAlert("danger", "Error: no tienes los permisos suficientes para esto"));
                }
                var lstVDVM = new List<VoucherDetail_ViewModel>();
                using(var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_SelByVoucher", cnn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = voucherNumber;
                    cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = DBNull.Value;
                    cmd.Parameters.Add("@idVoucherDtl", SqlDbType.Int).Value = DBNull.Value;
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lstVDVM.Add(new VoucherDetail_ViewModel()
                            {
                                Supply = Convert.ToString(rd["supply"])
                            });
                        }
                    }
                        cnn.Close();
                }
                ViewBag.VDList = lstVDVM;
                return PartialView("_Delete", VoucherDetailController.GetOneVoucher(voucherNumber, 0, connectionStringSQL));
            }
            catch (Exception ex)
            {
                return Json(HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde. <br>Error: " + ex.ToString()));
            }
        }

        [HttpPost]
        public IActionResult Delete(VoucherC_ViewModel voucherModel)
        {
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Del", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = voucherModel.VoucherNumber;
                cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    TempData["Message_Voucher"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                }
                cnn.Close();
            }
            return RedirectToAction("Index");
        }

        private List<SelectListItem> YearList()
        {
            var items = new List<SelectListItem>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from VoucherRequest.vw_Voucher_Years", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        items.Add(new SelectListItem()
                        {
                            Value = rd["año"].ToString(),
                            Text = rd["año"].ToString(),
                            Selected = false
                        });
                    }
                }
                cnn.Close();
            }
            items.Insert(0, new SelectListItem()
            {
                Text = "Año",
                Value = "0",
                Selected = true
            });
            return items;
        }

        [HttpPost]
        public IActionResult GetData(string user, string cc, int month, int year, string authorized, string pickedUp, string voucherNumber, int canceled, int page)
        {
            int offset = (page - 1) * 12;
            string empNumber = null;
            if (User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                empNumber = User.FindFirst("empNumber").Value.ToString();
            }
            var lstVM = new List<Voucher_Model>();
            int rawMaterial = -1;
            if (User.IsInRole("Almacen"))
            {
                rawMaterial = 0;
            }
            if (User.IsInRole("AlmacenMP"))
            {
                rawMaterial = 1;
            }
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Sel", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = string.IsNullOrEmpty(user) ? DBNull.Value : user;
                cmd.Parameters.Add("@cc", SqlDbType.VarChar).Value = string.IsNullOrEmpty(cc) ? DBNull.Value : cc;
                cmd.Parameters.Add("@month", SqlDbType.Int).Value = month == 0 ? DBNull.Value : month;
                cmd.Parameters.Add("@year", SqlDbType.Int).Value = year == 0 ? DBNull.Value : year;
                cmd.Parameters.Add("@authorized", SqlDbType.VarChar).Value = string.IsNullOrEmpty(authorized) ? DBNull.Value : authorized;
                cmd.Parameters.Add("@imported", SqlDbType.Bit).Value = DBNull.Value;
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = string.IsNullOrEmpty(empNumber) ? DBNull.Value : empNumber;
                cmd.Parameters.Add("@pickedUp", SqlDbType.VarChar).Value = string.IsNullOrEmpty(pickedUp) ? DBNull.Value : pickedUp;
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = string.IsNullOrEmpty(voucherNumber) ? DBNull.Value : voucherNumber;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial == -1 ? DBNull.Value : rawMaterial;
                cmd.Parameters.Add("@canceled", SqlDbType.Bit).Value = canceled == -1 ? DBNull.Value : canceled;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var date = DateOnly.FromDateTime(DateTime.Parse(rd["requestedDate"].ToString()));
                        lstVM.Add(new Voucher_Model()
                        {
                            VoucherNumber = Convert.ToString(rd["voucherNumber"]),
                            RequestedDate = date.ToString("dd-MM-yyyy"),
                            RequestedBy = Convert.ToString(rd["requestedBy"]),
                            Authorized = Convert.ToInt32(rd["authorized"]),
                            CostCenter = Convert.ToString(rd["cc"]),
                            Imported = Convert.ToInt32(rd["imported"]),
                            PickedUpBy = Convert.ToString(rd["pickedUpBy"]),
                            Canceled = Convert.ToInt32(rd["canceled"]),
                            AuthorizedDate = Convert.ToString(rd["authorizedDate"])
                        });
                    }
                }
            }
            int pages = CountPages(user, cc, month, year, authorized, pickedUp, voucherNumber, canceled, empNumber, rawMaterial);
            var result = HomeController.ControlPages(page, pages);
            ViewBag.ActualPage = page;
            ViewBag.MinPage = result.minPage;
            ViewBag.MaxPage = result.maxPage;
            ViewBag.Pages = pages;
           
            return PartialView("PartialViews//_PVAllRequests", lstVM);
        }

        private int CountPages(string user, string cc, int month, int year, string authorized, string pickedUp, string voucherNumber, int canceled, string empNumber, int rawMaterial)
        {
            int pages = 0;
            using (var conn = new SqlConnection(connectionStringSQL))
            {
                conn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Count", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@user", SqlDbType.VarChar).Value = string.IsNullOrEmpty(user) ? DBNull.Value : user;
                cmd.Parameters.Add("@cc", SqlDbType.VarChar).Value = string.IsNullOrEmpty(cc) ? DBNull.Value : cc;
                cmd.Parameters.Add("@month", SqlDbType.Int).Value = month == 0 ? DBNull.Value : month;
                cmd.Parameters.Add("@year", SqlDbType.Int).Value = year == 0 ? DBNull.Value : year;
                cmd.Parameters.Add("@authorized", SqlDbType.VarChar).Value = string.IsNullOrEmpty(authorized) ? DBNull.Value : authorized;
                cmd.Parameters.Add("@imported", SqlDbType.Bit).Value = DBNull.Value;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = string.IsNullOrEmpty(empNumber) ? DBNull.Value : empNumber;
                cmd.Parameters.Add("@pickedUp", SqlDbType.VarChar).Value = string.IsNullOrEmpty(pickedUp) ? DBNull.Value : pickedUp;
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = string.IsNullOrEmpty(voucherNumber) ? DBNull.Value : voucherNumber;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial == -1 ? DBNull.Value : rawMaterial;
                cmd.Parameters.Add("@canceled", SqlDbType.Bit).Value = canceled;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    decimal temp = (Convert.ToDecimal(rd["conteo"]) / 12);
                    pages = Convert.ToInt32(Math.Ceiling(temp));
                }
                conn.Close();
            }
            return pages;
        }
    }
}
