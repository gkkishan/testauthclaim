using Microsoft.AspNetCore.Mvc;

namespace SquarUI.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult AccessDenied() => View();
}
