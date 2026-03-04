using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyMvcApp.Controllers
{
    public class ContestController : Controller
    {
        // Static list of contests (in real app, this would come from database)
        private static List<Contest> contests = new List<Contest>
        {
            new Contest
            {
                Id = 1,
                Title = "CodeArena Weekly #42",
                Description = "Join us for our weekly coding contest! Problems range from easy to hard, covering algorithms, data structures, and math. Great for beginners and experienced coders alike.",
                Organizer = "CodeArena Team",
                StartTime = DateTime.Now.AddHours(2),
                EndTime = DateTime.Now.AddHours(4),
                Duration = "2 hours",
                Status = "Upcoming",
                ParticipantCount = 156,
                ProblemCount = 6,
                Difficulty = "Mixed",
                IsRegistered = false,
                ContestLink = "codearena-weekly-42",
                RequiresApproval = false,
                ApprovalStatus = "Approved"
            },
            new Contest
            {
                Id = 2,
                Title = "Dynamic Programming Challenge",
                Description = "Test your DP skills with 5 challenging problems. From classic knapsack to advanced DP with bitmasking. Recommended for intermediate to advanced programmers.",
                Organizer = "AlgoMaster",
                StartTime = DateTime.Now.AddDays(2),
                EndTime = DateTime.Now.AddDays(2).AddHours(3),
                Duration = "3 hours",
                Status = "Upcoming",
                ParticipantCount = 89,
                ProblemCount = 5,
                Difficulty = "Hard",
                IsRegistered = false,
                ContestLink = "dp-challenge-2025",
                RequiresApproval = true,
                ApprovalStatus = "Approved"
            },
            new Contest
            {
                Id = 3,
                Title = "Beginner's Warmup",
                Description = "Perfect for newcomers! Simple problems to get started with competitive programming. Learn basic input/output, conditionals, and loops.",
                Organizer = "CP Beginners",
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now.AddHours(-2),
                Duration = "2 hours",
                Status = "Ended",
                ParticipantCount = 234,
                ProblemCount = 4,
                Difficulty = "Easy",
                IsRegistered = false,
                ContestLink = "beginners-warmup",
                RequiresApproval = false,
                ApprovalStatus = "Approved"
            },
            new Contest
            {
                Id = 4,
                Title = "Graph Theory Marathon",
                Description = "A deep dive into graph algorithms: BFS, DFS, Dijkstra, Floyd-Warshall, and more. 7 problems of varying difficulty.",
                Organizer = "GraphGurus",
                StartTime = DateTime.Now.AddHours(5),
                EndTime = DateTime.Now.AddHours(9),
                Duration = "4 hours",
                Status = "Upcoming",
                ParticipantCount = 67,
                ProblemCount = 7,
                Difficulty = "Hard",
                IsRegistered = true,
                ContestLink = "graph-marathon",
                RequiresApproval = false,
                ApprovalStatus = "Approved"
            },
            new Contest
            {
                Id = 5,
                Title = "University Clash: CSE vs EEE",
                Description = "Inter-department coding battle! Represent your department and win prizes. Open only for university students.",
                Organizer = "University Coding Club",
                StartTime = DateTime.Now.AddDays(5),
                EndTime = DateTime.Now.AddDays(5).AddHours(4),
                Duration = "4 hours",
                Status = "Upcoming",
                ParticipantCount = 45,
                ProblemCount = 8,
                Difficulty = "Mixed",
                IsRegistered = false,
                ContestLink = "uni-clash-2025",
                RequiresApproval = true,
                ApprovalStatus = "Pending"
            },
            new Contest
            {
                Id = 6,
                Title = "Speed Programming: Code Sprint",
                Description = "Fast-paced contest with 10 short problems. Solve as many as you can in 90 minutes! Perfect for improving coding speed.",
                Organizer = "SpeedCoders",
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1).AddMinutes(30),
                Duration = "90 minutes",
                Status = "Active",
                ParticipantCount = 178,
                ProblemCount = 10,
                Difficulty = "Easy-Medium",
                IsRegistered = false,
                ContestLink = "code-sprint-42",
                RequiresApproval = false,
                ApprovalStatus = "Approved"
            }
        };

        private static List<ContestProblem> contestProblems = new List<ContestProblem>
        {
            new ContestProblem { Id = 1, Title = "Two Sum", Difficulty = "Easy", Points = 10, SolvedCount = 145 },
            new ContestProblem { Id = 2, Title = "Reverse String", Difficulty = "Easy", Points = 10, SolvedCount = 132 },
            new ContestProblem { Id = 3, Title = "Fibonacci", Difficulty = "Easy", Points = 10, SolvedCount = 128 },
            new ContestProblem { Id = 4, Title = "Binary Search", Difficulty = "Medium", Points = 20, SolvedCount = 98 },
            new ContestProblem { Id = 5, Title = "Merge Sort", Difficulty = "Medium", Points = 20, SolvedCount = 87 },
            new ContestProblem { Id = 6, Title = "Knapsack Problem", Difficulty = "Hard", Points = 30, SolvedCount = 56 },
            new ContestProblem { Id = 7, Title = "Dijkstra's Algorithm", Difficulty = "Hard", Points = 30, SolvedCount = 45 },
        };

        private static List<ContestLeaderboardEntry> leaderboard = new List<ContestLeaderboardEntry>
        {
            new ContestLeaderboardEntry { Rank = 1, UserName = "minhaz_coder", SolvedCount = 5, TotalPoints = 120, LastSubmission = "5 min ago" },
            new ContestLeaderboardEntry { Rank = 2, UserName = "junayed_dev", SolvedCount = 4, TotalPoints = 90, LastSubmission = "12 min ago" },
            new ContestLeaderboardEntry { Rank = 3, UserName = "nazmul_cp", SolvedCount = 4, TotalPoints = 85, LastSubmission = "15 min ago" },
            new ContestLeaderboardEntry { Rank = 4, UserName = "abdullah_coder", SolvedCount = 3, TotalPoints = 60, LastSubmission = "22 min ago" },
            new ContestLeaderboardEntry { Rank = 5, UserName = "sarah_khan", SolvedCount = 3, TotalPoints = 55, LastSubmission = "28 min ago" },
            new ContestLeaderboardEntry { Rank = 6, UserName = "rajib_ahmed", SolvedCount = 2, TotalPoints = 40, LastSubmission = "35 min ago" },
            new ContestLeaderboardEntry { Rank = 7, UserName = "tanvir_hasan", SolvedCount = 2, TotalPoints = 35, LastSubmission = "41 min ago" },
            new ContestLeaderboardEntry { Rank = 8, UserName = "priya_das", SolvedCount = 1, TotalPoints = 20, LastSubmission = "50 min ago" },
        };

        public IActionResult Index()
        {
            return View(contests);
        }

        public IActionResult Details(int id)
        {
            var contest = contests.FirstOrDefault(c => c.Id == id);
            if (contest == null)
            {
                return NotFound();
            }

            ViewBag.Problems = contestProblems.Take(6).ToList();
            ViewBag.Leaderboard = leaderboard;
            
            return View(contest);
        }

        [HttpPost]
        public IActionResult Register(int contestId)
        {
            var contest = contests.FirstOrDefault(c => c.Id == contestId);
            if (contest != null)
            {
                contest.ParticipantCount++;
                contest.IsRegistered = true;
            }
            
            return RedirectToAction("Details", new { id = contestId });
        }

        [HttpPost]
        public IActionResult Unregister(int contestId)
        {
            var contest = contests.FirstOrDefault(c => c.Id == contestId);
            if (contest != null)
            {
                contest.ParticipantCount--;
                contest.IsRegistered = false;
            }
            
            return RedirectToAction("Details", new { id = contestId });
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Contest newContest)
        {
            newContest.Id = contests.Max(c => c.Id) + 1;
            newContest.Status = "Upcoming";
            newContest.ParticipantCount = 0;
            newContest.ContestLink = $"contest-{newContest.Id}-{DateTime.Now.Ticks}";
            newContest.ApprovalStatus = "Pending";
            
            contests.Add(newContest);
            
            return RedirectToAction("Index");
        }

        public IActionResult Join(string link)
        {
            var contest = contests.FirstOrDefault(c => c.ContestLink == link);
            if (contest == null)
            {
                return NotFound();
            }
            
            return RedirectToAction("Details", new { id = contest.Id });
        }

        public IActionResult MyContests()
        {
            var myContests = contests.Where(c => c.IsRegistered).ToList();
            return View(myContests);
        }
    }
}