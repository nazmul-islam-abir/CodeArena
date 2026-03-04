using Microsoft.AspNetCore.Mvc;

namespace MyMvcApp.Controllers;

public class AuthController : Controller
{
    public IActionResult Login()
    {
        return View();
    }

    public IActionResult Signup()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        // Add your login logic here later
        // For now, just redirect to home
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Signup(string firstName, string lastName, string username, string email, string password)
    {
        // Add your signup logic here later
        // For now, just redirect to login
        return RedirectToAction("Login");
    }
}