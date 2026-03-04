using Microsoft.AspNetCore.Mvc;

namespace MyMvcApp.Controllers;

public class RankingController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}