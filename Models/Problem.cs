// Models/Problem.cs
namespace MyMvcApp.Models
{
    public class Problem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? InputFormat { get; set; }
        public string? OutputFormat { get; set; }
        public string? SampleInput { get; set; }
        public string? SampleOutput { get; set; }
        public string? Constraints { get; set; }
        public int Difficulty { get; set; } // 1=Easy, 2=Medium, 3=Hard
        public string DifficultyText
        {
            get
            {
                return Difficulty switch
                {
                    1 => "Easy",
                    2 => "Medium",
                    3 => "Hard",
                    _ => "Unknown"
                };
            }
        }
        public int Points { get; set; }
        public string? Category { get; set; }
        public int TimeLimit { get; set; }
        public int MemoryLimit { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        // Statistics (calculated)
        public int SolvedCount { get; set; }
        public int SubmissionCount { get; set; }
        public double SuccessRate
        {
            get
            {
                if (SubmissionCount == 0) return 0;
                return Math.Round((double)SolvedCount / SubmissionCount * 100, 1);
            }
        }

        // Source tracking for imported problems
        public string Source { get; set; } = "local";
        public string? SourceId { get; set; }

        // External URL for imported problems
        public string? ExternalUrl
        {
            get
            {
                if (Source == "Codeforces" && !string.IsNullOrEmpty(SourceId))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(SourceId, @"(\d+)([A-Z]\d*)");
                    if (match.Success)
                    {
                        return $"https://codeforces.com/problemset/problem/{match.Groups[1].Value}/{match.Groups[2].Value}";
                    }
                }
                return null;
            }
        }
    }
    public class Submission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProblemId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageName { get; set; } = string.Empty;
        public string SourceCode { get; set; } = string.Empty;
        public string Verdict { get; set; } = string.Empty;
        public double? ExecutionTime { get; set; }
        public int? MemoryUsed { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int? ContestId { get; set; }
        public int? TestCasesPassed { get; set; }
        public int? TotalTestCases { get; set; }

        // External unique token for the submission
        public string UniqueId { get; set; } = string.Empty;

        // Navigation properties
        public string ProblemTitle { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        // Add this property for error messages
        public string? ErrorMessage { get; set; }
    }

    public class TestCase
    {
        public int Id { get; set; }
        public int ProblemId { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public bool IsSample { get; set; }
        public int Points { get; set; }
        public int Order { get; set; }
    }
}