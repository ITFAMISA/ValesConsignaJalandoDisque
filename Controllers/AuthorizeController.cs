using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using VoucherCapture.Models;
using VoucherCapture.ViewModel;


namespace VoucherCapture.Controllers
{
    [Authorize]
    public class AuthorizeController : Controller
    {
        private readonly string connectionStringSQL;

        public AuthorizeController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        [HttpPost]
        public JsonResult Index(int status, int idVoucher, List<VoucherDetail_ViewModel> lsvVDM, string comment, int idConcept)
        {
            if (User.IsInRole("Lectura") || User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                return Json(HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder."));
            }
            if (idVoucher == 0)
            {
                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Ha sucedido un error id 0");
                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
            }
            int idSignatureFlow = GetIdSignatureFlow();
            if (idSignatureFlow == 0)
            {
                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Este usuario no tiene permitido autorizar solicitudes");
                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
            }
            try
            {
                var diccionario = new Dictionary<int, List<VoucherDetail_Model>>();
                var lstVDMList = new List<List<VoucherDetail_Model>>();
                var listVDM = new List<VoucherDetail_Model>();
                foreach (var item in lsvVDM)
                {
                    int countZeros = 0;
                    int i = 0;
                    foreach (var subitem in item.Storages)
                    { 
                        if (subitem.QtyTotal > 0)
                        {
                            if(i == 0) { 
                            listVDM.Add(new VoucherDetail_Model()
                            {
                                IdStorage = subitem.IdStorage,
                                QtyAuthorized = Convert.ToDecimal(subitem.QtyTotal),
                                IdVoucherDetail = item.IdVoucherDetail
                            });
                            } else
                            {
                                int idVoucherDetail = InsertVoucherDetail(idVoucher, item.IdVoucherDetail, subitem.QtyTotal);
                                listVDM.Add(new VoucherDetail_Model()
                                {
                                    IdStorage = subitem.IdStorage,
                                    QtyAuthorized = Convert.ToDecimal(subitem.QtyTotal),
                                    IdVoucherDetail = idVoucherDetail
                                });
                            }
                            i++;
                        }
                       else
                        {
                            countZeros++;
                        }
                    }
                    if(countZeros == item.Storages.Count)
                    {
                        listVDM.Add(new VoucherDetail_Model()
                        {
                            IdStorage = 1,
                            QtyAuthorized = 0,
                            IdVoucherDetail = item.IdVoucherDetail
                        });
                    }
                }

                foreach(var item in listVDM)
                {
                    if (!diccionario.ContainsKey(item.IdStorage))
                    {
                        //diccionario[item.IdStorage] = new List<VoucherDetail_Model>();
                        diccionario[item.IdVoucherDetail] = new List<VoucherDetail_Model>();
                    }
                    //diccionario[item.IdStorage].Add(item);
                    diccionario[item.IdVoucherDetail].Add(item);
                }

                foreach (var item in diccionario)
                {
                    lstVDMList.Add(item.Value);
                }

                string msg = "";
                if (lstVDMList.Count > 0)
                {
                    if (lstVDMList.Count > 1)
                    {
                        for (int i = 0; i < (lstVDMList.Count - 1); i++)
                        {
                            int idVoucherNew = InsertVoucher(idVoucher);
                            if (idVoucherNew != 0)
                            {
                                foreach (var item in lstVDMList[i])
                                {

                                    int resUpdate = UpdateIdVoucher(idVoucherNew, item.IdVoucherDetail);
                                    if (resUpdate == 0)
                                    {
                                        TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Sucedió un error inesperado: Id 0");
                                        return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
                                    }
                                    else
                                    {
                                            if(status == 3)
                                            {
                                            item.QtyAuthorized = 0;
                                            }
                                        int resQty = UpdateQuantities(item.IdVoucherDetail, item.QtyAuthorized, item.IdStorage);
                                        if (resQty == 0)
                                        {
                                            TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Sucedió un error inesperado, favor de intentarlo más tarde");
                                            return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
                                        }
                                    }
                                }
                                if (lstVDMList[i][0].QtyAuthorized == 0)
                                {
                                    msg = UpdateAndAuthorize(idVoucherNew, lstVDMList[i][0].IdStorage, idSignatureFlow, 3, comment, idConcept);
                                }
                                else
                                {
                                    msg = UpdateAndAuthorize(idVoucherNew, lstVDMList[i][0].IdStorage, idSignatureFlow, status, comment, idConcept);
                                }
                            }
                            else
                            {
                                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Sucedió un error inesperado: Id 0");
                                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
                            }
                        }
                    }
                    foreach (var item in lstVDMList[lstVDMList.Count - 1])
                    {
                        if (status == 3)
                        {
                            item.QtyAuthorized = 0;
                        }
                        int resQty = UpdateQuantities(item.IdVoucherDetail, item.QtyAuthorized, item.IdStorage);
                        if (resQty == 0)
                        {
                            TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Sucedió un error inesperado, favor de intentarlo más tarde");
                            return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
                        }
                    }
                    if (lstVDMList[lstVDMList.Count - 1][0].QtyAuthorized == 0)
                    {
                        msg = UpdateAndAuthorize(idVoucher, lstVDMList[lstVDMList.Count - 1][0].IdStorage, idSignatureFlow, 3, comment, idConcept);
                    }
                    else
                    {
                        msg = UpdateAndAuthorize(idVoucher, lstVDMList[lstVDMList.Count - 1][0].IdStorage, idSignatureFlow, status, comment, idConcept);
                    }

                }
                TempData["Message_Voucher"] = msg;
                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
            }
            catch (Exception ex)
            {
                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde. <br>Error: " + ex.ToString());
                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
            }
        }

        private int GetIdSignatureFlow()
        {
            int idSignatureFlow = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("AuthWorkflows.sp_SignatureFlow_SelIdSF", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idFlow", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    idSignatureFlow = Convert.ToInt32(rd["idSignatureFlow"]);
                }
            }
            return idSignatureFlow;
        }

