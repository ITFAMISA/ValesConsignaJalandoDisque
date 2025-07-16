using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VoucherCapture.Models;

namespace VoucherCapture.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View("Login");
        }

        public static string ShowAlert(string color, string message)
        {
            string icon = null;
            switch (color)
            {
                case "success":
                    icon = "bi-check-circle";
                    break;
                case "danger":
                    icon = "bi-exclamation-triangle";
                    break;
                case "warning":
                    icon = "bi-info-square";
                    break;
            }
            string alertDiv = "<div class='alert alert-" + color + " alert-dismissible fade show' role='alert'> <i class=\"bi " + icon + "\"></i> " + message + "<button type= 'button' class='btn-close' data-bs-dismiss='alert' aria-label='Close'></button></div>";
            return alertDiv;
        }

        public static (int minPage, int maxPage) ControlPages(int actualPage, int totalPages)
        {
            int min = Math.Max(1, actualPage - 12 / 2);
            int max = Math.Min(totalPages, actualPage + 12 / 2 + Math.Max(0, min - actualPage + 12 / 2));
            return (min, max);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        
    }
}
