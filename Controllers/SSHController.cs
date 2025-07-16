using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using System.Data;
using System.Data.SqlClient;
using System.Text;


namespace VoucherCapture.Controllers
{
    [Authorize]
    public class SSHController : Controller
    {
        private readonly string remoteHost = "216.238.83.71";
        private readonly string remoteUser = "Vales";
        private readonly string remotePassword = "famisa123.";
        private readonly string remotePath = @"C:\Users\Sistema\Desktop\ArtiData\MudVoucherSys.exe";

        private readonly string localHost = "192.168.1.134";
        private readonly string localUser = "serv system";
        private readonly string localPassword = "f4m1s@24.";
        private readonly string localPath = @"C:\Users\Serv System\Desktop\DownloadAD\DownloadMicrosipData.exe";
        private readonly IConfiguration _configuration;

        public SSHController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<JsonResult> ExecuteProcess()
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos para ejecutar esta acción" });
            }

            try
            {
                using var client = new SshClient(remoteHost, remoteUser, remotePassword);
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    return Json(new { success = false, message = "No se pudo conectar al servidor SSH" });
                }

                var command = client.CreateCommand($"cmd /c \"cd /d C:\\Users\\Sistema\\Desktop\\ArtiData && start /wait MudVoucherSys.exe\"");
                var result = await Task.Run(() => command.Execute());

                var checkCommand = client.CreateCommand("tasklist /FI \"IMAGENAME eq MudVoucherSys.exe\"");
                var processCheck = await Task.Run(() => checkCommand.Execute());

                client.Disconnect();

                return Json(new
                {
                    success = true,
                    message = "Proceso ejecutado",
                    output = result,
                    processRunning = processCheck.Contains("MudVoucherSys.exe"),
                    exitStatus = command.ExitStatus
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error al ejecutar proceso: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<JsonResult> CheckProcessStatus()
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos" });
            }

            try
            {
                using var client = new SshClient(remoteHost, remoteUser, remotePassword);
                await Task.Run(() => client.Connect());

                var command = client.CreateCommand("tasklist /FI \"IMAGENAME eq MudVoucherSys.exe\"");
                var result = await Task.Run(() => command.Execute());

                client.Disconnect();

                bool isRunning = result.Contains("MudVoucherSys.exe");

                return Json(new
                {
                    success = true,
                    isRunning = isRunning,
                    message = isRunning ? "Proceso ejecutándose" : "Proceso terminado"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> CheckConnection()
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos" });
            }

            try
            {
                using var client = new SshClient(remoteHost, remoteUser, remotePassword);
                await Task.Run(() => client.Connect());

                bool isConnected = client.IsConnected;
                client.Disconnect();

                return Json(new { success = isConnected, message = isConnected ? "Conexión exitosa" : "No se pudo conectar" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> ExecuteDownloadProcess()
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos para ejecutar esta acción" });
            }

            try
            {
                using var client = new SshClient(localHost, localUser, localPassword);
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    return Json(new { success = false, message = "No se pudo conectar al servidor local" });
                }

                var command = client.CreateCommand($"cmd /c \"cd /d \"C:\\Users\\Serv System\\Desktop\\DownloadAD\" && start /wait DownloadMicrosipData.exe\"");
                var result = await Task.Run(() => command.Execute());

                var checkCommand = client.CreateCommand("tasklist /FI \"IMAGENAME eq DownloadMicrosipData.exe\"");
                var processCheck = await Task.Run(() => checkCommand.Execute());

                client.Disconnect();

                return Json(new
                {
                    success = true,
                    message = "Proceso DownloadMicrosipData ejecutado",
                    output = result,
                    processRunning = processCheck.Contains("DownloadMicrosipData.exe"),
                    exitStatus = command.ExitStatus
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error al ejecutar DownloadMicrosipData: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<JsonResult> CheckDownloadProcessStatus()
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos" });
            }

            try
            {
                using var client = new SshClient(localHost, localUser, localPassword);
                await Task.Run(() => client.Connect());

                var command = client.CreateCommand("tasklist /FI \"IMAGENAME eq DownloadMicrosipData.exe\"");
                var result = await Task.Run(() => command.Execute());

                client.Disconnect();

                bool isRunning = result.Contains("DownloadMicrosipData.exe");

                return Json(new
                {
                    success = true,
                    isRunning = isRunning,
                    message = isRunning ? "DownloadMicrosipData ejecutándose" : "DownloadMicrosipData terminado"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<JsonResult> ExecuteSQLJob(string jobName)
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos" });
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("dbConnection");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var command = new SqlCommand("EXEC msdb.dbo.sp_start_job @job_name = @jobName", connection);
                command.Parameters.AddWithValue("@jobName", jobName);
                await command.ExecuteNonQueryAsync();

                return Json(new { success = true, message = "Job iniciado exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<JsonResult> CheckSQLJobStatus(string jobName)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("dbConnection");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var command = new SqlCommand(@"
            SELECT job.name, activity.run_status
            FROM msdb.dbo.sysjobs job
            LEFT JOIN msdb.dbo.sysjobactivity activity ON job.job_id = activity.job_id
            WHERE job.name = @jobName
            AND activity.session_id = (SELECT MAX(session_id) FROM msdb.dbo.sysjobactivity)", connection);

                command.Parameters.AddWithValue("@jobName", jobName);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int status = reader.IsDBNull("run_status") ? -1 : reader.GetInt32("run_status");
                    string message = status switch
                    {
                        0 => "Job falló",
                        1 => "Job completado",
                        4 => "Job ejecutándose",
                        _ => "Estado desconocido"
                    };

                    return Json(new { success = true, isRunning = status == 4, message });
                }

                return Json(new { success = false, message = "Job no encontrado" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<JsonResult> ExecuteUploadProcess()
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Almacen") && !User.IsInRole("AlmacenMP"))
            {
                return Json(new { success = false, message = "No tiene permisos para ejecutar esta acción" });
            }

            try
            {
                using var client = new SshClient(remoteHost, remoteUser, remotePassword);
                await Task.Run(() => client.Connect());

                if (!client.IsConnected)
                {
                    return Json(new { success = false, message = "No se pudo conectar al servidor remoto" });
                }

                // Ejecutar con entrada automática de dos enters
                var command = client.CreateCommand($"cmd /c \"cd /d C:\\Users\\Administrador\\Downloads\\InsertVoucher && echo. && echo. | UploadMicrosipData.exe\"");
                var result = await Task.Run(() => command.Execute());

                client.Disconnect();

                return Json(new
                {
                    success = true,
                    message = "Proceso UploadMicrosipData completado",
                    output = result,
                    exitStatus = command.ExitStatus
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


    }
}