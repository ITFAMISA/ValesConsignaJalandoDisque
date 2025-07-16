using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using VoucherCapture.ViewModel;
using Microsoft.AspNetCore.Authorization;
using VoucherCapture.Models;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class VoucherDetailController : Controller
    {
        private readonly string connectionStringSQL;
        public VoucherDetailController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Index(string voucherNumber, int authorized, int canceled)
        {
            ViewBag.Authorized = authorized;
            ViewBag.Canceled = canceled;
            return View("Index", GetOneVoucher(voucherNumber, 0, connectionStringSQL));
        }

        public IActionResult Update(string voucherNumber)
        {
            var result = GetLists(voucherNumber, true);
            return PartialView("PartialViews\\_VoucherDetailUpdate", result.lstVDVMList);
        }

        public static VoucherC_ViewModel GetOneVoucher(string voucherNumber, int idVoucher, string connection)
        {
            var voucherM = new VoucherC_ViewModel();
            using (var cnn = new SqlConnection(connection))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_SelOneVoucher", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = string.IsNullOrEmpty(voucherNumber) ? DBNull.Value : voucherNumber;
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucher == 0 ? DBNull.Value : idVoucher;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    voucherM.RequestedDate = rd["requestedDate"].ToString();
                    voucherM.AuthorizationDate = rd["authorizationDate"].ToString();
                    voucherM.VoucherNumber = rd["voucherNumber"].ToString();
                    voucherM.Cc = rd["costCenter"].ToString();
                    voucherM.PickedUp = Convert.ToBoolean(rd["pickedUp"]);
                    voucherM.PickedUpBy = rd["pickedUpBy"].ToString();
                    voucherM.Canceled = Convert.ToInt32(rd["canceled"]);
                    voucherM.AuthorizedBy = rd["authorizedBy"].ToString();
                    voucherM.RequestedBy = rd["requestedBy"].ToString();
                    voucherM.Imported = Convert.ToBoolean(rd["imported"]);
                    voucherM.Comment = rd["comment"].ToString();
                    voucherM.Concept = rd["concept"].ToString();
                    voucherM.Storage = rd["storage"].ToString();
                }
                cnn.Close();
            }
            return voucherM;
        }      
        
        public IActionResult PrintView(string voucherNumber)
        {
            if(User.IsInRole("Lectura") || User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Error: no tiene permisos suficientes para esto.");
                return RedirectToAction("Index", "Voucher");
            }
            var model = new VoucherComplete_Model
            {
                Header = new VoucherC_ViewModel(),
                Body = new List<List<VoucherDetail_ViewModel>>()
            };
            var result = GetLists(voucherNumber, false);
            model.Header = GetOneVoucher(voucherNumber, 0, connectionStringSQL);
            model.Body = result.lstVDVMList;
            if (User.IsInRole("Almacen") || User.IsInRole("AlmacenMP"))
            {
                if (model.Header.PickedUp == false)
                {
                    using (var cnn = new SqlConnection(connectionStringSQL))
                    {
                        cnn.Open();
                        var cmd = new SqlCommand("VoucherRequest.sp_Voucher_UpdPickedUp", cnn)
                        {
                            CommandType = CommandType.StoredProcedure
                        };
                        cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = voucherNumber;
                        cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                        using (var rd = cmd.ExecuteReader())
                        {
                            rd.Read();
                            TempData["Message_Voucher"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                        }
                        cnn.Close();
                    }
                }
            }           
            return View("SimpleView", model);
        }

        public IActionResult Cancel(string voucherNumber)
        {
            if (User.IsInRole("Lectura") || User.IsInRole("Firma") || User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                return Json(HomeController.ShowAlert("danger", "Error: no tiene los suficientes permisos para hacer esto"));
            }
            var voucherModel = new Voucher_Model
            {
                VoucherNumber = voucherNumber
            };
            return PartialView("_Cancel", voucherModel);
        }

        [HttpPost]
        public IActionResult Cancel(Voucher_Model vouchModel)
        {
            if (User.IsInRole("Lectura") || User.IsInRole("Firma") || User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                return Json(HomeController.ShowAlert("danger", "Error: no tiene los suficientes permisos para hacer esto"));
            }
            var lstVDM = new List<VoucherDetail_Model>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Cancel", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@voucherNumber", SqlDbType.VarChar).Value = vouchModel.VoucherNumber;
                cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstVDM.Add(new VoucherDetail_Model()
                        {
                            IdStorage = Convert.ToInt32(rd["idStorage"]),
                            IdVoucherDetail = Convert.ToInt32(rd["idVoucherDetail"])
                        });
                    }
                }
                cnn.Close();
            } 
            foreach(var item in lstVDM)
            {
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_UpdQtyCancel", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idVoucherDetail", SqlDbType.Int).Value = item.IdVoucherDetail;
                    cmd.Parameters.Add("@idStorage", SqlDbType.Int).Value = item.IdStorage;
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        TempData["Message_Voucher"] = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                    }
                        cnn.Close();
                }
            }
            return RedirectToAction("Index", "Voucher");
        }

        [HttpPost]
        public IActionResult GetData(string voucherNumber, int authorized, int canceled)
        {
            if (string.IsNullOrEmpty(voucherNumber))
            {
                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Sucedió un error inesperado, favor de intentarlo más tarde.");
                return RedirectToAction("Index", "Voucher");
            }
            bool rawM = false;
            if(authorized == 1 && User.IsInRole("AlmacenMP") && canceled != 1)
            {
                rawM = true;
            }
            var result = GetLists(voucherNumber, rawM);
            if (result.lstVDVM[0].IdRequestStatus == 2 && !User.IsInRole("Operacional") && !User.IsInRole("Lectura") && !User.IsInRole("CentroCosto"))
            {
                ViewBag.IdVoucher = result.lstVDVM[0].IdVoucher;
                ViewBag.VoucherNumber = voucherNumber;
                return PartialView("PartialViews\\_VoucherDetailForm", result.lstVDVM);
            }
            int rejected = 0;
            for(int i = 0; i< result.lstVDVM.Count; i++)
            {
                if (result.lstVDVM[i].IdRequestStatus == 3) {
                    rejected += 1;
                }
            }
            bool flag = false;
            if(rejected == (result.lstVDVM.Count + 1))
            {
                flag = true;
            }
            if(authorized == 1 && User.IsInRole("AlmacenMP") && canceled != 1 && !flag)
            {
                ViewBag.VoucherNumber = voucherNumber;
                return PartialView("PartialViews\\_VoucherDetailUpdate", result.lstVDVMList);
            }
            return PartialView("PartialViews\\_VoucherDetail", result.lstVDVMList);
        }

        private (List<VoucherDetail_ViewModel> lstVDVM, List<List<VoucherDetail_ViewModel>> lstVDVMList) GetLists(string voucherNumber, bool rawMaterial)
        {
            var lstVDVM = new List<VoucherDetail_ViewModel>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_SelByVoucher", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = voucherNumber;
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = DBNull.Value;
                cmd.Parameters.Add("@idVoucherDtl", SqlDbType.Int).Value = DBNull.Value;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstVDVM.Add(new VoucherDetail_ViewModel()
                        {
                            IdVoucher = Convert.ToInt32(rd["idVoucher"]),
                            IdVoucherDetail = Convert.ToInt32(rd["idVoucherDetail"]),
                            MicrosipKey = rd["microsipKey"].ToString(),
                            QtyAuthorized = float.Parse(rd["qtyAuthorized"].ToString()),
                            QtyRequested = float.Parse(rd["qtyRequested"].ToString()),
                            Supply = rd["supply"].ToString(),
                            UnitType = rd["unitType"].ToString(),
                            IdSupply = Convert.ToInt32(rd["idSupply"]),
                            IdRequestStatus = Convert.ToInt32(rd["idRequestStatus"])
                        });
                    }
                }
                if ((lstVDVM[0].IdRequestStatus == 2 && !User.IsInRole("Operacional") && !User.IsInRole("CentroCosto")) || rawMaterial)
                {
                    foreach (var item in lstVDVM)
                    {
                        var lstStorage = new List<Storage_ViewModel>();
                        var cmdStorage = new SqlCommand("VoucherRequest.sp_Storage_SelByIdSupply", cnn)
                        {
                            CommandType = CommandType.StoredProcedure
                        };
                        cmdStorage.Parameters.Add("@idSupply", SqlDbType.Int).Value = item.IdSupply;
                        cmdStorage.Parameters.Add("@idVoucher", SqlDbType.Int).Value = !rawMaterial ? DBNull.Value : item.IdVoucher;
                        using (var rd = cmdStorage.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                lstStorage.Add(new Storage_ViewModel()
                                {
                                    IdStorage = Convert.ToInt32(rd["idStorage"]),
                                    Name = Convert.ToString(rd["name"]),
                                    QtyTotal = float.Parse(rd["qtyTotal"].ToString())
                                });
                            }
                        }
                        item.Storages = lstStorage;
                    }
                }
                cnn.Close();
            }
            var lstVDVMList = new List<List<VoucherDetail_ViewModel>>();
            if (lstVDVM[0].IdRequestStatus != 2)
            {
                var lstAux = lstVDVM.GroupBy(x => x.IdSupply);
                var lstVDVMSum = new List<VoucherDetail_ViewModel>();
                foreach(var item in lstAux)
                {
                    float sumaQty = item.Sum(x => x.QtyAuthorized);
                    var firstObj = item.First();
                    firstObj.QtyAuthorized = sumaQty;
                    lstVDVMSum.Add(firstObj);
                }
                lstVDVM = lstVDVMSum;
                lstAux = lstVDVM.GroupBy(x => x.IdRequestStatus);
                lstVDVMList = lstAux.Select(x => x.ToList()).ToList();
            } else
            {
                lstVDVMList.Add(lstVDVM);
            }
            return (lstVDVM, lstVDVMList);
        }
    }
}
