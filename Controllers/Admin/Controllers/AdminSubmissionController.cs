using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Services;
using System.Threading.Tasks;
using System.Linq;

namespace MyMvcApp.Controllers.Admin.Controllers
{
    [Authorize(Roles = "Admin,Setter")]
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
        public async Task<IActionResult> Index(int? problemId = null, int? userId = null, int? contestId = null, bool? showPending = null)
        {
            var query = _context.Submissions
                .OrderByDescending(s => s.SubmittedAt)
                .AsQueryable();

            if (problemId.HasValue)
                query = query.Where(s => s.ProblemId == problemId.Value);

            if (userId.HasValue)
                query = query.Where(s => s.UserId == userId.Value);

            if (contestId.HasValue)
                query = query.Where(s => s.ContestId == contestId.Value);

            if (showPending.HasValue && showPending.Value)
                query = query.Where(s => s.Verdict == "Processing" || s.Verdict == "Queued");

            var submissions = await query
                .Take(200)
                .ToListAsync();

            var pendingCount = await _context.Submissions
                .CountAsync(s => s.Verdict == "Processing" || s.Verdict == "Queued");
            ViewBag.PendingCount = pendingCount;

            return View("~/Views/Submissions/Index.cshtml", submissions);
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
            
            bool isPending = submission.Verdict == "Processing" || submission.Verdict == "Queued" || string.IsNullOrEmpty(submission.Verdict);
            if (isPending)
            {
                ViewBag.IsPending = true;
                ViewBag.AutoRefresh = true;
            }
            
            return View("~/Views/Submissions/Details.cshtml", submission);
        }
    }
}

