
namespace MyMvcApp.Models
{
    public class Contest
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Organizer { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Duration { get; set; }
        public string Status { get; set; } // Upcoming, Active, Ended
        public int ParticipantCount { get; set; }
        public int ProblemCount { get; set; }
        public string Difficulty { get; set; }
        public bool IsRegistered { get; set; }
        public string ContestLink { get; set; }
        public bool RequiresApproval { get; set; }
        public string ApprovalStatus { get; set; } // Pending, Approved, Rejected
    }

    public class ContestProblem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Difficulty { get; set; }
        public int Points { get; set; }
        public int SolvedCount { get; set; }
    }

    public class ContestRegistration
    {
        public int ContestId { get; set; }
        public string UserName { get; set; }
        public DateTime RegistrationTime { get; set; }
        public string Status { get; set; }
    }

    public class ContestLeaderboardEntry
    {
        public int Rank { get; set; }
        public string UserName { get; set; }
        public int SolvedCount { get; set; }
        public int TotalPoints { get; set; }
        public string LastSubmission { get; set; }
    }
}