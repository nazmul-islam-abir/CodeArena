// Controllers/ContestController.cs - Simplified version without IHttpContextAccessor

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MyMvcApp.Models;
using MyMvcApp.Data;
using System.Security.Claims;

namespace MyMvcApp.Controllers
{
    [Route("Contest")]   // ← ADDED: Base route for all actions
    public class ContestController : Controller
    {
        private readonly AppDbContext _context;

        public ContestController(AppDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId") ??
                           User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdStr, out int id) ? id : (int?)null;
        }

        private string GetCurrentUsername()
        {
            return HttpContext.Session.GetString("Username") ??
                   User.Identity?.Name ?? string.Empty;
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role == "Admin" || User.IsInRole("Admin");
        }

        // GET: /Contest
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var contests = await _context.Contests
                .Where(c => !c.IsDeleted && c.IsActive)
                .OrderByDescending(c => c.StartTime)
                .ToListAsync();

            var username = GetCurrentUsername();
            var registeredContestIds = new HashSet<int>();

            if (!string.IsNullOrEmpty(username))
            {
                registeredContestIds = (await _context.ContestRegistrations
                    .Where(r => r.UserName == username)
                    .Select(r => r.ContestId)
                    .ToListAsync()).ToHashSet();
            }

            foreach (var contest in contests)
            {
                contest.ParticipantCount = await _context.ContestRegistrations
                    .CountAsync(r => r.ContestId == contest.Id && r.Status == "Approved");
                contest.ProblemCount = await _context.ContestProblems
                    .CountAsync(cp => cp.ContestId == contest.Id);
                contest.Status = GetContestStatus(contest.StartTime, contest.EndTime);
                contest.IsRegistered = registeredContestIds.Contains(contest.Id);
            }

