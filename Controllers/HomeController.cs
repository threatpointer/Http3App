using Microsoft.AspNetCore.Mvc;
using Http3App.Middleware; // Ensure this matches the namespace of your middleware

namespace Http3App.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewData["ActiveConnections"] = ConnectionLogMiddleware.GetActiveConnections();
            ViewData["StaleConnections"] = ConnectionLogMiddleware.GetStaleConnections();
            return View();
        }
    }
}
