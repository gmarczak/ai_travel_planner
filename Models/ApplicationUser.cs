using Microsoft.AspNetCore.Identity;

namespace project.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? FullName { get; set; }
    public bool IsAdmin { get; set; } = false;
}
