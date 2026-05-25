using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using System.Threading.Tasks;

namespace MyMvcApp.Controllers.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly AppDbContext _context;

        public AdminDashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Index()
        {
            var stats = new
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalProblems = await _context.Problems.CountAsync(),
                TotalContests = await _context.Contests.CountAsync(),
                TotalSubmissions = await _context.Submissions.CountAsync()
            };

            ViewBag.Stats = stats;
            return View("~/Views/Admin/Dashboard/Index.cshtml");
        }
    }
}
