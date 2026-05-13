using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MyMvcApp.Models;
using MyMvcApp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyMvcApp.Controllers
{
    public class ContestController : Controller
    {
        private readonly AppDbContext _context;

        public ContestController(AppDbContext context)
        {
            _context = context;
        }
        // GET: /Contest - List all contests
        [HttpGet("")]
        [Route("/Contest")]
        public async Task<IActionResult> Index()
        {
            var contests = await _context.Contests
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.StartTime)
                .Select(c => new Contest
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    Organizer = c.Organizer,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    Duration = c.Duration,
                    ParticipantCount = _context.ContestRegistrations.Count(r => r.ContestId == c.Id),
                    ProblemCount = _context.ContestProblems.Count(p => p.ContestId == c.Id),
                    Difficulty = c.Difficulty,
                    Status = GetContestStatus(c.StartTime, c.EndTime),
                    ContestLink = c.ContestLink,
                    IsRegistered = false
                })
                .ToListAsync();

            var username = HttpContext.Session.GetString("Username");
            if (!string.IsNullOrEmpty(username))
            {
                var registeredContestIds = await _context.ContestRegistrations
                    .Where(r => r.UserName == username)
                    .Select(r => r.ContestId)
                    .ToListAsync();

                foreach (var contest in contests)
                {
                    contest.IsRegistered = registeredContestIds.Contains(contest.Id);
                }
            }

            // Return the Index view with the list
            return View("Index", contests);
        }

        // GET: /Contest/{id} - Contest details with problems
        [HttpGet("{id:int}")]
        [Route("/Contest/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (contest == null)
            {
                return NotFound();
            }

            contest.Status = GetContestStatus(contest.StartTime, contest.EndTime);
            contest.ParticipantCount = await _context.ContestRegistrations.CountAsync(r => r.ContestId == id);
            contest.ProblemCount = await _context.ContestProblems.CountAsync(p => p.ContestId == id);

            // Get problems with letters (A, B, C, D...)
            var contestProblems = await _context.ContestProblems
                .Where(cp => cp.ContestId == id)
                .OrderBy(cp => cp.Order)
                .Include(cp => cp.Problem)
                .ToListAsync();

            // Assign letters
            for (int i = 0; i < contestProblems.Count; i++)
            {
                contestProblems[i].Letter = ((char)('A' + i)).ToString();
            }

            ViewBag.Problems = contestProblems;

            // Check registration
            var username = HttpContext.Session.GetString("Username");
            if (!string.IsNullOrEmpty(username))
            {
                contest.IsRegistered = await _context.ContestRegistrations
                    .AnyAsync(r => r.ContestId == id && r.UserName == username);
            }

            // Return the Details view with a single contest
            return View("Details", contest);
        }

        // GET: /Contest/{id}/Problem/{letter}
        [HttpGet("{id:int}/Problem/{letter}")]
        public async Task<IActionResult> Problem(int id, string letter)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (contest == null)
            {
                return NotFound();
            }

            // Check if contest is active (for contest mode)
            bool isContestActive = DateTime.Now >= contest.StartTime && DateTime.Now <= contest.EndTime;
            bool isPractice = !isContestActive;

            // Check if user is registered
            var username = HttpContext.Session.GetString("Username");
            bool isRegistered = false;
            if (!string.IsNullOrEmpty(username))
            {
                isRegistered = await _context.ContestRegistrations
                    .AnyAsync(r => r.ContestId == id && r.UserName == username);
            }

            if (isContestActive && !isRegistered)
            {
                TempData["ErrorMessage"] = "You must register for this contest to solve problems during contest time.";
                return RedirectToAction("Details", new { id });
            }

            // Get problem by letter
            var contestProblems = await _context.ContestProblems
                .Where(cp => cp.ContestId == id)
                .OrderBy(cp => cp.Order)
                .Include(cp => cp.Problem)
                .ToListAsync();

            for (int i = 0; i < contestProblems.Count; i++)
            {
                contestProblems[i].Letter = ((char)('A' + i)).ToString();
            }

            var contestProblem = contestProblems.FirstOrDefault(cp => cp.Letter == letter.ToUpper());

            if (contestProblem == null)
            {
                return NotFound();
            }

            ViewBag.Contest = contest;
            ViewBag.Letter = letter;
            ViewBag.IsContestActive = isContestActive;
            ViewBag.IsPractice = isPractice;

            return View("Problem", contestProblem.Problem);
        }

        // GET: /Contest/{id}/Submissions
        [HttpGet("{id:int}/Submissions")]
        public async Task<IActionResult> Submissions(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null) return NotFound();

            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Auth");
            }

            var submissions = await _context.Submissions
                .Where(s => s.ContestId == id && s.UserName == username)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            ViewBag.Contest = contest;
            return View(submissions);
        }

        // GET: /Contest/{id}/Ranking
        [HttpGet("{id:int}/Ranking")]
        public async Task<IActionResult> Ranking(int id)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (contest == null)
            {
                return NotFound();
            }

            var leaderboard = await GetLeaderboard(contest);
            ViewBag.Contest = contest;
            ViewBag.IsFrozen = contest.IsFrozen && DateTime.Now >= contest.EndTime.AddMinutes(-30);

            return View(leaderboard);
        }

        // POST: /Contest/Register
        [HttpPost("Register")]
        public async Task<IActionResult> Register(int contestId)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Auth");

            var contest = await _context.Contests.FindAsync(contestId);
            if (contest == null) return NotFound();

            if (DateTime.Now > contest.StartTime)
            {
                TempData["ErrorMessage"] = "Registration closed. Contest has already started.";
                return RedirectToAction("Details", new { id = contestId });
            }

            var existing = await _context.ContestRegistrations
                .FirstOrDefaultAsync(r => r.ContestId == contestId && r.UserName == username);

            if (existing == null)
            {
                _context.ContestRegistrations.Add(new ContestRegistration
                {
                    ContestId = contestId,
                    UserName = username,
                    RegistrationTime = DateTime.UtcNow,
                    Status = "Approved"
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Successfully registered for the contest!";
            }

            return RedirectToAction("Details", new { id = contestId });
        }

        // POST: /Contest/Unregister
        [HttpPost("Unregister")]
        public async Task<IActionResult> Unregister(int contestId)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Auth");

            var registration = await _context.ContestRegistrations
                .FirstOrDefaultAsync(r => r.ContestId == contestId && r.UserName == username);

            if (registration != null)
            {
                _context.ContestRegistrations.Remove(registration);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Unregistered from contest.";
            }

            return RedirectToAction("Details", new { id = contestId });
        }

        // GET: /Contest/Join
        [HttpGet("Join")]
        public async Task<IActionResult> Join(string link)
        {
            var contest = await _context.Contests
                .FirstOrDefaultAsync(c => c.ContestLink == link && !c.IsDeleted);

            if (contest == null)
                return NotFound();

            return RedirectToAction("Details", new { id = contest.Id });
        }

        #region Admin Actions

        [Authorize(Roles = "Admin")]
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost("Create")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Contest newContest, string problemIds)
        {
            if (string.IsNullOrEmpty(problemIds))
            {
                ModelState.AddModelError("problemIds", "At least one problem ID is required.");
                return View(newContest);
            }

            var idList = problemIds.Split(',')
                .Select(id => int.TryParse(id.Trim(), out int val) ? val : 0)
                .Where(i => i > 0)
                .Distinct()
                .ToList();

            var existingProblemIds = await _context.Problems
                .Where(p => idList.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            newContest.CreatedAt = DateTime.UtcNow;
            newContest.Status = "Upcoming";
            newContest.IsActive = true;
            newContest.RequiresApproval = false;
            newContest.ApprovalStatus = "Approved";
            newContest.ContestLink = Guid.NewGuid().ToString().Substring(0, 8);

            if (!string.IsNullOrEmpty(newContest.Duration) && newContest.Duration.Contains("hour"))
            {
                if (int.TryParse(newContest.Duration.Split(' ')[0], out int hours))
                {
                    newContest.EndTime = newContest.StartTime.AddHours(hours);
                }
                else
                {
                    newContest.EndTime = newContest.StartTime.AddHours(2);
                }
            }
            else
            {
                newContest.EndTime = newContest.StartTime.AddHours(2);
            }

            _context.Contests.Add(newContest);
            await _context.SaveChangesAsync();

            int order = 1;
            foreach (var pId in existingProblemIds)
            {
                _context.ContestProblems.Add(new ContestProblem
                {
                    ContestId = newContest.Id,
                    ProblemId = pId,
                    Order = order++,
                    Points = 100
                });
            }

            newContest.ProblemCount = existingProblemIds.Count;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Contest '{newContest.Title}' created successfully! Join link: /Contest/Join?link={newContest.ContestLink}";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null || contest.IsDeleted) return NotFound();

            var problems = await _context.ContestProblems
                .Where(cp => cp.ContestId == id)
                .OrderBy(cp => cp.Order)
                .Select(cp => cp.ProblemId)
                .ToListAsync();

            ViewBag.ProblemIds = string.Join(",", problems);
            return View(contest);
        }

        [HttpPost("Edit/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Contest updatedContest, string problemIds)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null || contest.IsDeleted) return NotFound();

            if (string.IsNullOrEmpty(problemIds))
            {
                ModelState.AddModelError("problemIds", "At least one problem ID is required.");
                return View(updatedContest);
            }

            var idList = problemIds.Split(',')
                .Select(pid => int.TryParse(pid.Trim(), out int val) ? val : 0)
                .Where(i => i > 0)
                .Distinct()
                .ToList();

            var existingProblemIds = await _context.Problems
                .Where(p => idList.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            // Update metadata
            contest.Title = updatedContest.Title;
            contest.Description = updatedContest.Description;
            contest.Organizer = updatedContest.Organizer;
            contest.StartTime = updatedContest.StartTime;
            contest.Difficulty = updatedContest.Difficulty;
            contest.Duration = updatedContest.Duration;

            if (!string.IsNullOrEmpty(contest.Duration) && contest.Duration.Contains("hour"))
            {
                if (int.TryParse(contest.Duration.Split(' ')[0], out int hours))
                    contest.EndTime = contest.StartTime.AddHours(hours);
                else
                    contest.EndTime = contest.StartTime.AddHours(2);
            }
            else
            {
                contest.EndTime = contest.StartTime.AddHours(2);
            }

            // Update problems
            var currentProblems = await _context.ContestProblems.Where(cp => cp.ContestId == id).ToListAsync();
            _context.ContestProblems.RemoveRange(currentProblems);

            int order = 1;
            foreach (var pId in existingProblemIds)
            {
                _context.ContestProblems.Add(new ContestProblem
                {
                    ContestId = id,
                    ProblemId = pId,
                    Order = order++,
                    Points = 100
                });
            }

            contest.ProblemCount = existingProblemIds.Count;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Contest updated successfully!";
            return RedirectToAction("Details", new { id = id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest != null)
            {
                contest.IsDeleted = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Contest deleted.";
            }
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("ToggleFreeze/{id:int}")]
        public async Task<IActionResult> ToggleFreeze(int id)
        {
            var contest = await _context.Contests.FindAsync(id);
            if (contest != null)
            {
                contest.IsFrozen = !contest.IsFrozen;
                if (contest.IsFrozen) contest.FreezeTime = DateTime.UtcNow;
                else contest.FreezeTime = null;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = contest.IsFrozen ? "Leaderboard frozen!" : "Leaderboard unfrozen!";
            }
            return RedirectToAction("Details", new { id = id });
        }

        #endregion

        #region Helper Methods

        private static string GetContestStatus(DateTime startTime, DateTime endTime)
        {
            var now = DateTime.Now;
            if (now < startTime) return "Upcoming";
            if (now <= endTime) return "Active";
            return "Ended";
        }

        private async Task<List<ContestLeaderboardEntry>> GetLeaderboard(Contest contest)
        {
            var registrations = await _context.ContestRegistrations
                .Where(r => r.ContestId == contest.Id)
                .Select(r => r.UserName)
                .ToListAsync();

            var allSubmissions = await _context.Submissions
                .Where(s => s.ContestId == contest.Id && s.SubmittedAt >= contest.StartTime)
                .ToListAsync();

            // Apply freeze logic
            bool isAdmin = HttpContext.Session.GetString("UserRole") == "Admin";
            if (contest.IsFrozen && !isAdmin && contest.FreezeTime.HasValue)
            {
                allSubmissions = allSubmissions.Where(s => s.SubmittedAt <= contest.FreezeTime.Value).ToList();
            }

            var contestProblems = await _context.ContestProblems
                .Where(cp => cp.ContestId == contest.Id)
                .OrderBy(cp => cp.Order)
                .ToListAsync();

            var leaderboard = new List<ContestLeaderboardEntry>();

            foreach (var username in registrations)
            {
                var userSubmissions = allSubmissions.Where(s => s.UserName == username).ToList();
                var solvedProblems = new Dictionary<int, Submission>();
                var attempts = new Dictionary<int, int>();

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
                        attempts[cp.ProblemId] = problemSubmissions.Count(s => s.Verdict != "AC");
                    }
                    else
                    {
                        attempts[cp.ProblemId] = problemSubmissions.Count;
                    }
                }

                int totalPoints = 0;
                int solvedCount = 0;
                string lastSubmission = "N/A";

                foreach (var cp in contestProblems)
                {
                    if (solvedProblems.ContainsKey(cp.ProblemId))
                    {
                        solvedCount++;
                        var sub = solvedProblems[cp.ProblemId];
                        int penalty = attempts[cp.ProblemId] * 20;
                        int timePoints = (int)(sub.SubmittedAt - contest.StartTime).TotalMinutes;
                        totalPoints += cp.Points - penalty - timePoints;
                        if (totalPoints < 0) totalPoints = 0;

                        if (lastSubmission == "N/A" || sub.SubmittedAt > solvedProblems.Values.Max(s => s.SubmittedAt))
                        {
                            lastSubmission = sub.SubmittedAt.ToString("HH:mm");
                        }
                    }
                }

                leaderboard.Add(new ContestLeaderboardEntry
                {
                    UserName = username,
                    SolvedCount = solvedCount,
                    TotalPoints = totalPoints,
                    LastSubmission = lastSubmission
                });
            }

            return leaderboard
                .OrderByDescending(u => u.SolvedCount)
                .ThenByDescending(u => u.TotalPoints)
                .Select((entry, index) => { entry.Rank = index + 1; return entry; })
                .ToList();
        }

        #endregion
    }
}