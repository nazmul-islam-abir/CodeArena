using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using MyMvcApp.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace MyMvcApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;
        private readonly MyMvcApp.Services.IEmailService _emailService;

        public AuthController(AppDbContext context, MyMvcApp.Services.IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => (u.Username == model.Username || u.Email == model.Username) && u.IsActive);

                if (user != null && VerifyPassword(model.Password, user.PasswordHash))
                {
                    // Update last login
                    user.LastLoginAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Set session variables
                    HttpContext.Session.SetString("UserId", user.Id.ToString());
                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("UserFullName", user.FullName);

                    // Create authentication cookie
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim("FullName", user.FullName)
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    TempData["SuccessMessage"] = "Login successful!";
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid username or password");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Signup()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signup(SignupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Check if username or email exists separately to provide precise feedback
                bool usernameExists = await _context.Users.AnyAsync(u => u.Username == model.Username);
                bool emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (usernameExists || emailExists)
                {
                    if (usernameExists)
                        ModelState.AddModelError("Username", "Username already exists");
                    if (emailExists)
                        ModelState.AddModelError("Email", "Email already registered");

                    return View(model);
                }

                // Create new user
                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Username = model.Username,
                    Email = model.Email,
                    StudentId = model.StudentId,
                    PasswordHash = HashPassword(model.Password),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Role = "User",
                    ProblemsSolved = 0,
                    ContestsParticipated = 0,
                    TotalPoints = 0
                };

                _context.Users.Add(user);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    // Extract inner exception message for diagnosis
                    var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                    Console.WriteLine("DbUpdateException during signup: " + inner);

                    // Provide friendly message for common unique constraint violations
                    if (inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || inner.Contains("unique", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("", "A user with the same username or email already exists.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Database error: " + inner);
                    }

                    return View(model);
                }

                // Set session
                HttpContext.Session.SetString("UserId", user.Id.ToString());
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("UserRole", "User");
                HttpContext.Session.SetString("UserFullName", user.FullName);

                TempData["SuccessMessage"] = "Account created successfully!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["ErrorMessage"] = "Please login to view your profile.";
                return RedirectToAction("Login");
            }

            try
            {
                int userId = int.Parse(userIdStr);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return NotFound();
                }

                var profile = new ProfileViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Username = user.Username,
                    Email = user.Email,
                    StudentId = user.StudentId,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    Role = user.Role,
                    ProblemsSolved = user.ProblemsSolved,
                    ContestsParticipated = user.ContestsParticipated,
                    TotalPoints = user.TotalPoints
                };

                // Get recent submissions
                var submissions = await _context.Submissions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SubmittedAt)
                    .Take(10)
                    .Select(s => new SubmissionViewModel
                    {
                        ProblemId = s.ProblemId,
                        ProblemTitle = s.ProblemTitle,
                        Verdict = s.Verdict,
                        ExecutionTime = s.ExecutionTime,
                        MemoryUsed = s.MemoryUsed,
                        SubmittedAt = s.SubmittedAt,
                        LanguageName = s.LanguageName
                    })
                    .ToListAsync();

                ViewBag.RecentSubmissions = submissions;

                // Get statistics by difficulty
                var stats = await _context.Submissions
                    .Where(s => s.UserId == userId && s.Verdict == "AC")
                    .Join(_context.Problems, s => s.ProblemId, p => p.Id, (s, p) => new { p.Difficulty, p.Id })
                    .Distinct()
                    .GroupBy(x => x.Difficulty)
                    .Select(g => new { Difficulty = g.Key, Count = g.Count() })
                    .ToListAsync();

                ViewBag.EasySolved = stats.FirstOrDefault(s => s.Difficulty == 1)?.Count ?? 0;
                ViewBag.MediumSolved = stats.FirstOrDefault(s => s.Difficulty == 2)?.Count ?? 0;
                ViewBag.HardSolved = stats.FirstOrDefault(s => s.Difficulty == 3)?.Count ?? 0;

                // Get solved problems
                var solvedProblems = await _context.Submissions
                    .Where(s => s.UserId == userId && s.Verdict == "AC")
                    .Join(_context.Problems, s => s.ProblemId, p => p.Id, (s, p) => new { s.SubmittedAt, p.Id, p.Title, p.Difficulty, p.Points })
                    .OrderByDescending(x => x.SubmittedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.Title,
                        x.Difficulty,
                        x.Points,
                        SolvedAt = x.SubmittedAt.ToString("MMM dd, yyyy")
                    })
                    .ToListAsync();

                ViewBag.SolvedProblems = solvedProblems;

                return View(profile);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading profile: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            string hashedInput = HashPassword(password);
            return hashedInput == hash;
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                // Return success anyway to prevent email enumeration
                TempData["SuccessMessage"] = "If an account with that email exists, a password reset link has been sent.";
                return RedirectToAction("Login");
            }

            // Generate Token
            var tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            var token = Convert.ToBase64String(tokenBytes);

            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            // Send Email
            var resetLink = Url.Action("ResetPassword", "Auth", new { token = token, email = user.Email }, Request.Scheme);
            var emailBody = $"<p>You requested a password reset. Click the link below to reset your password:</p><p><a href='{resetLink}'>Reset Password</a></p><p>This link will expire in 1 hour.</p>";
            
            try
            {
                await _emailService.SendEmailAsync(user.Email, "Password Reset", emailBody);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to send email. Please try again later. " + ex.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = "If an account with that email exists, a password reset link has been sent.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email && u.ResetToken == model.Token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Invalid or expired reset token.");
                return View(model);
            }

            user.PasswordHash = HashPassword(model.Password);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your password has been successfully reset. Please log in.";
            return RedirectToAction("Login");
        }
    }
}