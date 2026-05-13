using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;

namespace MyMvcApp.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Problems")]
    public class AdminProblemController : Controller
    {
        private readonly AppDbContext _context;

        public AdminProblemController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var problems = await _context.Problems.OrderByDescending(p => p.Id).ToListAsync();
            return View("~/Views/Admin/Problem/Index.cshtml", problems);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View("~/Views/Admin/Problem/Create.cshtml");
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Problem problem)
        {
            if (ModelState.IsValid)
            {
                problem.CreatedAt = DateTime.UtcNow;
                problem.IsActive = true;
                problem.Source = "local";
                problem.SourceId = Guid.NewGuid().ToString();
                problem.SolvedCount = 0;
                problem.SubmissionCount = 0;
                
                _context.Problems.Add(problem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Problem/Create.cshtml", problem);
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var problem = await _context.Problems.FindAsync(id);
            if (problem == null) return NotFound();
            return View("~/Views/Admin/Problem/Edit.cshtml", problem);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Problem problem)
        {
            if (id != problem.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(problem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("~/Views/Admin/Problem/Edit.cshtml", problem);
        }

        [HttpGet("TestCases/{problemId}")]
        public async Task<IActionResult> TestCases(int problemId)
        {
            var problem = await _context.Problems.FindAsync(problemId);
            if (problem == null) return NotFound();

            var testCases = await _context.TestCases
                .Where(t => t.ProblemId == problemId)
                .OrderBy(t => t.Order)
                .ToListAsync();

            ViewBag.Problem = problem;
            return View("~/Views/Admin/Problem/TestCases.cshtml", testCases);
        }

        [HttpPost("AddTestCase")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTestCase(TestCase testCase)
        {
            if (ModelState.IsValid)
            {
                _context.TestCases.Add(testCase);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(TestCases), new { problemId = testCase.ProblemId });
            }
            return RedirectToAction(nameof(TestCases), new { problemId = testCase.ProblemId });
        }

        [HttpPost("DeleteTestCase/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTestCase(int id)
        {
            var testCase = await _context.TestCases.FindAsync(id);
            if (testCase != null)
            {
                int problemId = testCase.ProblemId;
                _context.TestCases.Remove(testCase);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(TestCases), new { problemId = problemId });
            }
            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var problem = await _context.Problems.FindAsync(id);
            if (problem != null)
            {
                // Also delete associated test cases or leave them if you have cascade delete configured
                var testCases = _context.TestCases.Where(t => t.ProblemId == id);
                _context.TestCases.RemoveRange(testCases);
                
                _context.Problems.Remove(problem);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
