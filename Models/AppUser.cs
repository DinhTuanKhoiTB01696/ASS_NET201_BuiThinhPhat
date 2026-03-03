using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Assignment_NET201.Models
{
    public class AppUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        public int Points { get; set; } = 0;
        
        [StringLength(50)]
        public string Rank { get; set; } = "Bronze"; // Bronze, Silver, Gold, Diamond
    }
}
