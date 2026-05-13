using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Data;
using Microsoft.EntityFrameworkCore;

namespace MyMvcApp.Controllers;

public class RankingController : Controller
{
    private readonly AppDbContext _context;

    public RankingController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var topUsers = await _context.Users
            .OrderByDescending(u => u.TotalPoints)
            .ThenByDescending(u => u.ProblemsSolved)
            .Take(50)
            .ToListAsync();

        return View(topUsers);
    }
}