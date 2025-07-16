using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using VoucherCapture.Models;
using VoucherCapture.ViewModel;
using System.Data.Common;
using System;
using System.Linq;
using System.Collections.Generic;

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
            // --- VALIDACIONES INICIALES ---
            if (User.IsInRole("Lectura") || User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                return Json(HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder."));
            }
            if (idVoucher == 0)
            {
                return Json(HomeController.ShowAlert("danger", "Ha sucedido un error id 0."));
            }
            int idSignatureFlow = GetIdSignatureFlow();
            if (idSignatureFlow == 0)
            {
                return Json(HomeController.ShowAlert("danger", "Este usuario no tiene permitido autorizar solicitudes."));
            }

            // --- INICIA CONEXIÓN Y TRANSACCIÓN ---
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                using (SqlTransaction transaction = cnn.BeginTransaction())
                {
                    try
                    {
                        // --- LÓGICA DE PROCESAMIENTO ---
                        var listVDM = new List<VoucherDetail_Model>();
                        const int CONSIGNMENT_VIRTUAL_ID = 999;
                        const int GENERAL_WAREHOUSE_ID = 19; // ID de ALMACEN GENERAL

                        // 1. Mapea los datos del front-end a un modelo interno limpio
                        foreach (var item in lsvVDM)
                        {
                            foreach (var subitem in item.Storages)
                            {
                                if (subitem.QtyTotal > 0)
                                {
                                    bool isConsignment = subitem.IdStorage == CONSIGNMENT_VIRTUAL_ID;
                                    listVDM.Add(new VoucherDetail_Model()
                                    {
                                        IdVoucherDetail = item.IdVoucherDetail,
                                        QtyAuthorized = Convert.ToDecimal(subitem.QtyTotal),
                                        IdStorage = isConsignment ? GENERAL_WAREHOUSE_ID : subitem.IdStorage,
                                        IsFromConsignment = isConsignment
                                    });
                                }
                            }
                        }

                        // 2. Agrupa los artículos por su almacén físico real
                        var vouchersPorAlmacen = listVDM.GroupBy(d => d.IdStorage)
                                                        .Select(g => g.ToList())
                                                        .ToList();

                        // 3. Procesa cada grupo como un vale separado
                        // Si hay más de un almacén, crea vales nuevos
                        if (vouchersPorAlmacen.Count > 1)
                        {
                            for (int i = 0; i < vouchersPorAlmacen.Count - 1; i++)
                            {
                                var valeParcial = vouchersPorAlmacen[i];
                                int idVoucherNew = InsertVoucher(cnn, transaction, idVoucher);

                                foreach (var item in valeParcial)
                                {
                                    UpdateIdVoucher(cnn, transaction, idVoucherNew, item.IdVoucherDetail);
                                    UpdateQuantities(cnn, transaction, item, status);
                                }

                                int finalStatus = (valeParcial.All(item => item.QtyAuthorized == 0)) ? 3 : status;
                                UpdateAndAuthorize(cnn, transaction, idVoucherNew, valeParcial.First().IdStorage, idSignatureFlow, finalStatus, comment, idConcept);
                            }
                        }

                        // 4. Procesa el último (o único) grupo con el ID del vale original
                        var ultimoVale = vouchersPorAlmacen.Last();
                        foreach (var item in ultimoVale)
                        {
                            UpdateQuantities(cnn, transaction, item, status);
                        }

                        int ultimoStatus = (ultimoVale.All(item => item.QtyAuthorized == 0)) ? 3 : status;
                        UpdateAndAuthorize(cnn, transaction, idVoucher, ultimoVale.First().IdStorage, idSignatureFlow, ultimoStatus, comment, idConcept);

                        // --- COMMIT ---
                        transaction.Commit();
                        TempData["Message_Voucher"] = HomeController.ShowAlert("success", "El vale ha sido autorizado correctamente.");
                    }
                    catch (Exception ex)
                    {
                        // --- ROLLBACK ---
                        transaction.Rollback();
                        TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Ha sucedido un error y la operación fue revertida. <br>Error: " + ex.Message + ex.StackTrace);
                    }
                    return Json(new { RedirectUrl = Url.Action("Index", "Voucher") });
                }
            }
        }

        // --- MÉTODOS PRIVADOS ---
        private int GetIdSignatureFlow()
        {
            int idSignatureFlow = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("AuthWorkflows.sp_SignatureFlow_SelIdSF", cnn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.Add("@idFlow", SqlDbType.Int).Value = 1;
                cmd.Parameters.Add("@empNumber", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    idSignatureFlow = Convert.ToInt32(result);
                }
            }
            return idSignatureFlow;
        }

        private void UpdateQuantities(SqlConnection cnn, SqlTransaction transaction, VoucherDetail_Model itemDetail, int status)
        {
            decimal qtyToAuth = (status == 3) ? 0 : itemDetail.QtyAuthorized;

            // 1. Actualiza la cantidad autorizada en la línea del detalle
            var cmdUpdateDetail = new SqlCommand("UPDATE VoucherRequest.VoucherDetail SET qtyAuthorized = @qty WHERE idVoucherDetail = @id", cnn, transaction);
            cmdUpdateDetail.Parameters.AddWithValue("@qty", qtyToAuth);
            cmdUpdateDetail.Parameters.AddWithValue("@id", itemDetail.IdVoucherDetail);
            if (cmdUpdateDetail.ExecuteNonQuery() == 0)
                throw new Exception($"No se pudo actualizar la cantidad autorizada para el detalle ID {itemDetail.IdVoucherDetail}.");

            // 2. Obtiene el IdSupply para usarlo en la actualización de stock
            var cmdGetSupply = new SqlCommand("SELECT idSupply FROM VoucherRequest.VoucherDetail WHERE idVoucherDetail = @id", cnn, transaction);
            cmdGetSupply.Parameters.AddWithValue("@id", itemDetail.IdVoucherDetail);
            int idSupply = (int)cmdGetSupply.ExecuteScalar();

            // 3. Llama al SP correcto para descontar el stock
            if (itemDetail.IsFromConsignment)
            {
                var cmd = new SqlCommand("VoucherRequest.sp_UpdateConsignmentStock", cnn, transaction) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@idSupply", idSupply);
                cmd.Parameters.AddWithValue("@quantity", qtyToAuth);
                cmd.ExecuteNonQuery();
            }
            else
            {
                var cmd = new SqlCommand("VoucherRequest.sp_UpdatePhysicalStock", cnn, transaction) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@idSupply", idSupply);
                cmd.Parameters.AddWithValue("@idStorage", itemDetail.IdStorage);
                cmd.Parameters.AddWithValue("@quantity", qtyToAuth);
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateAndAuthorize(SqlConnection cnn, SqlTransaction transaction, int idVoucher, int idStorage, int idSignatureFlow, int idRequestStatus, string comment, int idConcept)
        {
            var cmd = new SqlCommand("VoucherRequest.sp_Voucher_Auth", cnn, transaction) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@idVoucher", idVoucher);
            cmd.Parameters.AddWithValue("@idStorage", idStorage);
            cmd.Parameters.AddWithValue("@idRequestStatus", idRequestStatus);
            cmd.Parameters.AddWithValue("@idSignatureFlow", idSignatureFlow);
            cmd.Parameters.AddWithValue("@comment", string.IsNullOrEmpty(comment) ? DBNull.Value : (object)comment.Trim());
            cmd.Parameters.AddWithValue("@idConcept", idConcept);
            cmd.ExecuteNonQuery();
        }

        private int InsertVoucher(SqlConnection cnn, SqlTransaction transaction, int idVoucherOld)
        {
            var cmd = new SqlCommand("VoucherRequest.sp_Voucher_InsByIdVoucher", cnn, transaction) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucherOld;

            var result = cmd.ExecuteScalar();
            int newId = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;

            if (newId <= 0) throw new Exception("No se pudo crear el nuevo vale a partir del original.");
            return newId;
        }

        private void UpdateIdVoucher(SqlConnection cnn, SqlTransaction transaction, int idVoucherNew, int idVoucherDetail)
        {
            var cmd = new SqlCommand("UPDATE VoucherRequest.VoucherDetail SET idVoucher = @idVoucherNew WHERE idVoucherDetail = @idVoucherDetail", cnn, transaction);
            cmd.Parameters.AddWithValue("@idVoucherNew", idVoucherNew);
            cmd.Parameters.AddWithValue("@idVoucherDetail", idVoucherDetail);
            if (cmd.ExecuteNonQuery() == 0)
                throw new Exception($"No se pudo actualizar el IdVoucher para el detalle {idVoucherDetail}.");
        }

        private int InsertVoucherDetail(SqlConnection cnn, SqlTransaction transaction, int idVoucher, int idVoucherDetail, float qtyAuthorized)
        {
            var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_InsAuth", cnn, transaction) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@idVoucher", idVoucher);
            cmd.Parameters.AddWithValue("@idVoucherDetail", idVoucherDetail);
            cmd.Parameters.AddWithValue("@qtyAuthorized", qtyAuthorized);

            var result = cmd.ExecuteScalar();
            int newId = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;

            if (newId <= 0) throw new Exception("No se pudo insertar el nuevo detalle del vale.");
            return newId;
        }
    }
}