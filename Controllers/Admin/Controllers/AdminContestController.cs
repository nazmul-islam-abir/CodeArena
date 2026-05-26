using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using System.Threading.Tasks;
using System.Linq;

namespace MyMvcApp.Controllers.Admin.Controllers
{
    [Authorize(Roles = "Admin,Setter")]
    [Route("Admin/Contests")]
    public class AdminContestController : Controller
    {
        private readonly AppDbContext _context;

        public AdminContestController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var contests = await _context.Contests
                .OrderByDescending(c => c.StartTime)
                .ToListAsync();
            
            return View("~/Views/Admin/Contest/Index.cshtml", contests);
        }
    }
}

