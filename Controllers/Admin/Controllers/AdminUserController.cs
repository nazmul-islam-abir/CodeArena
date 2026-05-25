using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp.Data;
using MyMvcApp.Models;
using System.Threading.Tasks;
using System.Linq;

namespace MyMvcApp.Controllers.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Users")]
    public class AdminUserController : Controller
    {
        private readonly AppDbContext _context;

        public AdminUserController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View("~/Views/Admin/User/Index.cshtml", users);
        }

        [HttpPost("MakeAdmin/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeAdmin(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.Role = "Admin";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{user.Username} is now an Admin.";
            }
            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost("RemoveAdmin/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAdmin(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Prevent removing self if it's the only admin or just prevent removing own admin status
                var currentUserId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                if (currentUserId == id.ToString())
                {
                    TempData["ErrorMessage"] = "You cannot remove your own admin status.";
                    return RedirectToAction(nameof(Index));
                }

                user.Role = "";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{user.Username} is no longer an Admin.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                var currentUserId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                if (currentUserId == id.ToString())
                {
                    TempData["ErrorMessage"] = "You cannot deactivate your own account.";
                    return RedirectToAction(nameof(Index));
                }

                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"User status updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                var currentUserId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                if (currentUserId == id.ToString())
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
