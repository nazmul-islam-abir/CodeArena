using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;

namespace MyMvcApp.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var viewModel = new HomeViewModel
        {
            TotalActiveUsers = await _context.Users.CountAsync(),
            FeaturedProblems = await _context.Problems
                .Where(p => p.IsActive && !p.IsPrivate)
                .OrderByDescending(p => p.SolvedCount)
                .Take(3)
                .ToListAsync(),
            TopUsers = await _context.Users
                .Where(u => u.IsActive && u.Role == "User")
                .OrderByDescending(u => u.TotalPoints)
                .Take(3)
                .ToListAsync()
        };

        return View(viewModel);
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
