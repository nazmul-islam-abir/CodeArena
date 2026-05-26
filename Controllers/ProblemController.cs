using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;

namespace MyMvcApp.Controllers
{
    public class ProblemController : Controller
    {
        private readonly AppDbContext _context;

        public ProblemController(AppDbContext context)
        {
            _context = context;
        }

        // Helper: get IDs of problems whose contest has ended (so they become public)
        private async Task<HashSet<int>> GetPublicizedProblemIdsAsync()
        {
            var now = DateTime.UtcNow;
            var ids = await _context.ContestProblems
                .Where(cp => _context.Contests.Any(c => c.Id == cp.ContestId && c.EndTime <= now))
                .Select(cp => cp.ProblemId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var isAdmin = User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin");

                var problems = await _context.Problems
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Id)
                    .ToListAsync();

                if (!isAdmin)
                {
                    var publicizedIds = await GetPublicizedProblemIdsAsync();
                    problems = problems.Where(p => !p.IsPrivate || publicizedIds.Contains(p.Id)).ToList();
                }

                return View(problems);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading problems: " + ex.Message;
                return View(new List<Problem>());
            }
        }

        // This handles /Problem/{id} as details view
        [Route("Problem/{id:int}")]
        public async Task<IActionResult> Index(int id, int? contestId = null)
        {
            try
            {
                var isAdmin = User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin");

                var problem = await _context.Problems
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (problem == null)
                {
                    return NotFound();
                }

                // Block non-admins from accessing private problems that haven't been publicized
                if (!isAdmin && problem.IsPrivate)
                {
                    var publicizedIds = await GetPublicizedProblemIdsAsync();
                    if (!publicizedIds.Contains(problem.Id))
                    {
                        return NotFound();
                    }
                }

                ViewBag.ContestId = contestId;
                return View("Details", problem);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading problem: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Solve(int id, int? contestId = null)
        {
            // Check if user is logged in
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Please login to solve problems.";
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var isAdmin = User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin");

                var problem = await _context.Problems
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (problem == null)
                {
                    return NotFound();
                }

                // Allow access if solving via contest (contestId provided), admin, or problem is public/publicized
                if (!isAdmin && problem.IsPrivate && contestId == null)
                {
                    var publicizedIds = await GetPublicizedProblemIdsAsync();
                    if (!publicizedIds.Contains(problem.Id))
                    {
                        return NotFound();
                    }
                }

                // Store problem info in TempData to pass to compiler
                TempData["ProblemId"] = id;
                TempData["ContestId"] = contestId;
                TempData["ProblemTitle"] = problem.Title;
                TempData["ProblemDescription"] = problem.Description;
                TempData["ProblemSampleInput"] = problem.SampleInput;
                TempData["ProblemSampleOutput"] = problem.SampleOutput;
                TempData["ProblemTimeLimit"] = problem.TimeLimit;
                TempData["ProblemMemoryLimit"] = problem.MemoryLimit;

                // Redirect to compiler with problem info
                return RedirectToAction("Index", "Compiler", new { problemId = id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading problem: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}