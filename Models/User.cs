using System;
using System.ComponentModel.DataAnnotations;

namespace MyMvcApp.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscore")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Student ID")]
        public string? StudentId { get; set; }

        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; } = string.Empty;

        // Password Reset Fields
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Statistics columns (added for problem solving)
        public int ProblemsSolved { get; set; }
        public int ContestsParticipated { get; set; }
        public int TotalPoints { get; set; }

        public string FullName => $"{FirstName} {LastName}";
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or email is required")]
        [Display(Name = "Username or Email")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public class SignupViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscore")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Student ID")]
        public string? StudentId { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "I agree to the Terms of Service")]
        public bool AgreeToTerms { get; set; }
    }

    public class ProfileViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? StudentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Role { get; set; } = string.Empty;

        // Statistics
        public int ProblemsSolved { get; set; }
        public int ContestsParticipated { get; set; }
        public int TotalPoints { get; set; }

        public string FullName => $"{FirstName} {LastName}";
        public string MemberSince => CreatedAt.ToString("MMMM yyyy");
        public string LastActive => LastLoginAt?.ToString("MMM dd, yyyy") ?? "Never";
    }

    // Submission ViewModel for profile page
    public class SubmissionViewModel
    {
        public int ProblemId { get; set; }
        public string ProblemTitle { get; set; } = string.Empty;
        public string Verdict { get; set; } = string.Empty;
        public double? ExecutionTime { get; set; }
        public int? MemoryUsed { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string LanguageName { get; set; } = string.Empty;

        // For display formatting
        public string VerdictDisplay
        {
            get
            {
                return Verdict switch
                {
                    "AC" => "Accepted",
                    "WA" => "Wrong Answer",
                    "TLE" => "Time Limit Exceeded",
                    "MLE" => "Memory Limit Exceeded",
                    "RE" => "Runtime Error",
                    "CE" => "Compilation Error",
                    _ => Verdict
                };
            }
        }

        public string VerdictColor
        {
            get
            {
                return Verdict switch
                {
                    "AC" => "success",
                    "WA" => "danger",
                    "TLE" => "warning",
                    "MLE" => "info",
                    "RE" => "danger",
                    "CE" => "secondary",
                    _ => "secondary"
                };
            }
        }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}