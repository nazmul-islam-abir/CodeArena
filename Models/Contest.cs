// Models/Contest.cs
namespace MyMvcApp.Models
{
    public class Contest
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Organizer { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Duration { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Upcoming, Active, Ended
        public int ParticipantCount { get; set; }
        public int ProblemCount { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }
        public string ContestLink { get; set; } = string.Empty;
        public bool RequiresApproval { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFrozen { get; set; } = false;
        public DateTime? FreezeTime { get; set; }
        public bool IsDeleted { get; set; } = false;
    }

    public class ContestProblem
    {
        public int Id { get; set; }
        public int ContestId { get; set; }
        public int ProblemId { get; set; }
        public string? CustomTitle { get; set; }
        public string Letter { get; set; } = string.Empty; // A, B, C, D...
        public int Order { get; set; }
        public int Points { get; set; }

        // Navigation properties
        public virtual Contest? Contest { get; set; }
        public virtual Problem? Problem { get; set; }
    }

    public class ContestRegistration
    {
        public int ContestId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime RegistrationTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ContestLeaderboardEntry
    {
        public int Rank { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int SolvedCount { get; set; }
        public int TotalPoints { get; set; }
        public string LastSubmission { get; set; } = string.Empty;
        public List<ProblemSubmissionStatus> ProblemStatuses { get; set; } = new();
    }

    public class ProblemSubmissionStatus
    {
        public string Letter { get; set; } = string.Empty;
        public string ProblemTitle { get; set; } = string.Empty;
        public bool IsSolved { get; set; }
        public int Attempts { get; set; }
        public int Points { get; set; }
        public string SubmissionTime { get; set; } = string.Empty;
    }
}