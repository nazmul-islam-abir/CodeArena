using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace MyMvcApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly string _connectionString;

        public AuthController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Check if user is already logged in
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                var query = @"
                    SELECT id, first_name, last_name, username, email, student_id, 
                           password_hash, is_active, role
                    FROM users 
                    WHERE (username = @username OR email = @email) AND is_active = true";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", model.Username);
                cmd.Parameters.AddWithValue("@email", model.Username);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var passwordHash = reader.GetString(6);
                    if (VerifyPassword(model.Password, passwordHash))
                    {
                        var userId = reader.GetInt32(0);
                        var firstName = reader.GetString(1);
                        var lastName = reader.GetString(2);
                        var username = reader.GetString(3);
                        var role = reader.GetString(8);

                        // Close reader before executing another command
                        reader.Close();

                        // Update last login
                        var updateQuery = "UPDATE users SET last_login_at = @lastLogin WHERE id = @id";
                        using var updateCmd = new NpgsqlCommand(updateQuery, conn);
                        updateCmd.Parameters.AddWithValue("@lastLogin", DateTime.Now);
                        updateCmd.Parameters.AddWithValue("@id", userId);
                        updateCmd.ExecuteNonQuery();

                        // Set session variables
                        HttpContext.Session.SetString("UserId", userId.ToString());
                        HttpContext.Session.SetString("Username", username);
                        HttpContext.Session.SetString("UserRole", role);
                        HttpContext.Session.SetString("UserFullName", firstName + " " + lastName);

                        TempData["SuccessMessage"] = "Login successful!";
                        return RedirectToAction("Index", "Home");
                    }
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
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public IActionResult Signup(SignupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                // Check if username exists
                var checkQuery = "SELECT COUNT(*) FROM users WHERE username = @username";
                using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@username", model.Username);
                var count = (long)checkCmd.ExecuteScalar();
                if (count > 0)
                {
                    ModelState.AddModelError("Username", "Username already exists");
                    return View(model);
                }

                // Check if email exists
                checkCmd.CommandText = "SELECT COUNT(*) FROM users WHERE email = @email";
                checkCmd.Parameters.Clear();
                checkCmd.Parameters.AddWithValue("@email", model.Email);
                count = (long)checkCmd.ExecuteScalar();
                if (count > 0)
                {
                    ModelState.AddModelError("Email", "Email already registered");
                    return View(model);
                }

                // Insert new user
                var insertQuery = @"
                    INSERT INTO users (first_name, last_name, username, email, student_id, password_hash, created_at, is_active, role)
                    VALUES (@firstName, @lastName, @username, @email, @studentId, @passwordHash, @createdAt, @isActive, @role)
                    RETURNING id";

                using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@firstName", model.FirstName);
                insertCmd.Parameters.AddWithValue("@lastName", model.LastName);
                insertCmd.Parameters.AddWithValue("@username", model.Username);
                insertCmd.Parameters.AddWithValue("@email", model.Email);
                insertCmd.Parameters.AddWithValue("@studentId", string.IsNullOrEmpty(model.StudentId) ? DBNull.Value : (object)model.StudentId);
                insertCmd.Parameters.AddWithValue("@passwordHash", HashPassword(model.Password));
                insertCmd.Parameters.AddWithValue("@createdAt", DateTime.Now);
                insertCmd.Parameters.AddWithValue("@isActive", true);
                insertCmd.Parameters.AddWithValue("@role", "User");

                var userId = (int)insertCmd.ExecuteScalar();

                // Set session
                HttpContext.Session.SetString("UserId", userId.ToString());
                HttpContext.Session.SetString("Username", model.Username);
                HttpContext.Session.SetString("UserRole", "User");
                HttpContext.Session.SetString("UserFullName", model.FirstName + " " + model.LastName);

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
        public IActionResult Profile()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Please login to view your profile.";
                return RedirectToAction("Login");
            }

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                var query = @"
                    SELECT id, first_name, last_name, username, email, student_id, 
                           created_at, last_login_at, role
                    FROM users 
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", int.Parse(userId));

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var profile = new ProfileViewModel
                    {
                        Id = reader.GetInt32(0),
                        FirstName = reader.GetString(1),
                        LastName = reader.GetString(2),
                        Username = reader.GetString(3),
                        Email = reader.GetString(4),
                        StudentId = reader.IsDBNull(5) ? null : reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6),
                        LastLoginAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        Role = reader.GetString(8),
                        
                        // For demo purposes - in real app, calculate from database
                        ProblemsSolved = 12,
                        ContestsParticipated = 5,
                        TotalPoints = 450
                    };

                    return View(profile);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading profile: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
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
    }
}