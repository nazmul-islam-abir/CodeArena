using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;
using MyMvcApp.Services;

namespace MyMvcApp.Controllers
{
    public class SubmissionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;

        public SubmissionsController(AppDbContext context, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
        }

        // This handles /Submissions (list view)
        public async Task<IActionResult> Index(int? problemId = null, int? userId = null, int? contestId = null, bool? showPending = null)
        {
            try
            {
                var query = _context.Submissions
                    .OrderByDescending(s => s.SubmittedAt)
                    .AsQueryable();

                if (problemId.HasValue)
                {
                    query = query.Where(s => s.ProblemId == problemId.Value);
                }

                if (userId.HasValue)
                {
                    query = query.Where(s => s.UserId == userId.Value);
                }

                if (contestId.HasValue)
                {
                    query = query.Where(s => s.ContestId == contestId.Value);
                }

                if (showPending.HasValue && showPending.Value)
                {
                    query = query.Where(s => s.Verdict == "Processing" || s.Verdict == "Queued");
                }

                var submissions = await query
                    .Take(200)
                    .Select(s => new Submission
                    {
                        Id = s.Id,
                        UserId = s.UserId,
                        ProblemId = s.ProblemId,
                        LanguageId = s.LanguageId,
                        LanguageName = s.LanguageName,
                        SourceCode = s.SourceCode,
                        Verdict = s.Verdict,
                        ExecutionTime = s.ExecutionTime,
                        MemoryUsed = s.MemoryUsed,
                        SubmittedAt = s.SubmittedAt,
                        ContestId = s.ContestId,
                        TestCasesPassed = s.TestCasesPassed,
                        TotalTestCases = s.TotalTestCases,
                        UserName = s.UserName,
                        ProblemTitle = s.ProblemTitle,
                        UniqueId = s.UniqueId,
                        ErrorMessage = s.ErrorMessage
                    })
                    .ToListAsync();

                // For admin view, also show pending count
                if (User.IsInRole("Admin"))
                {
                    var pendingCount = await _context.Submissions
                        .CountAsync(s => s.Verdict == "Processing" || s.Verdict == "Queued");
                    ViewBag.PendingCount = pendingCount;
                }

                return View(submissions);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading submissions: " + ex.Message;
                return View(new List<Submission>());
            }
        }

        // This handles /Submissions/my (my submissions)
        public IActionResult My()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("Login", "Auth");

            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            return RedirectToAction("Index", new { userId = userId });
        }

        // This handles /Submissions/{id} as details view
        [Route("Submissions/{id:int}")]
        public async Task<IActionResult> Index(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid submission ID.";
                return RedirectToAction("Index");
            }

            try
            {
                var submission = await _context.Submissions
                    .Where(s => s.Id == id)
                    .Select(s => new Submission
                    {
                        Id = s.Id,
                        UserId = s.UserId,
                        ProblemId = s.ProblemId,
                        LanguageId = s.LanguageId,
                        LanguageName = s.LanguageName,
                        SourceCode = s.SourceCode,
                        Verdict = s.Verdict,
                        ExecutionTime = s.ExecutionTime,
                        MemoryUsed = s.MemoryUsed,
                        SubmittedAt = s.SubmittedAt,
                        ContestId = s.ContestId,
                        TestCasesPassed = s.TestCasesPassed,
                        TotalTestCases = s.TotalTestCases,
                        UserName = s.UserName,
                        ProblemTitle = s.ProblemTitle,
                        UniqueId = s.UniqueId,
                        ErrorMessage = s.ErrorMessage
                    })
                    .FirstOrDefaultAsync();

                if (submission == null)
                {
                    TempData["ErrorMessage"] = $"Submission #{id} not found.";
                    return RedirectToAction("Index");
                }

                bool isPending = submission.Verdict == "Processing" || submission.Verdict == "Queued" || string.IsNullOrEmpty(submission.Verdict);

                if (isPending)
                {
                    ViewBag.IsPending = true;
                    ViewBag.AutoRefresh = true;
                }

                return View("Details", submission);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading submission: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ADMIN: Delete a submission
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var submission = await _context.Submissions.FindAsync(id);
                if (submission == null)
                {
                    TempData["ErrorMessage"] = "Submission not found.";
                    return RedirectToAction("Index");
                }

                _context.Submissions.Remove(submission);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Submission #{id} has been deleted.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting submission: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ADMIN: Re-judge (resubmit) a submission
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Rejudge(int id)
        {
            try
            {
                var submission = await _context.Submissions.FindAsync(id);
                if (submission == null)
                {
                    TempData["ErrorMessage"] = "Submission not found.";
                    return RedirectToAction("Index");
                }

                // Reset verdict to Processing
                submission.Verdict = "Processing";
                submission.ErrorMessage = null;
                submission.ExecutionTime = null;
                submission.MemoryUsed = null;
                submission.TestCasesPassed = null;
                submission.TotalTestCases = null;
                await _context.SaveChangesAsync();

                // Start background processing again
                _ = Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var judgeService = scope.ServiceProvider.GetRequiredService<JudgeService>();
                    // This will trigger re-processing
                    await judgeService.RejudgeSubmissionAsync(submission.Id);
                });

                TempData["SuccessMessage"] = $"Submission #{id} has been queued for re-judging.";
                return RedirectToAction("Index", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error re-judging submission: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ADMIN: Delete all pending submissions
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ClearAllPending()
        {
            try
            {
                var pendingSubmissions = await _context.Submissions
                    .Where(s => s.Verdict == "Processing" || s.Verdict == "Queued")
                    .ToListAsync();

                int count = pendingSubmissions.Count;
                _context.Submissions.RemoveRange(pendingSubmissions);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cleared {count} pending submission(s).";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error clearing submissions: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}