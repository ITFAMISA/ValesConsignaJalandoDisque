using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using VoucherCapture.ViewModel;
using VoucherCapture.Models;
using Microsoft.AspNetCore.Authorization;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class StorageController : Controller
    {
        private readonly string connectionStringSQL;
        public StorageController(IConfiguration config) {
            connectionStringSQL = config.GetConnectionString("dbConnection");
        }
 
        public JsonResult ListStorage(int idSupply)
        {
            var lstStorage = new List<Storage_ViewModel>();
            using(var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("VoucherRequest.sp_Storage_SelByIdSupply", cnn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstStorage.Add(new Storage_ViewModel()
                        {
                            IdStorage = Convert.ToInt32(rd["idStorage"]),
                            Name = rd["name"].ToString(),
                            QtyTotal = float.Parse(rd["qtyTotal"].ToString())
                        });
                    }
                }
                cnn.Close();
            }
            return Json(lstStorage);
        }
        // Agregar este método al StorageController existente

        public JsonResult ListStorageWithConsignment(int idSupply)
        {
            var lstStorage = new List<Storage_ViewModel>();

            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();

                // Verificar si es artículo de consignación
                var cmdCheck = new SqlCommand("SELECT ISNULL(IsConsignment, 0), ISNULL(CurrentStock, 0) FROM VoucherRequest.ConsignmentItems WHERE IdSupply = @idSupply", cnn);
                cmdCheck.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;

                bool isConsignment = false;
                decimal consignmentStock = 0;

                using (var rd = cmdCheck.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        isConsignment = Convert.ToBoolean(rd[0]);
                        consignmentStock = Convert.ToDecimal(rd[1]);
                    }
                }

                if (isConsignment)
                {
                    // Si es de consignación, devolver solo el stock de consignación
                    lstStorage.Add(new Storage_ViewModel()
                    {
                        IdStorage = 999, // ID especial para consignación
                        Name = "Almacén Consignación",
                        QtyTotal = (float)consignmentStock
                    });
                }
                else
                {
                    // Si es normal, usar el procedimiento existente
                    var cmd = new SqlCommand("VoucherRequest.sp_Storage_SelByIdSupply", cnn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@idSupply", SqlDbType.Int).Value = idSupply;

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            lstStorage.Add(new Storage_ViewModel()
                            {
                                IdStorage = Convert.ToInt32(rd["idStorage"]),
                                Name = rd["name"].ToString(),
                                QtyTotal = float.Parse(rd["qtyTotal"].ToString())
                            });
                        }
                    }
                }

                cnn.Close();
            }

            return Json(lstStorage);
        }
        public JsonResult ListConcepts()
        {
            var lstConcept = new List<Concept_Model>();
            using (var cnn = new SqlConnection(connectionStringSQL))
            {
                cnn.Open();
                var cmd = new SqlCommand("Select * from VoucherRequest.vw_Concept_List", cnn);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        lstConcept.Add(new Concept_Model()
                        {
                            IdConcept = Convert.ToInt32(rd["idConcept"]),
                            Description = rd["description"].ToString()
                        });
                    }
                }
                cnn.Close();
            }
            return Json(lstConcept);
        }
    }
}
