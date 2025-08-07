using CollaborativePresentation.Data;
using CollaborativePresentation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;


namespace CollaborativePresentation.Controllers
{
    public class PresentationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PresentationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSlide([FromBody] AddSlideRequest request)
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var presentation = await _context.Presentations
                .Include(p => p.ConnectedUsers)
                .Include(p => p.Slides)
                .FirstOrDefaultAsync(p => p.Id == request.PresentationId);

            if (presentation == null)
            {
                return NotFound();
            }

            var user = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
            if (user == null || user.Role != UserRole.Creator)
            {
                return Forbid("Only the creator can add slides");
            }

            var maxOrder = presentation.Slides.Any() ? presentation.Slides.Max(s => s.Order) : 0;
            var newSlide = new Slide
            {
                Order = maxOrder + 1,
                PresentationId = request.PresentationId,
                LastModified = DateTime.UtcNow
            };

            _context.Slides.Add(newSlide);
            await _context.SaveChangesAsync();

            return Json(new { success = true, slideId = newSlide.Id, order = newSlide.Order });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlide([FromBody] DeleteSlideRequest request)
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var slide = await _context.Slides
                .Include(s => s.Presentation)
                .ThenInclude(p => p.ConnectedUsers)
                .FirstOrDefaultAsync(s => s.Id == request.SlideId);

            if (slide == null)
            {
                return NotFound();
            }

            var user = slide.Presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
            if (user == null || user.Role != UserRole.Creator)
            {
                return Forbid("Only the creator can delete slides");
            }

            // Don't allow deleting the last slide
            var slideCount = await _context.Slides.CountAsync(s => s.PresentationId == request.PresentationId);
            if (slideCount <= 1)
            {
                return BadRequest("Cannot delete the last slide");
            }

            _context.Slides.Remove(slide);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole([FromBody] UpdateUserRoleRequest request)
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var presentation = await _context.Presentations
                .Include(p => p.ConnectedUsers)
                .FirstOrDefaultAsync(p => p.Id == request.PresentationId);

            if (presentation == null)
            {
                return NotFound();
            }

            var currentUser = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
            if (currentUser == null || currentUser.Role != UserRole.Creator)
            {
                return Forbid("Only the creator can change user roles");
            }

            var targetUser = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == request.Username);
            if (targetUser == null)
            {
                return NotFound("User not found");
            }

            if (targetUser.Role == UserRole.Creator)
            {
                return BadRequest("Cannot change creator role");
            }

            if (Enum.TryParse<UserRole>(request.NewRole, out var newRole))
            {
                targetUser.Role = newRole;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return BadRequest("Invalid role");
        }

        [HttpGet]
        public async Task<IActionResult> GetSvg(int slideId)
        {
            var slide = await _context.Slides.FindAsync(slideId);
            if (slide == null || slide.SvgData == null)
            {
                return Content("", "image/svg+xml");
            }

            var svgString = Encoding.UTF8.GetString(slide.SvgData);
            return Content(svgString, "image/svg+xml");
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers(int presentationId)
        {
            var users = await _context.Users
                .Where(u => u.PresentationId == presentationId)
                .Select(u => new { u.Name, Role = u.Role.ToString() })
                .ToListAsync();

            return Json(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSlide([FromBody] SaveSlideRequest request)
        {
            try
            {
                // Check authentication
                var username = HttpContext.Session.GetString("UserNickname");
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Validate input
                if (request == null || string.IsNullOrEmpty(request.SvgData))
                {
                    return Json(new { success = false, message = "Invalid SVG data" });
                }

                // Find the slide
                var slide = await _context.Slides
                    .Include(s => s.Presentation)
                    .ThenInclude(p => p.ConnectedUsers)
                    .FirstOrDefaultAsync(s => s.Id == request.SlideId);

                if (slide == null)
                {
                    return Json(new { success = false, message = "Slide not found" });
                }

                // Check permissions
                var user = slide.Presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found in presentation" });
                }

                if (user.Role == UserRole.Viewer)
                {
                    return Json(new { success = false, message = "Viewers cannot edit slides" });
                }

                // Save the SVG data
                byte[] svgBytes = System.Text.Encoding.UTF8.GetBytes(request.SvgData);
                slide.SvgData = svgBytes;
                slide.LastModified = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Slide saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error saving slide: {ex.Message}" });
            }
        }
    }

    public class SaveSlideRequest
    {
        public int SlideId { get; set; }
        public string SvgData { get; set; }
    }
} 