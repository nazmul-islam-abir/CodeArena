namespace MyMvcApp.Models
{
    public class Problem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string InputFormat { get; set; }
        public string OutputFormat { get; set; }
        public string SampleInput { get; set; }
        public string SampleOutput { get; set; }
        public string Constraints { get; set; }
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
        public string Category { get; set; }
        public int TimeLimit { get; set; } = 2; // seconds
        public int MemoryLimit { get; set; } = 256; // MB
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
    }

    public class ProblemSolution
    {
        public int ProblemId { get; set; }
        public string Language { get; set; }
        public string Code { get; set; }
        public int LanguageId { get; set; }
    }
}