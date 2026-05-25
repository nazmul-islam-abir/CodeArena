using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Services;
using System.Threading.Tasks;
using System.Linq;

namespace MyMvcApp.Controllers.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Submissions")]
    public class AdminSubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly JudgeService _judgeService;

        public AdminSubmissionController(AppDbContext context, JudgeService judgeService)
        {
            _context = context;
            _judgeService = judgeService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var totalItems = await _context.Submissions.CountAsync();
            var submissions = await _context.Submissions
                .OrderByDescending(s => s.SubmittedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalItems / pageSize);
            
            return View("~/Views/Admin/Submission/Index.cshtml", submissions);
        }

        [HttpPost("Rejudge/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rejudge(int id)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission != null)
            {
                submission.Verdict = "Processing";
                await _context.SaveChangesAsync();
                
                // Fire and forget
                _ = Task.Run(() => _judgeService.RejudgeSubmissionAsync(id));
                
                TempData["SuccessMessage"] = $"Submission {id} has been queued for rejudging.";
            }
            else
            {
                TempData["ErrorMessage"] = "Submission not found.";
            }
            
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission == null) return NotFound();
            
            return View("~/Views/Admin/Submission/Details.cshtml", submission);
        }
    }
}
