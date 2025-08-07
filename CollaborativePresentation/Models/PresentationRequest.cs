namespace CollaborativePresentation.Models
{
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

    public class UpdateUserRoleRequest
    {
        public int PresentationId { get; set; }
        public string Username { get; set; }
        public string NewRole { get; set; }
    }
}
