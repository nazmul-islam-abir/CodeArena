// Controllers/CompilerController.cs
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Data;
using MyMvcApp.Models;
using MyMvcApp.Services;
using Newtonsoft.Json;
using System.Text;

[Route("Compiler")]
public class CompilerController : Controller
{
    private readonly JudgeService _judgeService;
    private readonly AppDbContext _context;

    public CompilerController(IConfiguration configuration, JudgeService judgeService, AppDbContext context)
    {
        _judgeService = judgeService;
        _context = context;
    }

    public IActionResult Index(int? problemId = null, int? contestId = null)
    {
        // Restrict access: compiler should only be used when a problem is provided
        if (!problemId.HasValue && TempData["ProblemId"] == null)
        {
            TempData["ErrorMessage"] = "Compiler is available only when solving a problem.";
            return RedirectToAction("Index", "Problem");
        }
        ViewBag.SelectedLanguage = 54;

        if (contestId.HasValue)
        {
            ViewBag.ContestId = contestId;
            TempData["ContestId"] = contestId;
        }
        else if (TempData["ContestId"] != null)
        {
            ViewBag.ContestId = TempData["ContestId"];
            TempData.Keep("ContestId");
        }

        if (TempData["ProblemTitle"] != null)
        {
            ViewBag.ProblemTitle = TempData["ProblemTitle"];
            ViewBag.ProblemDescription = TempData["ProblemDescription"];
            ViewBag.ProblemSampleInput = TempData["ProblemSampleInput"];
            ViewBag.ProblemSampleOutput = TempData["ProblemSampleOutput"];
            ViewBag.ProblemId = problemId ?? (TempData["ProblemId"] as int?);

            TempData.Keep("ProblemTitle");
            TempData.Keep("ProblemDescription");
            TempData.Keep("ProblemSampleInput");
            TempData.Keep("ProblemSampleOutput");
            TempData.Keep("ProblemId");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(string sourceCode, int languageId = 54, int? problemId = null, int? contestId = null, string action = "submit")
    {
        var userId = HttpContext.Session.GetString("UserId");
        bool isLoggedIn = !string.IsNullOrEmpty(userId);

        if (problemId == null && TempData["ProblemTitle"] != null)
        {
            problemId = TempData["ProblemId"] as int?;
            contestId = TempData["ContestId"] as int?;
        }

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            TempData["ErrorMessage"] = "No code provided.";
            return RedirectToAction("Details", "Problem", new { id = problemId });
        }

        if (!isLoggedIn)
        {
            TempData["ErrorMessage"] = "Please login to submit solutions.";
            return RedirectToAction("Login", "Auth");
        }

        if (!problemId.HasValue)
        {
            TempData["ErrorMessage"] = "No problem specified.";
            return RedirectToAction("Index", "Problem");
        }

        var languageName = GetLanguageName(languageId);

        if (!int.TryParse(userId, out int parsedUserId))
        {
            TempData["ErrorMessage"] = "Invalid user session. Please login again.";
            return RedirectToAction("Login", "Auth");
        }

        if (action == "runSample")
        {
            var problem = await _context.Problems.FindAsync(problemId.Value);
            if (problem == null)
            {
                TempData["ErrorMessage"] = "Problem not found.";
                return RedirectToAction("Index", "Problem");
            }

            var sampleTestCase = new TestCase
            {
                Input = problem.SampleInput ?? string.Empty,
                ExpectedOutput = problem.SampleOutput ?? string.Empty
            };

            var result = await _judgeService.RunSingleTestCaseAsync(
                sourceCode, languageId, sampleTestCase, problem.TimeLimit, problem.MemoryLimit
            );

            ViewBag.Output = result.IsSuccess ? result.Output : (string.IsNullOrEmpty(result.ErrorMessage) ? result.Output : result.ErrorMessage);
            ViewBag.ExpectedOutput = problem.SampleOutput;
            
            int statusCode = 3; // AC
            string statusDesc = "Accepted";
            if (result.Verdict == "WA") { statusCode = 4; statusDesc = "Wrong Answer"; }
            else if (result.Verdict == "TLE") { statusCode = 5; statusDesc = "Time Limit Exceeded"; }
            else if (result.Verdict == "MLE") { statusCode = 6; statusDesc = "Memory Limit Exceeded"; }
            else if (result.Verdict == "RE") { statusCode = 7; statusDesc = "Runtime Error"; }
            else if (result.Verdict == "CE") { statusCode = 8; statusDesc = "Compilation Error"; }
            
            ViewBag.StatusCode = statusCode;
            ViewBag.StatusDescription = statusDesc;
            if (result.ExecutionTime > 0) ViewBag.Time = result.ExecutionTime.ToString("0.000") + " s";
            if (result.MemoryUsed > 0) ViewBag.Memory = (result.MemoryUsed / 1024.0).ToString("0.00") + " MB";
            
            ViewBag.SubmittedCode = sourceCode;
            ViewBag.SelectedLanguage = languageId;
            ViewBag.ProblemTitle = problem.Title;
            ViewBag.ProblemDescription = problem.Description;
            ViewBag.ProblemSampleInput = problem.SampleInput;
            ViewBag.ProblemSampleOutput = problem.SampleOutput;
            ViewBag.ProblemId = problemId;
            ViewBag.ContestId = contestId;

            // Preserve TempData for subsequent requests
            TempData["ProblemTitle"] = problem.Title;
            TempData["ProblemDescription"] = problem.Description;
            TempData["ProblemSampleInput"] = problem.SampleInput;
            TempData["ProblemSampleOutput"] = problem.SampleOutput;
            TempData["ProblemId"] = problemId;
            TempData["ContestId"] = contestId;

            return View();
        }

        // Submit asynchronously - returns immediately with submission ID
        var submissionId = await _judgeService.SubmitAsync(
            parsedUserId,
            problemId.Value,
            sourceCode,
            languageId,
            languageName,
            contestId
        );

        // Redirect to submission page immediately
        return RedirectToAction("Index", "Submissions", new { id = submissionId });
    }

    private string GetLanguageName(int languageId)
    {
        return languageId switch
        {
            54 => "C++",
            50 => "C",
            71 => "Python",
            62 => "Java",
            63 => "JavaScript",
            51 => "C#",
            68 => "PHP",
            60 => "Go",
            73 => "Rust",
            _ => "Unknown"
        };
    }
}