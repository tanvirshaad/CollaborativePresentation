using Microsoft.AspNetCore.SignalR;
using CollaborativePresentation.Models;
using CollaborativePresentation.Data;
using Microsoft.EntityFrameworkCore;

namespace CollaborativePresentation.Hubs
{
    public class PresentationHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PresentationHub(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task Modify(string slideId, string Data)
        {
            try
            {
                
                var nickname = _httpContextAccessor.HttpContext?.Session.GetString("UserNickname");
                if (string.IsNullOrEmpty(nickname))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }
                
                
                var slide = await _db.Slides
                    .Include(s => s.Presentation)
                    .ThenInclude(p => p.ConnectedUsers)
                    .FirstOrDefaultAsync(s => s.Id == int.Parse(slideId));
                
                if (slide == null)
                {
                    await Clients.Caller.SendAsync("Error", "Slide not found");
                    return;
                }
                
                var user = slide.Presentation.ConnectedUsers.FirstOrDefault(u => u.Name == nickname);
                if (user == null || user.Role == UserRole.Viewer)
                {
                    await Clients.Caller.SendAsync("Error", "You don't have permission to edit this slide");
                    return;
                }
                
                
                await Clients.OthersInGroup(slideId).SendAsync("UpdateDrawing", Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Modify: {ex.Message}");
                await Clients.Caller.SendAsync("Error", $"An error occurred: {ex.Message}");
            }
        }

        public async Task<string> JoinBoard(string boardId, string username)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, boardId);
            await Clients.OthersInGroup(boardId).SendAsync("ReceiveUserJoinInfo", username);
            return $"Added to group with connection id {Context.ConnectionId} in group {boardId}";
        }

        public async Task SaveSvg(string slideId, string svgData)
        {
            var nickname = string.Empty;
            try
            {
                Console.WriteLine($"SaveSvg called for slide {slideId}");
                
                if (string.IsNullOrEmpty(slideId))
                {
                    Console.WriteLine("Empty slideId received");
                    await Clients.Caller.SendAsync("Error", "Invalid slide ID");
                    return;
                }

                
                if (string.IsNullOrEmpty(svgData))
                {
                    Console.WriteLine("Empty SVG data received");
                    await Clients.Caller.SendAsync("Error", "Empty SVG data received");
                    return;
                }

                
                if (!svgData.Contains("<svg"))
                {
                    Console.WriteLine("Invalid SVG data format");
                    await Clients.Caller.SendAsync("Error", "Invalid SVG data format");
                    return;
                }

                nickname = _httpContextAccessor.HttpContext?.Session.GetString("UserNickname");
                if (string.IsNullOrEmpty(nickname))
                {
                    Console.WriteLine("User not authenticated");
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                Console.WriteLine($"User {nickname} attempting to save slide {slideId}");
                
                int slideIdInt;
                if (!int.TryParse(slideId, out slideIdInt))
                {
                    Console.WriteLine($"Invalid slideId format: {slideId}");
                    await Clients.Caller.SendAsync("Error", "Invalid slide ID format");
                    return;
                }

                var slide = await _db.Slides
                    .Include(s => s.Presentation)
                    .ThenInclude(p => p.ConnectedUsers)
                    .FirstOrDefaultAsync(s => s.Id == slideIdInt);

                if (slide == null)
                {
                    Console.WriteLine($"Slide not found: {slideId}");
                    await Clients.Caller.SendAsync("Error", "Slide not found");
                    return;
                }

                var user = slide.Presentation.ConnectedUsers.FirstOrDefault(u => u.Name == nickname);
                if (user == null)
                {
                    Console.WriteLine($"User {nickname} not found in presentation");
                    await Clients.Caller.SendAsync("Error", "User not found in presentation");
                    return;
                }
                
                if (user.Role == UserRole.Viewer)
                {
                    Console.WriteLine($"User {nickname} is a viewer and cannot save");
                    await Clients.Caller.SendAsync("Error", "Viewers cannot edit slides");
                    return;
                }

                Console.WriteLine($"Saving SVG for slide {slideId}, data length: {svgData.Length}");
                
                try 
                {
                    
                    byte[] svgBytes;
                    try
                    {
                        svgBytes = System.Text.Encoding.UTF8.GetBytes(svgData);
                        if (svgBytes.Length == 0)
                        {
                            Console.WriteLine("Failed to encode SVG data");
                            await Clients.Caller.SendAsync("Error", "Failed to encode SVG data");
                            return;
                        }
                    }
                    catch (Exception encEx)
                    {
                        Console.WriteLine($"Error encoding SVG data: {encEx.Message}");
                        await Clients.Caller.SendAsync("Error", "Error encoding SVG data");
                        return;
                    }

                    
                    slide.SvgData = svgBytes;
                    slide.LastModified = DateTime.UtcNow;
                    
                    
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Successfully saved SVG data for slide {slideId}");
                    
                    
                    await Clients.Group(slideId).SendAsync("SvgSaved", slideId);
                    Console.WriteLine($"Broadcast SvgSaved event for slide {slideId}");
                    
                    
                    await Clients.Caller.SendAsync("SaveSuccessful", slideId);
                }
                catch (Exception dbEx) 
                {
                    Console.WriteLine($"Database error while saving SVG: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {dbEx.InnerException.Message}");
                    }
                    await Clients.Caller.SendAsync("Error", $"Database error: {dbEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SaveSvg: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                await Clients.Caller.SendAsync("Error", "Failed to save SVG: " + ex.Message);
            }
        }
    }
} 