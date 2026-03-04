using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using System.Collections.Generic;
using System.Linq;

namespace MyMvcApp.Controllers
{
    public class ProblemController : Controller
    {
        // Static list of problems (in real app, this would come from database)
        private static List<Problem> problems = new List<Problem>
        {
            new Problem
            {
                Id = 1,
                Title = "Sum of Two Numbers",
                Description = "Given two integers A and B, calculate their sum.",
                InputFormat = "The first line contains two space-separated integers A and B.",
                OutputFormat = "Print a single integer - the sum of A and B.",
                SampleInput = "5 3",
                SampleOutput = "8",
                Constraints = "-10^9 ≤ A, B ≤ 10^9",
                Difficulty = 1,
                Points = 10,
                Category = "Basic Math",
                SolvedCount = 245,
                SubmissionCount = 312,
                TimeLimit = 1,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 2,
                Title = "Even or Odd",
                Description = "Given an integer N, determine if it's even or odd.",
                InputFormat = "The first line contains a single integer N.",
                OutputFormat = "Print 'EVEN' if N is even, 'ODD' if N is odd.",
                SampleInput = "7",
                SampleOutput = "ODD",
                Constraints = "-10^6 ≤ N ≤ 10^6",
                Difficulty = 1,
                Points = 10,
                Category = "Conditional",
                SolvedCount = 189,
                SubmissionCount = 234,
                TimeLimit = 1,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 3,
                Title = "Factorial",
                Description = "Calculate the factorial of a given number N (N!).",
                InputFormat = "The first line contains a single integer N (0 ≤ N ≤ 20).",
                OutputFormat = "Print the factorial of N.",
                SampleInput = "5",
                SampleOutput = "120",
                Constraints = "0 ≤ N ≤ 20",
                Difficulty = 2,
                Points = 30,
                Category = "Loop",
                SolvedCount = 156,
                SubmissionCount = 278,
                TimeLimit = 1,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 4,
                Title = "Prime Numbers",
                Description = "Check if a given number N is prime or not.",
                InputFormat = "The first line contains a single integer N (1 ≤ N ≤ 10^6).",
                OutputFormat = "Print 'YES' if N is prime, 'NO' otherwise.",
                SampleInput = "17",
                SampleOutput = "YES",
                Constraints = "1 ≤ N ≤ 10^6",
                Difficulty = 2,
                Points = 30,
                Category = "Math",
                SolvedCount = 134,
                SubmissionCount = 298,
                TimeLimit = 2,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 5,
                Title = "Fibonacci Sequence",
                Description = "Find the Nth number in the Fibonacci sequence. The sequence starts with F(0) = 0, F(1) = 1.",
                InputFormat = "The first line contains a single integer N (0 ≤ N ≤ 30).",
                OutputFormat = "Print the Nth Fibonacci number.",
                SampleInput = "10",
                SampleOutput = "55",
                Constraints = "0 ≤ N ≤ 30",
                Difficulty = 2,
                Points = 30,
                Category = "Recursion",
                SolvedCount = 98,
                SubmissionCount = 187,
                TimeLimit = 1,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 6,
                Title = "Palindrome Check",
                Description = "Check if a given string is a palindrome (reads the same forwards and backwards).",
                InputFormat = "The first line contains a single string S (1 ≤ |S| ≤ 1000).",
                OutputFormat = "Print 'YES' if the string is palindrome, 'NO' otherwise.",
                SampleInput = "racecar",
                SampleOutput = "YES",
                Constraints = "String contains only lowercase English letters",
                Difficulty = 2,
                Points = 30,
                Category = "String",
                SolvedCount = 167,
                SubmissionCount = 245,
                TimeLimit = 1,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 7,
                Title = "GCD of Two Numbers",
                Description = "Find the Greatest Common Divisor (GCD) of two numbers A and B.",
                InputFormat = "The first line contains two space-separated integers A and B (1 ≤ A, B ≤ 10^9).",
                OutputFormat = "Print the GCD of A and B.",
                SampleInput = "48 18",
                SampleOutput = "6",
                Constraints = "1 ≤ A, B ≤ 10^9",
                Difficulty = 2,
                Points = 30,
                Category = "Math",
                SolvedCount = 145,
                SubmissionCount = 189,
                TimeLimit = 1,
                MemoryLimit = 128
            },
            new Problem
            {
                Id = 8,
                Title = "Array Sorting",
                Description = "Sort an array of integers in non-decreasing order.",
                InputFormat = "First line contains N (1 ≤ N ≤ 1000). Second line contains N space-separated integers.",
                OutputFormat = "Print the sorted array.",
                SampleInput = "5\n3 1 4 1 5",
                SampleOutput = "1 1 3 4 5",
                Constraints = "1 ≤ N ≤ 1000\n-10^6 ≤ arr[i] ≤ 10^6",
                Difficulty = 3,
                Points = 60,
                Category = "Sorting",
                SolvedCount = 89,
                SubmissionCount = 167,
                TimeLimit = 2,
                MemoryLimit = 256
            },
            new Problem
            {
                Id = 9,
                Title = "Binary Search",
                Description = "Given a sorted array and a target value, find the index of the target (0-based). Return -1 if not found.",
                InputFormat = "First line: N (size of array)\nSecond line: N sorted integers\nThird line: target value",
                OutputFormat = "Print the index of target or -1",
                SampleInput = "5\n1 3 5 7 9\n5",
                SampleOutput = "2",
                Constraints = "1 ≤ N ≤ 10^5\n-10^9 ≤ arr[i] ≤ 10^9",
                Difficulty = 3,
                Points = 60,
                Category = "Searching",
                SolvedCount = 76,
                SubmissionCount = 198,
                TimeLimit = 2,
                MemoryLimit = 256
            },
            new Problem
            {
                Id = 10,
                Title = "Knapsack Problem",
                Description = "Given N items with weights and values, find the maximum value that can be obtained with capacity W.",
                InputFormat = "First line: N W\nNext N lines: weight value",
                OutputFormat = "Print maximum value",
                SampleInput = "3 50\n10 60\n20 100\n30 120",
                SampleOutput = "220",
                Constraints = "1 ≤ N ≤ 100\n1 ≤ W ≤ 10000",
                Difficulty = 3,
                Points = 60,
                Category = "Dynamic Programming",
                SolvedCount = 45,
                SubmissionCount = 134,
                TimeLimit = 2,
                MemoryLimit = 256
            }
        };

        public IActionResult Index()
        {
            return View(problems);
        }

        public IActionResult Details(int id)
        {
            var problem = problems.FirstOrDefault(p => p.Id == id);
            if (problem == null)
            {
                return NotFound();
            }
            return View(problem);
        }

        [HttpGet]
        public IActionResult Solve(int id)
        {
            var problem = problems.FirstOrDefault(p => p.Id == id);
            if (problem == null)
            {
                return NotFound();
            }

            // Store problem info in TempData to pass to compiler
            TempData["CurrentProblem"] = System.Text.Json.JsonSerializer.Serialize(problem);
            TempData["ProblemId"] = id;
            TempData["ProblemTitle"] = problem.Title;
            TempData["ProblemDescription"] = problem.Description;
            TempData["ProblemSampleInput"] = problem.SampleInput;
            TempData["ProblemSampleOutput"] = problem.SampleOutput;

            // Redirect to compiler with problem info
            return RedirectToAction("Index", "Compiler", new { problemId = id });
        }
    }
}