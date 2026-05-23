// Models/Contest.cs - Enhanced contest models
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;   // Added for [NotMapped]

namespace MyMvcApp.Models
{
    public class Contest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Contest title is required")]
        [StringLength(200, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string Organizer { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public string Duration { get; set; } = string.Empty;

        [NotMapped]
        public string Status { get; set; } = string.Empty;

        [NotMapped]
        public int ParticipantCount { get; set; }

        [NotMapped]
        public int ProblemCount { get; set; }

        public string Difficulty { get; set; } = string.Empty;

        [NotMapped]
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

        // New properties for better contest management
        public int MaxParticipants { get; set; } = 1000;
        public bool ShowRankingImmediately { get; set; } = true;

        // Password property with null-safe setter
        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => _password = value ?? string.Empty;
        }

        public bool IsPrivate { get; set; } = false;

        // Rules property with null-safe setter
        private string _rules = string.Empty;
        public string Rules
        {
            get => _rules;
            set => _rules = value ?? string.Empty;
        }
    }

    public class ContestProblem
    {
        public int Id { get; set; }
        public int ContestId { get; set; }
        public int ProblemId { get; set; }
        public string? CustomTitle { get; set; }
        public string Letter { get; set; } = string.Empty;
        public int Order { get; set; }
        public int Points { get; set; }

        public virtual Contest? Contest { get; set; }
        public virtual Problem? Problem { get; set; }
    }

    // Models/Contest.cs - Update ContestRegistration class
    public class ContestRegistration
    {
        public int Id { get; set; }
        public int ContestId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime RegistrationTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PasswordEntered { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual Contest? Contest { get; set; }
    }

    public class ContestSubmission
    {
        public int Id { get; set; }
        public int ContestId { get; set; }
        public int SubmissionId { get; set; }
        public int UserId { get; set; }
        public int ProblemId { get; set; }
        public string ProblemLetter { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTime? SolvedAt { get; set; }
        public int PointsEarned { get; set; }
        public int Penalty { get; set; }
    }

    public class ContestLeaderboardEntry
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int SolvedCount { get; set; }
        public int TotalPoints { get; set; }
        public string LastSubmission { get; set; } = string.Empty;
        public int TotalTime { get; set; }
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
        public int TimeFromStart { get; set; }
        public string ColorClass => IsSolved ? "bg-success" : (Attempts > 0 ? "bg-danger" : "bg-secondary");
    }

    public class ContestViewModel
    {
        public Contest Contest { get; set; } = new();
        public List<Problem> AvailableProblems { get; set; } = new();
        public List<int> SelectedProblemIds { get; set; } = new();
        public string ProblemIdsString { get; set; } = string.Empty;
    }

    public class JoinContestViewModel
    {
        public int ContestId { get; set; }
        public string Password { get; set; } = string.Empty;
        public string ContestTitle { get; set; } = string.Empty;
        public bool IsPrivate { get; set; }
    }
}