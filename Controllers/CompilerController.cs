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
    public async Task<IActionResult> Index(string sourceCode, int languageId = 54, int? problemId = null, int? contestId = null)
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

        // Submit asynchronously - returns immediately with submission ID
        if (!int.TryParse(userId, out int parsedUserId))
        {
            TempData["ErrorMessage"] = "Invalid user session. Please login again.";
            return RedirectToAction("Login", "Auth");
        }

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