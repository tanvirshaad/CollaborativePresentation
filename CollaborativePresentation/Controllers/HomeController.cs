using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CollaborativePresentation.Data;
using CollaborativePresentation.Models;

namespace CollaborativePresentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return View("Login");
            }

            return RedirectToAction("Presentations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                ViewBag.Error = "Please enter a valid username";
                return View("Login");
            }

            HttpContext.Session.SetString("UserNickname", username.Trim());
            return RedirectToAction("Presentations");
        }

        public async Task<IActionResult> Presentations()
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Index");
            }

            var presentations = await _context.Presentations
                .Include(p => p.ConnectedUsers)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.Username = username;
            return View(presentations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePresentation(string title)
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Please enter a valid presentation title";
                return RedirectToAction("Presentations");
            }

            var presentation = new Presentation
            {
                Title = title.Trim(),
                CreatorName = username,
                CreatedAt = DateTime.UtcNow,
                Slides = new List<Slide>(),
                ConnectedUsers = new List<User>()
            };

            _context.Presentations.Add(presentation);
            await _context.SaveChangesAsync();

            
            var creator = new User
            {
                Name = username,
                Role = UserRole.Creator,
                PresentationId = presentation.Id
            };

            _context.Users.Add(creator);

            
            var initialSlide = new Slide
            {
                Order = 1,
                PresentationId = presentation.Id,
                LastModified = DateTime.UtcNow
            };

            _context.Slides.Add(initialSlide);
            await _context.SaveChangesAsync();

            return RedirectToAction("Edit", new { id = presentation.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var username = HttpContext.Session.GetString("UserNickname");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Index");
            }

            var presentation = await _context.Presentations
                .Include(p => p.Slides.OrderBy(s => s.Order))
                .Include(p => p.ConnectedUsers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (presentation == null)
            {
                return NotFound();
            }

            
            var existingUser = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
            if (existingUser == null)
            {
                var newUser = new User
                {
                    Name = username,
                    Role = UserRole.Viewer,
                    PresentationId = presentation.Id
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
            }

            ViewBag.Username = username;
            ViewBag.UserRole = existingUser?.Role ?? UserRole.Viewer;
            return View(presentation);
        }

        public async Task<IActionResult> Present(int id)
        {
            Console.WriteLine($"Present method called with presentation ID: {id}");

            
            var presentation = await _context.Presentations
                .Include(p => p.Slides.OrderBy(s => s.Order))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (presentation == null)
            {
                Console.WriteLine($"Presentation with ID {id} not found");
                return NotFound("Presentation not found");
            }

            Console.WriteLine($"Found presentation '{presentation.Title}' with {presentation.Slides.Count} slides");

            
            if (presentation.Slides == null || !presentation.Slides.Any())
            {
                Console.WriteLine("No slides found for this presentation, creating a default slide");

                
                var anySlides = await _context.Slides
                    .Where(s => s.PresentationId == id)
                    .ToListAsync();

                if (anySlides.Any())
                {
                    Console.WriteLine($"Found {anySlides.Count} slides in database that weren't loaded with presentation");
                    foreach (var slide in anySlides)
                    {
                        Console.WriteLine($"Database slide: ID={slide.Id}, Order={slide.Order}, HasData={slide.SvgData != null && slide.SvgData.Length > 0}");
                    }

                    
                    presentation.Slides = anySlides;
                }
                else
                {
                    
                    var defaultSlide = new Slide
                    {
                        Order = 1,
                        PresentationId = id,
                        LastModified = DateTime.UtcNow
                    };

                    
                    var defaultSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\">" +
                                    "<text x=\"400\" y=\"300\" font-family=\"Arial\" font-size=\"24\" text-anchor=\"middle\" dominant-baseline=\"middle\">Default Slide</text></svg>";
                    defaultSlide.SvgData = System.Text.Encoding.UTF8.GetBytes(defaultSvg);

                    _context.Slides.Add(defaultSlide);
                    await _context.SaveChangesAsync();

                    
                    presentation = await _context.Presentations
                        .Include(p => p.Slides.OrderBy(s => s.Order))
                        .FirstOrDefaultAsync(p => p.Id == id);
                }
            }

            
            foreach (var slide in presentation.Slides)
            {
                Console.WriteLine($"Slide ID: {slide.Id}, Order: {slide.Order}, Has SVG: {slide.SvgData != null && slide.SvgData.Length > 0}");
            }

            
            foreach (var slide in presentation.Slides)
            {
                if (slide.SvgData == null || slide.SvgData.Length == 0)
                {
                    
                    var defaultSvg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\"><text x=\"400\" y=\"300\" font-family=\"Arial\" font-size=\"24\" text-anchor=\"middle\" dominant-baseline=\"middle\">Slide {slide.Order}</text></svg>";
                    slide.SvgData = System.Text.Encoding.UTF8.GetBytes(defaultSvg);

                    
                    _context.Slides.Update(slide);
                }
            }
            
            
            await _context.SaveChangesAsync();

            return View(presentation);
        }

        [HttpGet]
        public async Task<IActionResult> GetSlideSvg(int slideId)
        {
            try
            {
                Console.WriteLine($"GetSlideSvg called with ID: {slideId}");

                
                if (slideId <= 0)
                {
                    Console.WriteLine($"Invalid slide ID: {slideId}");
                    var errorSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\">" +
                                 "<text x=\"400\" y=\"300\" font-family=\"Arial\" font-size=\"24\" fill=\"red\" text-anchor=\"middle\" dominant-baseline=\"middle\">Error: Invalid Slide ID</text></svg>";
                    return Content(errorSvg, "image/svg+xml");
                }

                var slide = await _context.Slides.FindAsync(slideId);
                if (slide == null)
                {
                    Console.WriteLine($"Slide not found with ID: {slideId}");
                    
                    var defaultSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\">" +
                                    "<text x=\"400\" y=\"300\" font-family=\"Arial\" font-size=\"24\" text-anchor=\"middle\" dominant-baseline=\"middle\">Slide Not Found (ID: " + slideId + ")</text></svg>";
                    return Content(defaultSvg, "image/svg+xml");
                }

                if (slide.SvgData == null || slide.SvgData.Length == 0)
                {
                    
                    var defaultSvg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\">" +
                                    $"<text x=\"400\" y=\"300\" font-family=\"Arial\" font-size=\"24\" text-anchor=\"middle\" dominant-baseline=\"middle\">Slide {slide.Order}</text></svg>";
                    return Content(defaultSvg, "image/svg+xml");
                }

                var svgData = System.Text.Encoding.UTF8.GetString(slide.SvgData);
                return Content(svgData, "image/svg+xml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSlideSvg: {ex.Message}");
                
                var errorSvg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\">" +
                              $"<text x=\"400\" y=\"300\" font-family=\"Arial\" font-size=\"24\" fill=\"red\" text-anchor=\"middle\" dominant-baseline=\"middle\">Error: {ex.Message}</text></svg>";
                return Content(errorSvg, "image/svg+xml");
            }
        }
            
        

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }
    
    public IActionResult Status()
    {
        return View();
    }
    
    [HttpGet]
    public async Task<IActionResult> TestDatabaseConnection()
    {
        try
        {
            
            bool canConnect = await _context.Database.CanConnectAsync();
            
            if (canConnect)
            {
               
                int userCount = await _context.Users.CountAsync();
                return Json(new { success = true, message = $"Connected successfully. User count: {userCount}" });
            }
            else
            {
                return Json(new { success = false, message = "Cannot connect to database" });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }        [HttpGet]
        public async Task<IActionResult> GetSlideData(int slideId)
        {
            try
            {
                Console.WriteLine($"GetSlideData called with slideId: {slideId}");
                
                
                bool slideExists = await _context.Slides.AnyAsync(s => s.Id == slideId);
                if (!slideExists)
                {
                    Console.WriteLine($"Slide with ID {slideId} not found in database");
                    
                    
                    var otherSlides = await _context.Slides.Take(5).ToListAsync();
                    if (otherSlides.Any())
                    {
                        Console.WriteLine($"Some existing slide IDs: {string.Join(", ", otherSlides.Select(s => s.Id))}");
                    }
                    else
                    {
                        Console.WriteLine("No slides found in database at all");
                    }
                    
                    return Json(new { success = false, message = $"Slide not found (ID: {slideId})" });
                }
                
                var slide = await _context.Slides.FindAsync(slideId);
                if (slide == null) 
                {
                    return Json(new { success = false, message = "Slide not found after verification" });
                }

                string svgData = null;
                bool isJsonData = false;
                
                if (slide.SvgData != null && slide.SvgData.Length > 0)
                {
                    svgData = System.Text.Encoding.UTF8.GetString(slide.SvgData);
                    
                    
                    if (!string.IsNullOrEmpty(svgData) && svgData.Trim().StartsWith("{"))
                    {
                        isJsonData = true;
                    }
                    
                    else if (!string.IsNullOrEmpty(svgData) && !svgData.Trim().StartsWith("<svg"))
                    {
                       
                        var svgStart = svgData.IndexOf("<svg");
                        var svgEnd = svgData.LastIndexOf("</svg>") + 6;
                        if (svgStart >= 0 && svgEnd > svgStart)
                        {
                            svgData = svgData.Substring(svgStart, svgEnd - svgStart);
                        }
                    }
                }
                
                
                if (string.IsNullOrEmpty(svgData))
                {
                    svgData = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\"></svg>";
                }

                return Json(new { 
                    success = true, 
                    svgData = svgData,
                    slideId = slideId,
                    hasData = svgData != null,
                    isJsonData = isJsonData
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSlide([FromBody] AddSlideRequest request)
        {
            try
            {
                var username = HttpContext.Session.GetString("UserNickname");
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var presentation = await _context.Presentations
                    .Include(p => p.ConnectedUsers)
                    .Include(p => p.Slides)
                    .FirstOrDefaultAsync(p => p.Id == request.PresentationId);

                if (presentation == null)
                {
                    return Json(new { success = false, message = "Presentation not found" });
                }

                var user = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
                if (user == null || user.Role != UserRole.Creator)
                {
                    return Json(new { success = false, message = "Only the creator can add slides" });
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
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlide([FromBody] DeleteSlideRequest request)
        {
            try
            {
                var username = HttpContext.Session.GetString("UserNickname");
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var slide = await _context.Slides
                    .Include(s => s.Presentation)
                    .ThenInclude(p => p.ConnectedUsers)
                    .FirstOrDefaultAsync(s => s.Id == request.SlideId);

                if (slide == null)
                {
                    return Json(new { success = false, message = "Slide not found" });
                }

                var user = slide.Presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
                if (user == null || user.Role != UserRole.Creator)
                {
                    return Json(new { success = false, message = "Only the creator can delete slides" });
                }

                
                var slideCount = await _context.Slides.CountAsync(s => s.PresentationId == slide.PresentationId);
                if (slideCount <= 1)
                {
                    return Json(new { success = false, message = "Cannot delete the last slide" });
                }

                _context.Slides.Remove(slide);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole([FromBody] ChangeUserRoleRequest request)
        {
            try
            {
                var username = HttpContext.Session.GetString("UserNickname");
                if (string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var presentation = await _context.Presentations
                    .Include(p => p.ConnectedUsers)
                    .FirstOrDefaultAsync(p => p.Id == request.PresentationId);

                if (presentation == null)
                {
                    return Json(new { success = false, message = "Presentation not found" });
                }

                var currentUser = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == username);
                if (currentUser == null || currentUser.Role != UserRole.Creator)
                {
                    return Json(new { success = false, message = "Only the creator can change user roles" });
                }

                var targetUser = presentation.ConnectedUsers.FirstOrDefault(u => u.Name == request.Username);
                if (targetUser == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                if (targetUser.Role == UserRole.Creator)
                {
                    return Json(new { success = false, message = "Cannot change creator role" });
                }

                targetUser.Role = Enum.Parse<UserRole>(request.NewRole);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class ChangeUserRoleRequest
    {
        public int PresentationId { get; set; }
        public string Username { get; set; }
        public string NewRole { get; set; }
    }

    public class AddSlideRequest
    {
        public int PresentationId { get; set; }
        public bool IsBlank { get; set; }
    }

    public class DeleteSlideRequest
    {
        public int SlideId { get; set; }
        public int PresentationId { get; set; }
    }
}
