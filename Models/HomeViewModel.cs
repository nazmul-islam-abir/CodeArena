namespace MyMvcApp.Models
{
    public class HomeViewModel
    {
        public int TotalActiveUsers { get; set; }
        public List<Problem> FeaturedProblems { get; set; } = new List<Problem>();
        public List<User> TopUsers { get; set; } = new List<User>();
    }
}
