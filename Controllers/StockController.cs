using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using VoucherCapture.ViewModel;
using VoucherCapture.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using Azure.Storage.Blobs;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class StockController : Controller
    {
        private readonly string connectionStringSQL;

        public StockController(IConfiguration config)
        {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }

        public IActionResult Index()
        {
            ViewBag.Storages = ListStorage();
            ViewBag.GroupSupplyLines = ListGroupSupplyLine(connectionStringSQL, 0);
            return View();
        }

        [HttpPost]
        public IActionResult SupplyInfo(int idSupply)
        {
            return PartialView("_SupplyInfo", SelectOne(idSupply));
        }

        [HttpPost]
        public JsonResult Update(Supply_Model supplyModel)
        {
            if (User.IsInRole("Lectura") || User.IsInRole("Firma") || User.IsInRole("Operacional") || User.IsInRole("CentroCosto"))
            {
                return Json("");
            }
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Supply_UpdInspection", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = supplyModel.IdSupply;
                cmd.Parameters.Add("@inspection", SqlDbType.Bit).Value = supplyModel.Inspection;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = supplyModel.RawMaterial;
                cmd.ExecuteReader();
            }
            return Json(supplyModel.Inspection);
        }
        public async Task DownloadFile()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();
            string blobConn = config.GetValue<string>("BlobConnection");
            string blobContainer = config.GetValue<string>("BlobContainer");
            string blobFile = "articulosData.json";
            try
            {
                string fullPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), $"MicrosipData\\{blobFile}");
                BlobServiceClient serviceClient = new BlobServiceClient(blobConn);
                BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(blobContainer);
                BlobClient client = containerClient.GetBlobClient(blobFile);
                if (client.Exists())
                {
                    FileStream fs = System.IO.File.OpenWrite(fullPath);
                    await client.DownloadToAsync(fs);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                TempData["Message_Stock"] = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde: <br>" + ex.ToString());
            }
        }

        public async Task<IActionResult> UpdateData()
        {
            try
            {
                await DownloadFile();
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    await cnn.OpenAsync();
                    var cmd = new SqlCommand("VoucherRequest.sp_MergeStorageSupply", cnn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 280;
                    await cmd.ExecuteNonQueryAsync();
                    await cnn.CloseAsync();
                }
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    await cnn.OpenAsync();
                    var cmd = new SqlCommand("VoucherRequest.sp_MergeSupply", cnn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    await cmd.ExecuteNonQueryAsync();
                    await cnn.CloseAsync();
                }
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    await cnn.OpenAsync();
                    var cmd = new SqlCommand("VoucherRequest.sp_UpdQuantities", cnn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    await cmd.ExecuteNonQueryAsync();
                    await cnn.CloseAsync();
                }
                TempData["Message_Stock"] = HomeController.ShowAlert("success", "Datos actualizados correctamente");
            }
            catch (Exception ex)
            {
                TempData["Message_Stock"] = HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde: <br>" + ex.ToString());
            }
            return Json(new { RedirectUrl = Url.Action("Index", "Stock") });
        }

        [HttpPost]
        public IActionResult GetData(string msKey, string description, int orderBy, string inspection, int storage, int groupLine, int supplyLine, int rawMaterial, int page)
        {
            try
            {
                int offset = (page - 1) * 30;
                var lstStock = new List<Supply_Model>();
                using (var cnn = new SqlConnection(connectionStringSQL))
                {
                    cnn.Open();
                    var cmd = new SqlCommand("VoucherRequest.sp_Supply_Sel", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@microsipKey", SqlDbType.VarChar).Value = string.IsNullOrEmpty(msKey) ? DBNull.Value : msKey.Trim();
                    cmd.Parameters.Add("@description", SqlDbType.VarChar).Value = string.IsNullOrEmpty(description) ? DBNull.Value : description.Trim();
                    cmd.Parameters.Add("@orderBy", SqlDbType.Int).Value = orderBy;
                    cmd.Parameters.Add("@inspection", SqlDbType.VarChar).Value = string.IsNullOrEmpty(inspection) ? DBNull.Value : inspection;
                    cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                    cmd.Parameters.Add("@storage", SqlDbType.Int).Value = storage == 0 ? DBNull.Value : storage;
                    cmd.Parameters.Add("@groupSupplyLine", SqlDbType.Int).Value = groupLine == 0 ? DBNull.Value : groupLine;
                    cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial == 2 ? DBNull.Value : rawMaterial;
                    cmd.Parameters.Add("@supplyLine", SqlDbType.Int).Value = supplyLine == 0 ? DBNull.Value : supplyLine;
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lstStock.Add(new Supply_Model()
                            {
                                IdSupply = Convert.ToInt32(rd["idSupply"]),
                                Description = Convert.ToString(rd["description"]),
                                QtyTotal = float.Parse(rd["qtyTotal"].ToString()),
                                UnitType = Convert.ToString(rd["unitType"]),
                                Inspection = Convert.ToInt32(rd["inspection"]),
                                MicrosipKey = Convert.ToString(rd["microsipKey"])
                            });
                        }
                    }
                    cnn.Close();
                }
                int pages = CountPages(msKey, description, inspection, storage, groupLine, supplyLine, rawMaterial);
                var result = HomeController.ControlPages(page, pages);
                ViewBag.ActualPage = page;
                ViewBag.MinPage = result.minPage;
                ViewBag.MaxPage = result.maxPage;
                ViewBag.Pages = pages;
                return PartialView("_PVStockTbl", lstStock);
            }
            catch (Exception ex)
            {
                return Json(HomeController.ShowAlert("danger", "Ha sucedido un error, favor de intentarlo más tarde: <br>" + ex.ToString()));
            }
        }

        public int CountPages(string msKey, string description, string inspection, int storage, int groupLine, int supplyLine, int rawMaterial)
        {
            int pages = 0;
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Supply_Count", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@microsipKey", SqlDbType.VarChar).Value = string.IsNullOrEmpty(msKey) ? DBNull.Value : msKey.Trim();
                cmd.Parameters.Add("@description", SqlDbType.VarChar).Value = string.IsNullOrEmpty(description) ? DBNull.Value : description.Trim();
                cmd.Parameters.Add("@inspection", SqlDbType.VarChar).Value = string.IsNullOrEmpty(inspection) ? DBNull.Value : inspection;
                cmd.Parameters.Add("@storage", SqlDbType.Int).Value = storage == 0 ? DBNull.Value : storage;
                cmd.Parameters.Add("@groupSupplyLine", SqlDbType.Int).Value = groupLine == 0 ? DBNull.Value : groupLine;
                cmd.Parameters.Add("@supplyLine", SqlDbType.Int).Value = supplyLine == 0 ? DBNull.Value : supplyLine;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial == 2 ? DBNull.Value : rawMaterial;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    decimal temp = (Convert.ToDecimal(rd["conteo"]) / 30);
                    pages = Convert.ToInt32(Math.Ceiling(temp));
                }
            }
            return pages;
        }

        private Supply_ViewModel SelectOne(int idSupply)
        {
            var supplyModel = new Supply_ViewModel();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Supply_SelByIdSupply", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;
                using (var rd = cmd.ExecuteReader())
                {
                    rd.Read();
                    supplyModel.IdSupply = Convert.ToInt32(rd["idSupply"]);
                    supplyModel.Description = Convert.ToString(rd["description"]);
                    supplyModel.MicrosipKey = Convert.ToString(rd["microsipKey"]);
                    supplyModel.UnitType = Convert.ToString(rd["unitType"]);
                    supplyModel.Inspection = Convert.ToInt32(rd["inspection"]);
                    supplyModel.General = float.Parse(rd["general"].ToString());
                    supplyModel.Herramienta = float.Parse(rd["herramienta"].ToString());
                    supplyModel.Consignacion = float.Parse(rd["consignacion"].ToString());
                    supplyModel.Habilitado = float.Parse(rd["habilitado"].ToString());
                    supplyModel.Mp = float.Parse(rd["mp"].ToString());
                    supplyModel.Importacion = float.Parse(rd["importacion"].ToString());
                    supplyModel.Mty = float.Parse(rd["mty"].ToString());
                    supplyModel.QtyTotal = float.Parse(rd["qtyTotal"].ToString());
                    supplyModel.RawMaterial = Convert.ToInt32(rd["rawMaterial"]);
                    supplyModel.SupplyLine = Convert.ToString(rd["supplyLine"]);
                }
            }
            return supplyModel;
        }

        // Actualizar el método ListSupply existente en StockController
        [HttpGet]
        public JsonResult ListSupply(int idGroupSupplyLine, int idSupplyLine, int rawMaterial)
        {
            var lst = new List<object>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();

                // Primero obtener artículos normales (NO de consignación)
                var cmd = new SqlCommand("VoucherRequest.sp_Supply_SelForList", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@groupSupplyLine", SqlDbType.Int).Value = idGroupSupplyLine == 0 ? DBNull.Value : idGroupSupplyLine;
                cmd.Parameters.Add("@supplyLine", SqlDbType.Int).Value = idSupplyLine == 0 ? DBNull.Value : idSupplyLine;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        // Solo agregar si NO es de consignación
                        int idSupply = Convert.ToInt32(rd["idSupply"]);
                        bool isConsignment = IsConsignmentItem(idSupply);

                        if (!isConsignment)
                        {
                            lst.Add(new
                            {
                                idSupply = idSupply,
                                description = rd["description"].ToString(),
                                unitType = rd["unitType"].ToString(),
                                qtyTotal = float.Parse(rd["qtyTotal"].ToString()),
                                idSupplySurplus = Convert.ToInt32(rd["idSupplySurplus"]),
                                microsipkey = rd["microsipKey"].ToString(),
                                isConsignment = false
                            });
                        }
                    }
                }

                // Luego obtener artículos de consignación activos
                var cmdConsignment = new SqlCommand("VoucherRequest.sp_ConsignmentItems_ForList", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using (var rd = cmdConsignment.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lst.Add(new
                        {
                            idSupply = Convert.ToInt32(rd["idSupply"]),
                            description = rd["description"].ToString(),
                            unitType = rd["unitType"].ToString(),
                            qtyTotal = Convert.ToDecimal(rd["currentStock"]),
                            idSupplySurplus = 0,
                            microsipkey = rd["microsipKey"].ToString(),
                            isConsignment = true
                        });
                    }
                }

                cnn.Close();
            }

            // Ordenar por descripción
            lst = lst.OrderBy(x => x.GetType().GetProperty("description").GetValue(x, null).ToString()).ToList();

            return Json(lst);
        }
        // Método auxiliar para verificar si un artículo es de consignación
        private bool IsConsignmentItem(int idSupply)
        {
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("SELECT ISNULL(IsConsignment, 0) FROM VoucherRequest.ConsignmentItems WHERE IdSupply = @idSupply", cnn);
                cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;

                var result = cmd.ExecuteScalar();
                cnn.Close();

                return result != null && Convert.ToBoolean(result);
            }
        }
        [HttpGet]
        public JsonResult ListSupplySurplus()
        {
            var lst = new List<object>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from VoucherRequest.vw_SupplySurplus_List order by description", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lst.Add(new
                        {
                            idSupply = Convert.ToInt32(rd["idSupply"]),
                            description = rd["description"].ToString(),
                            unitType = rd["unitType"].ToString(),
                            qtyTotal = float.Parse(rd["qtyTotal"].ToString()),
                            idSupplySurplus = Convert.ToInt32(rd["idSupplySurplus"]),
                            microsipkey = rd["microsipKey"].ToString()
                        });
                    }
                }
                cnn.Close();
            }
            return Json(lst);
        }

        public static List<SelectListItem> ListGroupSupplyLine(string connection, int voucherType)
        {
            var items = new List<SelectListItem>();
            using (var cnn = new SqlConnection(connection))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_GroupSupplyLine_Sel", cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = voucherType == 0 ? DBNull.Value : voucherType;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        items.Add(new SelectListItem()
                        {
                            Text = rd["name"].ToString(),
                            Value = rd["idGroupSupplyLine"].ToString(),
                            Selected = false
                        });
                    }
                }
                cnn.Close();
            }
            items.Insert(0, new SelectListItem()
            {
                Text = "-- Seleccionar grupo --",
                Value = "0",
                Selected = true
            });
            return items;
        }

        public List<SupplyLine_Model> ListSupplyLine(int idGroupSupplyLine, int rawMaterial)
        {
            var lstSupplyLine = new List<SupplyLine_Model>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_SupplyLine_Sel", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idGroupSupplyLine", SqlDbType.Int).Value = idGroupSupplyLine == 0 ? DBNull.Value : idGroupSupplyLine;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstSupplyLine.Add(new SupplyLine_Model()
                        {
                            IdSupplyLine = Convert.ToInt32(rd["idSupplyLine"]),
                            Name = rd["name"].ToString()
                        });
                    }
                }
                cnn.Close();
            }
            return lstSupplyLine;
        }
        // Agregar este método al StockController existente

        [HttpGet]
        public JsonResult ListSupplyWithConsignment(int idGroupSupplyLine, int idSupplyLine, int rawMaterial)
        {
            var lst = new List<object>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();

                // Primero obtener artículos normales
                var cmd = new SqlCommand("VoucherRequest.sp_Supply_SelForList", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@groupSupplyLine", SqlDbType.Int).Value = idGroupSupplyLine == 0 ? DBNull.Value : idGroupSupplyLine;
                cmd.Parameters.Add("@supplyLine", SqlDbType.Int).Value = idSupplyLine == 0 ? DBNull.Value : idSupplyLine;
                cmd.Parameters.Add("@rawMaterial", SqlDbType.Bit).Value = rawMaterial;

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lst.Add(new
                        {
                            idSupply = Convert.ToInt32(rd["idSupply"]),
                            description = rd["description"].ToString(),
                            unitType = rd["unitType"].ToString(),
                            qtyTotal = float.Parse(rd["qtyTotal"].ToString()),
                            idSupplySurplus = Convert.ToInt32(rd["idSupplySurplus"]),
                            microsipkey = rd["microsipKey"].ToString(),
                            isConsignment = false
                        });
                    }
                }

                // Luego obtener artículos de consignación
                var cmdConsignment = new SqlCommand("VoucherRequest.sp_ConsignmentItems_ForList", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using (var rd = cmdConsignment.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lst.Add(new
                        {
                            idSupply = Convert.ToInt32(rd["idSupply"]),
                            description = rd["description"].ToString(),
                            unitType = rd["unitType"].ToString(),
                            qtyTotal = Convert.ToDecimal(rd["currentStock"]),
                            idSupplySurplus = 0,
                            microsipkey = rd["microsipKey"].ToString(),
                            isConsignment = true
                        });
                    }
                }

                cnn.Close();
            }

            // Ordenar por descripción
            lst = lst.OrderBy(x => x.GetType().GetProperty("description").GetValue(x, null).ToString()).ToList();

            return Json(lst);
        }
        private List<SelectListItem> ListStorage()
        {
            var items = new List<SelectListItem>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from VoucherRequest.vw_Storage_List order by name", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        items.Add(new SelectListItem()
                        {
                            Text = rd["name"].ToString(),
                            Value = rd["idStorage"].ToString(),
                            Selected = false
                        });
                    }
                }
                cnn.Close();
            }
            items.Insert(0, new SelectListItem()
            {
                Text = "-- Seleccionar almacen --",
                Value = "0",
                Selected = true
            });
            return items;
        }
    }
}