            return View(contests);
        }

        // GET: /Contest/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && c.IsActive);

            if (contest == null) return NotFound();

            contest.Status = GetContestStatus(contest.StartTime, contest.EndTime);
            contest.ParticipantCount = await _context.ContestRegistrations
                .CountAsync(r => r.ContestId == id && r.Status == "Approved");
            contest.ProblemCount = await _context.ContestProblems
                .CountAsync(cp => cp.ContestId == id);

            // Get problems with letters
            var contestProblems = await GetContestProblemsWithLetters(id);

            ViewBag.Problems = contestProblems;

            // Check registration
            var username = GetCurrentUsername();
            var userId = GetCurrentUserId();

            if (!string.IsNullOrEmpty(username))
            {
                contest.IsRegistered = await _context.ContestRegistrations
                    .AnyAsync(r => r.ContestId == id && r.UserName == username && r.Status == "Approved");
            }

            // Get user's solved status for each problem
            if (contest.IsRegistered && userId.HasValue)
            {
                var solvedStatus = new Dictionary<string, bool>();
                foreach (var cp in contestProblems)
                {
                    var solved = await _context.Submissions
                        .AnyAsync(s => s.ContestId == id &&
                                      s.ProblemId == cp.ProblemId &&
                                      s.UserId == userId.Value &&
                                      s.Verdict == "AC");
                    solvedStatus[cp.Letter] = solved;
                }
                ViewBag.SolvedStatus = solvedStatus;
            }

            return View(contest);
        }

        // GET: /Contest/{id}/Problem/{letter}
        [HttpGet("{id:int}/Problem/{letter}")]
        public async Task<IActionResult> Problem(int id, string letter)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && c.IsActive);

            if (contest == null) return NotFound();

            var isContestActive = DateTime.UtcNow >= contest.StartTime && DateTime.UtcNow <= contest.EndTime;
            var isPractice = DateTime.UtcNow > contest.EndTime;
            var username = GetCurrentUsername();
            var userId = GetCurrentUserId();

            // Check if user can access
            var isRegistered = !string.IsNullOrEmpty(username) && await _context.ContestRegistrations
                .AnyAsync(r => r.ContestId == id && r.UserName == username && r.Status == "Approved");

            if (isContestActive && !isRegistered && !IsAdmin())
            {
                TempData["ErrorMessage"] = "You must be registered for this contest to solve problems.";
                return RedirectToAction("Details", new { id });
            }

            // Get problem by letter
            var contestProblems = await GetContestProblemsWithLetters(id);

            var contestProblem = contestProblems.FirstOrDefault(cp => cp.Letter == letter.ToUpper());
            if (contestProblem?.Problem == null) return NotFound();

            // Check if already solved (during contest, prevent resubmit for points)
            if (isContestActive && userId.HasValue)
            {
                var alreadySolved = await _context.Submissions
                    .AnyAsync(s => s.ContestId == id &&
                                  s.ProblemId == contestProblem.ProblemId &&
                                  s.UserId == userId.Value &&
                                  s.Verdict == "AC");

                if (alreadySolved)
                {
                    TempData["WarningMessage"] = "You have already solved this problem. No additional points will be awarded.";
                }
            }

            // Store contest context for the compiler
            TempData["ContestId"] = id;
            TempData["ContestTitle"] = contest.Title;
            TempData["ContestLetter"] = letter;
            TempData["ContestPoints"] = contestProblem.Points;

            // Store problem info
            TempData["ProblemId"] = contestProblem.ProblemId;
            TempData["ProblemTitle"] = contestProblem.Problem.Title;
            TempData["ProblemDescription"] = contestProblem.Problem.Description;
            TempData["ProblemSampleInput"] = contestProblem.Problem.SampleInput;
            TempData["ProblemSampleOutput"] = contestProblem.Problem.SampleOutput;

            ViewBag.Contest = contest;
            ViewBag.Letter = letter;
            ViewBag.IsContestActive = isContestActive;
            ViewBag.IsPractice = isPractice;
            ViewBag.Points = contestProblem.Points;

            return View("Problem", contestProblem.Problem);
        }

        // POST: /Contest/Register
        [HttpPost("Register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int contestId, string? password = null)
        {
            var username = GetCurrentUsername();
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(username) || !userId.HasValue)
            {
                TempData["ErrorMessage"] = "Please login to register for contests.";
                return RedirectToAction("Login", "Auth");
            }

            var contest = await _context.Contests.FindAsync(contestId);
            if (contest == null)
            {
                TempData["ErrorMessage"] = "Contest not found.";
                return RedirectToAction("Index");
            }

            // Check if registration is still open
            if (DateTime.UtcNow > contest.StartTime)
            {
                TempData["ErrorMessage"] = "Registration closed. Contest has already started.";
                return RedirectToAction("Details", new { id = contestId });
            }

            // Check password for private contests
            if (contest.IsPrivate && !string.IsNullOrEmpty(contest.Password))
            {
                if (string.IsNullOrEmpty(password) || contest.Password != password)
                {
                    TempData["ErrorMessage"] = "Invalid contest password.";
                    return RedirectToAction("Details", new { id = contestId });
                }
            }

            // Check max participants
            var currentCount = await _context.ContestRegistrations
                .CountAsync(r => r.ContestId == contestId && r.Status == "Approved");

            if (currentCount >= contest.MaxParticipants)
            {
                TempData["ErrorMessage"] = $"Registration full. Maximum {contest.MaxParticipants} participants allowed.";
                return RedirectToAction("Details", new { id = contestId });
            }

            var existing = await _context.ContestRegistrations
                .FirstOrDefaultAsync(r => r.ContestId == contestId && r.UserId == userId.Value);

            if (existing == null)
            {
                _context.ContestRegistrations.Add(new ContestRegistration
                {
                    ContestId = contestId,
                    UserId = userId.Value,
                    UserName = username,
                    RegistrationTime = DateTime.UtcNow,
                    Status = contest.RequiresApproval ? "Pending" : "Approved",
                    PasswordEntered = contest.IsPrivate ? password : null
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = contest.RequiresApproval
                    ? "Registration submitted for approval."
                    : "Successfully registered for the contest!";
            }
            else if (existing.Status == "Pending")
            {
                TempData["WarningMessage"] = "Your registration is pending approval.";
            }
            else
            {
                TempData["InfoMessage"] = "You are already registered for this contest.";
            }

            return RedirectToAction("Details", new { id = contestId });
        }

        // POST: /Contest/Unregister
        [HttpPost("Unregister")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unregister(int contestId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var contest = await _context.Contests.FindAsync(contestId);
            if (contest != null && DateTime.UtcNow > contest.StartTime)
            {
                TempData["ErrorMessage"] = "Cannot unregister after contest has started.";
                return RedirectToAction("Details", new { id = contestId });
            }

            var registration = await _context.ContestRegistrations
                .FirstOrDefaultAsync(r => r.ContestId == contestId && r.UserId == userId.Value);

            if (registration != null)
            {
                _context.ContestRegistrations.Remove(registration);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Successfully unregistered from contest.";
            }

            return RedirectToAction("Details", new { id = contestId });
        }

        // GET: /Contest/{id}/Ranking
        [HttpGet("{id:int}/Ranking")]
        public async Task<IActionResult> Ranking(int id)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (contest == null) return NotFound();

            contest.ProblemCount = await _context.ContestProblems
                .CountAsync(cp => cp.ContestId == id);

            var leaderboard = await GetLeaderboard(contest);
            var isFrozen = contest.IsFrozen;

            ViewBag.Contest = contest;
            ViewBag.IsFrozen = isFrozen;
            ViewBag.FreezeMessage = isFrozen ? "Leaderboard is frozen. Final results will be available after contest ends." : null;
            ViewBag.CurrentUserId = GetCurrentUserId();
            ViewBag.IsAdmin = IsAdmin();

            return View(leaderboard);
        }

        // GET: /Contest/{id}/Ranking/Refresh - AJAX endpoint for real-time updates
        [HttpGet("{id:int}/Ranking/Refresh")]
        public async Task<IActionResult> RefreshRanking(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null) return Json(new { error = "Contest not found" });

            var leaderboard = await GetLeaderboard(contest);
            var isFrozen = contest.IsFrozen;

            return Json(new
            {
                leaderboard = leaderboard.Select(e => new {
                    e.Rank,
                    e.UserName,
                    e.FullName,
                    e.SolvedCount,
                    e.TotalPoints,
                    e.LastSubmission,
                    e.TotalTime,
                    e.UserId,
                    e.ProblemStatuses,
                    isCurrentUser = e.UserId == GetCurrentUserId()
                }),
                isFrozen,
                lastUpdate = DateTime.Now.ToString("HH:mm:ss")
            });
        }

        // GET: /Contest/{id}/Submissions
        [HttpGet("{id:int}/Submissions")]
        public async Task<IActionResult> Submissions(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null) return NotFound();

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var submissions = await _context.Submissions
                .Where(s => s.ContestId == id && s.UserId == userId.Value)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            ViewBag.Contest = contest;
            return View(submissions);
        }

        // GET: /Contest/Join
        [HttpGet("Join")]
        public async Task<IActionResult> Join(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return RedirectToAction("Index");
            }

            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.ContestLink == link && !c.IsDeleted && c.IsActive);

            if (contest == null)
            {
                TempData["ErrorMessage"] = "Invalid or expired contest link.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("Details", new { id = contest.Id });
        }

        #region Admin Actions

        // GET: /Contest/Create
        [Authorize(Roles = "Admin")]
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var model = new ContestViewModel
            {
                Contest = new Contest
                {
                    StartTime = DateTime.Now.AddDays(7),
                    Duration = "2 hours",
                    Difficulty = "Mixed",
                    MaxParticipants = 1000,
                    ShowRankingImmediately = true
                },
                AvailableProblems = await _context.Problems.Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync()
            };
            return View(model);
        }

        // POST: /Contest/Create
        [Authorize(Roles = "Admin")]
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContestViewModel model, string? problemIds)
        {
            // Parse problem IDs
            var idList = new List<int>();
            if (!string.IsNullOrEmpty(problemIds))
            {
                idList = problemIds.Split(',')
                    .Select(id => int.TryParse(id.Trim(), out int val) ? val : 0)
                    .Where(i => i > 0)
                    .Distinct()
                    .ToList();
            }
            else if (!string.IsNullOrEmpty(model.ProblemIdsString))
            {
                idList = model.ProblemIdsString.Split(',')
                    .Select(id => int.TryParse(id.Trim(), out int val) ? val : 0)
                    .Where(i => i > 0)
                    .Distinct()
                    .ToList();
            }

            if (idList.Count == 0)
            {
                ModelState.AddModelError("", "At least one problem is required.");
                model.AvailableProblems = await _context.Problems.Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync();
                return View(model);
            }

            var existingProblems = await _context.Problems
                .Where(p => idList.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (existingProblems.Count == 0)
            {
                ModelState.AddModelError("", "No valid problems found.");
                model.AvailableProblems = await _context.Problems.Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync();
                return View(model);
            }

            var contest = model.Contest;

            // Ensure StartTime is in UTC
            if (contest.StartTime.Kind == DateTimeKind.Local)
            {
                contest.StartTime = contest.StartTime.ToUniversalTime();
            }
            else if (contest.StartTime.Kind == DateTimeKind.Unspecified)
            {
                contest.StartTime = DateTime.SpecifyKind(contest.StartTime, DateTimeKind.Local).ToUniversalTime();
            }

            contest.CreatedAt = DateTime.UtcNow;
            contest.Status = "Upcoming";
            contest.IsActive = true;
            contest.RequiresApproval = false;
            contest.ApprovalStatus = "Approved";
            contest.ContestLink = Guid.NewGuid().ToString().Substring(0, 8);

            // Calculate end time
            if (!string.IsNullOrEmpty(contest.Duration) && contest.Duration.Contains("hour"))
            {
                var hoursMatch = System.Text.RegularExpressions.Regex.Match(contest.Duration, @"\d+");
                int hours = hoursMatch.Success ? int.Parse(hoursMatch.Value) : 2;
                contest.EndTime = contest.StartTime.AddHours(hours);
            }
            else
            {
                contest.EndTime = contest.StartTime.AddHours(2);
            }

            _context.Contests.Add(contest);
            await _context.SaveChangesAsync();

            int order = 1;
            foreach (var problem in existingProblems)
            {
                _context.ContestProblems.Add(new ContestProblem
                {
                    ContestId = contest.Id,
                    ProblemId = problem.Id,
                    Order = order++,
                    Points = problem.Points
                });
            }

            // REMOVED: contest.ProblemCount = existingProblemIds.Count;
            // (ProblemCount is [NotMapped] and will be calculated on query)

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Contest '{contest.Title}' created successfully!";
            TempData["ContestLink"] = $"/Contest/Join?link={contest.ContestLink}";

            return RedirectToAction("Details", new { id = contest.Id });
        }

        // GET: /Contest/Edit/{id}
        [Authorize(Roles = "Admin")]
        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null || contest.IsDeleted) return NotFound();

            var contestProblems = await _context.ContestProblems
                .Where(cp => cp.ContestId == id)
                .OrderBy(cp => cp.Order)
                .ToListAsync();

            var model = new ContestViewModel
            {
                Contest = contest,
                SelectedProblemIds = contestProblems.Select(cp => cp.ProblemId).ToList(),
                AvailableProblems = await _context.Problems.Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync(),
                ProblemIdsString = string.Join(",", contestProblems.Select(cp => cp.ProblemId))
            };

            return View(model);
        }

        // POST: /Contest/Edit/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ContestViewModel model, string? problemIds)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null || contest.IsDeleted) return NotFound();

            // Parse problem IDs
            var idList = new List<int>();
            if (!string.IsNullOrEmpty(problemIds))
            {
                idList = problemIds.Split(',')
                    .Select(pid => int.TryParse(pid.Trim(), out int val) ? val : 0)
                    .Where(i => i > 0)
                    .Distinct()
                    .ToList();
            }

            if (idList.Count == 0 && !string.IsNullOrEmpty(model.ProblemIdsString))
            {
                idList = model.ProblemIdsString.Split(',')
                    .Select(pid => int.TryParse(pid.Trim(), out int val) ? val : 0)
                    .Where(i => i > 0)
                    .Distinct()
                    .ToList();
            }

            if (idList.Count == 0)
            {
                ModelState.AddModelError("", "At least one problem is required.");
                model.AvailableProblems = await _context.Problems.Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync();
                return View(model);
            }

            var existingProblems = await _context.Problems
                .Where(p => idList.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            // Update contest properties
            contest.Title = model.Contest.Title;
            contest.Description = model.Contest.Description;
            contest.Organizer = model.Contest.Organizer;

            // Ensure StartTime is in UTC
            var newStartTime = model.Contest.StartTime;
            if (newStartTime.Kind == DateTimeKind.Local)
            {
                newStartTime = newStartTime.ToUniversalTime();
            }
            else if (newStartTime.Kind == DateTimeKind.Unspecified)
            {
                newStartTime = DateTime.SpecifyKind(newStartTime, DateTimeKind.Local).ToUniversalTime();
            }
            contest.StartTime = newStartTime;

            contest.Difficulty = model.Contest.Difficulty;
            contest.Duration = model.Contest.Duration;
            contest.MaxParticipants = model.Contest.MaxParticipants;
            contest.ShowRankingImmediately = model.Contest.ShowRankingImmediately;
            contest.IsPrivate = model.Contest.IsPrivate;
            contest.Password = model.Contest.IsPrivate ? model.Contest.Password : string.Empty;
            contest.Rules = model.Contest.Rules;

            // Calculate end time
            if (!string.IsNullOrEmpty(contest.Duration) && contest.Duration.Contains("hour"))
            {
                var hoursMatch = System.Text.RegularExpressions.Regex.Match(contest.Duration, @"\d+");
                int hours = hoursMatch.Success ? int.Parse(hoursMatch.Value) : 2;
                contest.EndTime = contest.StartTime.AddHours(hours);
            }
            else
            {
                contest.EndTime = contest.StartTime.AddHours(2);
            }

            // Update problems
            var currentProblems = await _context.ContestProblems.Where(cp => cp.ContestId == id).ToListAsync();
            _context.ContestProblems.RemoveRange(currentProblems);

            int order = 1;
            foreach (var problem in existingProblems)
            {
                _context.ContestProblems.Add(new ContestProblem
                {
                    ContestId = id,
                    ProblemId = problem.Id,
                    Order = order++,
                    Points = problem.Points
                });
            }

            // REMOVED: contest.ProblemCount = existingProblemIds.Count;
            // (ProblemCount is [NotMapped] and will be calculated on query)

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Contest updated successfully!";
            return RedirectToAction("Details", new { id });
        }

        // POST: /Contest/Delete/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest != null)
            {
                contest.IsDeleted = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Contest deleted successfully.";
            }
            return RedirectToAction("Index");
        }

        // POST: /Contest/ToggleFreeze/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost("ToggleFreeze/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFreeze(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest != null)
            {
                contest.IsFrozen = !contest.IsFrozen;
                contest.FreezeTime = contest.IsFrozen ? DateTime.UtcNow : null;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = contest.IsFrozen
                    ? "Rankings are now frozen. Participants cannot see final standings until after contest ends."
                    : "Rankings are now unfrozen.";
            }
            return RedirectToAction("Details", new { id });
        }

        // GET: /Contest/{id}/Registrations
        [Authorize(Roles = "Admin")]
        [HttpGet("{id:int}/Registrations")]
        public async Task<IActionResult> Registrations(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null) return NotFound();

            var registrations = await _context.ContestRegistrations
                .Where(r => r.ContestId == id)
                .OrderByDescending(r => r.RegistrationTime)
                .ToListAsync();

            ViewBag.Contest = contest;
            return View(registrations);
        }

        // POST: /Contest/ApproveRegistration
        [Authorize(Roles = "Admin")]
        [HttpPost("ApproveRegistration")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistration(int contestId, int userId)
        {
            var registration = await _context.ContestRegistrations
                .FirstOrDefaultAsync(r => r.ContestId == contestId && r.UserId == userId);

            if (registration != null)
            {
                registration.Status = "Approved";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Registration for {registration.UserName} approved.";
            }

            return RedirectToAction("Registrations", new { id = contestId });
        }

        // POST: /Contest/RejectRegistration
        [Authorize(Roles = "Admin")]
        [HttpPost("RejectRegistration")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRegistration(int contestId, int userId)
        {
            var registration = await _context.ContestRegistrations
                .FirstOrDefaultAsync(r => r.ContestId == contestId && r.UserId == userId);

            if (registration != null)
            {
                _context.ContestRegistrations.Remove(registration);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Registration for {registration.UserName} rejected and removed.";
            }

            return RedirectToAction("Registrations", new { id = contestId });
        }

        #endregion

        #region Helper Methods

        private string GetContestStatus(DateTime startTime, DateTime endTime)
        {
            var now = DateTime.UtcNow;
            if (now < startTime) return "Upcoming";
            if (now <= endTime) return "Active";
            return "Ended";
        }

        private async Task<List<ContestLeaderboardEntry>> GetLeaderboard(Contest contest)
        {
            var registrations = await _context.ContestRegistrations
                .Where(r => r.ContestId == contest.Id && r.Status == "Approved")
                .ToListAsync();

            // Get user details separately
            var userIds = registrations.Select(r => r.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u);

            var contestProblems = await GetContestProblemsWithLetters(contest.Id);

            // Get all submissions for this contest
            var allSubmissions = await _context.Submissions
                .Where(s => s.ContestId == contest.Id)
                .OrderBy(s => s.SubmittedAt)
                .ToListAsync();

            // Apply freeze logic
            var isAdmin = IsAdmin();
            if (contest.IsFrozen && !isAdmin && contest.FreezeTime.HasValue)
            {
                allSubmissions = allSubmissions.Where(s => s.SubmittedAt <= contest.FreezeTime.Value).ToList();
            }

            var leaderboard = new List<ContestLeaderboardEntry>();

            foreach (var reg in registrations)
            {
                var user = users.GetValueOrDefault(reg.UserId);
                var userSubmissions = allSubmissions.Where(s => s.UserId == reg.UserId).ToList();
                var solvedProblems = new Dictionary<int, Submission>();
                var attempts = new Dictionary<int, int>();
                var solvedTimes = new Dictionary<int, int>();

                foreach (var cp in contestProblems)
                {
                    var problemSubmissions = userSubmissions
                        .Where(s => s.ProblemId == cp.ProblemId)
                        .OrderBy(s => s.SubmittedAt)
                        .ToList();

                    var acceptedSubmission = problemSubmissions.FirstOrDefault(s => s.Verdict == "AC");

                    if (acceptedSubmission != null)
                    {
                        solvedProblems[cp.ProblemId] = acceptedSubmission;
                        var wrongAttempts = problemSubmissions.Count(s => s.Verdict != "AC");
                        attempts[cp.ProblemId] = wrongAttempts;

                        var timeFromStart = (int)(acceptedSubmission.SubmittedAt - contest.StartTime).TotalMinutes;
                        if (timeFromStart < 0) timeFromStart = 0;
                        solvedTimes[cp.ProblemId] = timeFromStart;
                    }
                    else
                    {
                        attempts[cp.ProblemId] = problemSubmissions.Count;
                    }
                }

                int totalPoints = 0;
                int solvedCount = 0;
                int totalTime = 0;
                string lastSubmission = "N/A";
                DateTime? lastTime = null;

                foreach (var cp in contestProblems)
                {
                    if (solvedProblems.ContainsKey(cp.ProblemId))
                    {
                        solvedCount++;
                        var sub = solvedProblems[cp.ProblemId];
                        int penalty = attempts[cp.ProblemId] * 20;
                        int timePoints = solvedTimes[cp.ProblemId];

                        int pointsEarned = Math.Max(0, cp.Points - penalty - timePoints);
                        totalPoints += pointsEarned;
                        totalTime += timePoints + penalty;

                        if (lastTime == null || sub.SubmittedAt > lastTime)
                        {
                            lastTime = sub.SubmittedAt;
                            lastSubmission = sub.SubmittedAt.ToString("HH:mm:ss");
                        }
                    }
                }

                leaderboard.Add(new ContestLeaderboardEntry
                {
                    UserId = reg.UserId,
                    UserName = reg.UserName,
                    FullName = user?.FullName ?? reg.UserName,
                    SolvedCount = solvedCount,
                    TotalPoints = totalPoints,
                    LastSubmission = lastSubmission,
                    TotalTime = totalTime,
                    ProblemStatuses = contestProblems.Select(cp => new ProblemSubmissionStatus
                    {
                        Letter = cp.Letter,
                        ProblemTitle = cp.Problem?.Title ?? $"Problem {cp.Letter}",
                        IsSolved = solvedProblems.ContainsKey(cp.ProblemId),
                        Attempts = attempts.GetValueOrDefault(cp.ProblemId, 0),
                        Points = cp.Points,
                        SubmissionTime = solvedProblems.ContainsKey(cp.ProblemId)
                            ? solvedProblems[cp.ProblemId].SubmittedAt.ToString("HH:mm:ss")
                            : "-",
                        TimeFromStart = solvedTimes.GetValueOrDefault(cp.ProblemId, 0)
                    }).ToList()
                });
            }

            return leaderboard
                .OrderByDescending(e => e.SolvedCount)
                .ThenByDescending(e => e.TotalPoints)
                .ThenBy(e => e.TotalTime)
                .Select((entry, index) => { entry.Rank = index + 1; return entry; })
                .ToList();
        }

        private async Task<List<ContestProblem>> GetContestProblemsWithLetters(int contestId)
        {
            var contestProblems = await _context.ContestProblems
                .Where(cp => cp.ContestId == contestId)
                .OrderBy(cp => cp.Order)
                .Include(cp => cp.Problem)
                .ToListAsync();

            for (int i = 0; i < contestProblems.Count; i++)
            {
                contestProblems[i].Letter = ((char)('A' + i)).ToString();
            }

            return contestProblems;
        }

        #endregion
    }
}