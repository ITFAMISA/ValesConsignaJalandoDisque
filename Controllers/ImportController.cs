using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using VoucherCapture.ViewModel;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class ImportController : Controller
    {
        private readonly string connectionStringSQL, blobConn, blobFile, blobContainer;
        public ImportController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
            blobConn = config.GetValue<string>("BlobConnection");
            blobContainer = config.GetValue<string>("BlobContainer");
            blobFile = config.GetValue<string>("BlobFileName");
        }
        public IActionResult Index()
        {
            if (User.IsInRole("Administrador") || User.IsInRole("Almacen") || User.IsInRole("AlmacenMP"))
            {
                return View("Index");
            }
            TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder.");
            return RedirectToAction("Index", "Voucher"); 
        }

        [HttpPost]
        public async Task<IActionResult> Import(string idsToImport)
        {
            if (User.IsInRole("Administrador") || User.IsInRole("Almacen") || User.IsInRole("AlmacenMP"))
            {
                var lstVouchers = ListVouchers(idsToImport);
                var lstVouchersDetail = ListVouchersDetails(idsToImport);
                if (lstVouchers == null || lstVouchersDetail == null)
                {
                    return Json(HomeController.ShowAlert("danger", "Sucedió un error, favor de intentarlo más tarde."));
                }
                var diccionario = new Dictionary<int, List<ExportVoucherDetail_ViewModel>>();
                foreach (var item in lstVouchersDetail)
                {
                    if (!diccionario.ContainsKey(item.IdVoucher))
                    {
                        diccionario[item.IdVoucher] = new List<ExportVoucherDetail_ViewModel>();
                    }
                    diccionario[item.IdVoucher].Add(item);
                }
                List<List<ExportVoucherDetail_ViewModel>> lstVoucherDetaillst = diccionario.Values.ToList();
                var lstExportOutputs = new List<ExportOutput_ViewModel>();
                foreach (var item in lstVouchers)
                {
                    lstExportOutputs.Add(new ExportOutput_ViewModel()
                    {
                        NuevaSalida = item,
                        RenglonesSalida = lstVoucherDetaillst.FirstOrDefault(lst => lst.Any(x => x.IdVoucher == item.IdVoucher))
                    });
                }
                BlobServiceClient blobServiceClient = new BlobServiceClient(blobConn);
                BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainer);
                BlobClient blobClient = blobContainerClient.GetBlobClient(blobFile);
                string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), blobFile);
                if (blobClient.Exists())
                {
                    var lstOutputsOld = await AlreadyExists(blobClient, fullPath);
                    if (lstOutputsOld == null)
                    {
                        return Json(HomeController.ShowAlert("danger", "Hubo un problema al momento de descargar la información, favor de intentarlo más tarde."));
                    }
                    lstExportOutputs.AddRange(lstOutputsOld);
                }
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
                string json = JsonSerializer.Serialize(lstExportOutputs);
                System.IO.File.WriteAllText(fullPath, json);
                var response = await blobClient.UploadAsync(fullPath, true);
                if (response.GetRawResponse().IsError)
                {
                    return Json(HomeController.ShowAlert("danger", "Sucedió un problema y no se subió la información. <br>" + response.GetRawResponse()));
                }
                UpdateImports(idsToImport);
                System.IO.File.Delete(fullPath);
                return Json(HomeController.ShowAlert("success", "Los vales han sido enviados correctamente y se reflejarán en el sistema a partir de mañana"));
            }
            TempData["Message_Voucher"] = HomeController.ShowAlert("danger", "Error: no tiene permiso de acceder.");
            return RedirectToAction("Index", "Voucher");
        }

        public IActionResult InfoModal(int idVoucher, int idVoucherDetail)        
        {
            ViewBag.VoucherModel = VoucherDetailController.GetOneVoucher(null, idVoucher, connectionStringSQL);
            var lstVM = new List<VoucherDetail_ViewModel>();
            using(var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_VoucherDetail_SelByVoucher", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVoucher", SqlDbType.Int).Value = idVoucher;
                cmd.Parameters.Add("@voucherNumber", SqlDbType.VarChar).Value = DBNull.Value;
                cmd.Parameters.Add("@idVoucherDtl", SqlDbType.Int).Value = idVoucherDetail;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstVM.Add(new VoucherDetail_ViewModel()
                        {
                            Supply = Convert.ToString(rd["supply"]),
                            MicrosipKey = Convert.ToString(rd["microsipKey"]),
                            QtyAuthorized = float.Parse(rd["qtyAuthorized"].ToString()),
                            QtyRequested = float.Parse(rd["qtyRequested"].ToString()),
                            UnitType = Convert.ToString(rd["unitType"])
                        });
                    }
                }
            }
            return PartialView("_InfoVoucher", lstVM);
        }

        [HttpPost]
        public IActionResult GetData(int imported, int page)
        {
            int offset = (page - 1) * 12;
            int rawMaterial = 2;
            List<VoucherC_ViewModel> lstVVM = new List<VoucherC_ViewModel>();
            using (var conn = new SqlConnection(connectionStringSQL))
            {
                conn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_SelToImport", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                if (User.IsInRole("Almacen"))
                {
                    rawMaterial = 0;
                }
                if (User.IsInRole("AlmacenMP"))
                {
                    rawMaterial = 1;
                }
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial == 2 ? DBNull.Value : rawMaterial;
                cmd.Parameters.Add("@imported", SqlDbType.Bit).Value = imported;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstVVM.Add(new VoucherC_ViewModel()
                        {
                            IdVoucher = Convert.ToInt32(rd["idVoucher"]),
                            IdRequestSignature = Convert.ToInt32(rd["idRequestSignature"]),
                            Cc = rd["cc"].ToString(),
                            RequestedBy = rd["requestedBy"].ToString(),
                            RequestedDate = rd["requestedDate"].ToString(),
                            AuthorizationDate = rd["authorizationDate"].ToString(),
                            RequestStatus = rd["requestStatus"].ToString(),
                            PickedUpBy = rd["pickedUpBy"].ToString(),
                            PickedUp = Convert.ToBoolean(rd["pickedUp"]),
                            Imported = Convert.ToBoolean(rd["imported"]),
                            AuthorizedBy = rd["authorizedBy"].ToString(),
                            VoucherNumber = Convert.ToString(rd["voucherNumber"]),
                            idVoucherDetail = Convert.ToInt32(rd["idVoucherDetail"])
                        });
                    }
                }
                conn.Close();
            }
            int pages = CountPages(imported, rawMaterial);
            var result = HomeController.ControlPages(page, pages);
            ViewBag.Imported = imported;
            ViewBag.ActualPage = page;
            ViewBag.MinPage = result.minPage;
            ViewBag.MaxPage = result.maxPage;
            ViewBag.Pages = pages;
            return PartialView("_PVImportTbl", lstVVM);
        }

        private int CountPages(int imported, int rawMaterial)
        {
            int pages = 0;
            using (var conn = new SqlConnection(connectionStringSQL))
            {
                conn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_CountToImport", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@imported", SqlDbType.Bit).Value = imported;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial == 2 ? DBNull.Value : rawMaterial;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    decimal temp = (Convert.ToDecimal(rd["conteo"]) / 18);
                    pages = Convert.ToInt32(Math.Ceiling(temp));
                }
                conn.Close();
            }
            return pages;
        }

        private async Task<List<ExportOutput_ViewModel>> AlreadyExists(BlobClient blobClient, string path)
        {
            FileStream fs = System.IO.File.OpenWrite(path);
            await blobClient.DownloadToAsync(fs);
            fs.Close();
            if (System.IO.File.Exists(path))
            {
                await blobClient.DeleteAsync();
                string json = System.IO.File.ReadAllText(path);
                var lstExportOutputs = JsonSerializer.Deserialize<List<ExportOutput_ViewModel>>(json);
                return lstExportOutputs;
            }
            else
            {
                return null;
            }
        }

        private void UpdateImports(string idsVouchers)
        {
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Voucher_UpdImported", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idsVouchers", SqlDbType.VarChar).Value = idsVouchers;
                cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                cmd.ExecuteNonQuery();
                cnn.Close();
            }
        }

        private List<ExportVoucher_ViewModel> ListVouchers(string idsVouchers)
        {
            var lstVoucher = new List<ExportVoucher_ViewModel>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_ExportVouchers", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVouchers", SqlDbType.VarChar).Value = idsVouchers;
                //cmd.Parameters.Add("@modifiedBy", SqlDbType.VarChar).Value = User.FindFirst("empNumber").Value.ToString();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!string.IsNullOrEmpty(rd["concepto"].ToString()))
                        {
                            lstVoucher.Add(new ExportVoucher_ViewModel()
                            {
                                IdVoucher = Convert.ToInt32(rd["idVoucher"]),
                                Concepto = Convert.ToInt32(rd["concepto"]),
                                Almacen = Convert.ToInt32(rd["almacen"]),
                                CentroCosto = Convert.ToInt32(rd["centroCosto"]),
                                Descripcion = Convert.ToString(rd["descripcion"]),
                                AlmacenDestino = 0,
                                Folio = Convert.ToString(rd["folio"]),
                                Fecha = Convert.ToString(rd["fecha"])
                            });
                        }
                        else
                        {
                            lstVoucher = null;
                            return lstVoucher;
                        }
                    }
                }
                cnn.Close();
            }
                return lstVoucher;
        }

        private List<ExportVoucherDetail_ViewModel> ListVouchersDetails(string idsVouchers)
        {
            var lstVoucherDet = new List<ExportVoucherDetail_ViewModel>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_ExportVouchersDetails", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idVouchers", SqlDbType.VarChar).Value = idsVouchers;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstVoucherDet.Add(new ExportVoucherDetail_ViewModel()
                        {
                            IdVoucher = Convert.ToInt32(rd["idVoucher"]),
                            Articulo = Convert.ToInt32(rd["articulo"]),
                            Unidades = float.Parse(rd["unidades"].ToString())
                        });
                    }
                }
                cnn.Close();
            }
                return lstVoucherDet;
        }
    }
}