        private int InsertVoucher(int idVoucherOld)
        {
            int idVoucherNew = 0;
            if (idVoucherOld == 0)
            {
                return 0;
            }
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_InsByIdVoucher", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucherOld;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    idVoucherNew = Convert.ToInt32(rd["idVoucher"]);
                }
            }
            return idVoucherNew;
        }

        private string UpdateAndAuthorize(int idVoucher, int idStorage, int idSignatureFlow, int idRequestStatus, string comment, int idConcept)
        {
            if (idVoucher == 0 || idStorage == 0 || idRequestStatus == 0)
            {
                return HomeController.ShowAlert("danger", "Hubo un problema: ids 0");
            }
            string msg = "";
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Auth", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucher;
                cmd.Parameters.Add("@idStorage", SqlDbType.Int).Value = idStorage;
                cmd.Parameters.Add("@idRequestStatus", SqlDbType.Int).Value = idRequestStatus;
                cmd.Parameters.Add("@idSignatureFlow", SqlDbType.Int).Value = idSignatureFlow;
                cmd.Parameters.Add("@comment", SqlDbType.VarChar).Value = string.IsNullOrEmpty(comment) ? DBNull.Value : comment.Trim();
                cmd.Parameters.Add("@idConcept", SqlDbType.Int).Value = idConcept;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    msg = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                }
                cnn.Close();
            }
            return msg;
        }

        private int InsertVoucherDetail(int idVoucher, int idVoucherDetail, float qtyAuthorized)
        {
            int res = 0;
            if(idVoucher == 0 || idVoucherDetail == 0 || qtyAuthorized <= 0)
            {
                return res;
            }
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_InsAuth", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucher;
                cmd.Parameters.Add("@idVoucherDetail", SqlDbType.Int).Value = idVoucherDetail;
                cmd.Parameters.Add("@qtyAuthorized", SqlDbType.Decimal).Value = qtyAuthorized;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    res = Convert.ToInt32(rd["res"]);
                }
            }
                return res;
        }

        private int UpdateIdVoucher(int idVoucherNew, int idVoucherDetail)
        {
            if (idVoucherNew == 0 || idVoucherDetail == 0)
            {
                return 0;
            }
            int res = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_UpdVoucher", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucherNew;
                cmd.Parameters.Add("@idVoucherDetail", SqlDbType.Int).Value = idVoucherDetail;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    res = Convert.ToInt32(rd["res"]);
                }
                cnn.Close();

            }
            return res;
        }

        private int UpdateQuantities(int idVoucherDetail, decimal qtyAuthorized, int idStorage)
        {
            int res = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_UpdQtyAuth", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVoucherDetail", SqlDbType.Int).Value = idVoucherDetail;
                cmd.Parameters.Add("@qtyAuthorized", SqlDbType.Decimal).Value = qtyAuthorized;
                cmd.Parameters.Add("@idStorage", SqlDbType.Int).Value = idStorage;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    res = Convert.ToInt32(rd["res"]);
                }
                cnn.Close();
            }
            return res;
        }

        [HttpPost]
        public JsonResult Update(List<VoucherDetail_ViewModel> lsvVDM, string voucherNumber)
        {
            var listVDM = new List<VoucherDetail_Model>();
            string msg = "";
            try 
            {
                foreach(var item in lsvVDM)
                {
                    foreach(var subitem in item.Storages)
                    {
                        if(subitem.QtyTotal > 0)
                        {
                            listVDM.Add(new VoucherDetail_Model()
                            {
                                IdStorage = subitem.IdStorage,
                                QtyAuthorized = Convert.ToDecimal(subitem.QtyTotal),
                                IdVoucherDetail = item.IdVoucherDetail,
                                IdSupply = item.IdSupply
                            });
                        }
                    }
                }

                foreach (var item in listVDM)
                {
                    using (var cnn = new SqlConnection(connectionStringSQL))
                    {
                        cnn.Open();
                        var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_UpdRM", cnn);
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@idVoucherDetail", SqlDbType.Int).Value = item.IdVoucherDetail;
                        cmd.Parameters.Add("@qtyAuthorized", SqlDbType.Decimal).Value = item.QtyAuthorized;
                        cmd.Parameters.Add("@idStorage", SqlDbType.Int).Value = item.IdStorage;
                        cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = item.IdSupply;
                        cmd.Parameters.Add("@voucherNumber", SqlDbType.Char).Value = voucherNumber;
                        cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                        using (var rd = cmd.ExecuteReader())
                        {
                            rd.Read();
                            msg = HomeController.ShowAlert(rd["color"].ToString(), rd["message"].ToString());
                            if (rd["color"].ToString() == "warning")
                            {
                                cnn.Close();
                                break;
                            }
                        }
                        cnn.Close();
                    }
                }
                TempData["Message_Voucher"] = msg;
                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
            }
            catch (Exception ex)
            {
                TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde. <br>Error: " + ex.ToString());
                return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
            }
        }
    }
}
