using System.ComponentModel.DataAnnotations;

namespace project.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;
        
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastLoginAt { get; set; }
        
        public List<TravelPlan> SavedPlans { get; set; } = new();
    }
    
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
        
        public bool RememberMe { get; set; }
    }
    
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required")]
        public string FirstName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Last name is required")]
        public string LastName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords don't match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}