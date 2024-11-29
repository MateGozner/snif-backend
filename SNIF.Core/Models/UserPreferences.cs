using System.ComponentModel.DataAnnotations;
using SNIF.Core.Entities;

namespace SNIF.Core.Models
{
    public class UserPreferences : BaseModel
    {
        [Range(1, 500)]
        public double SearchRadius { get; set; } = 50;
        
        public virtual NotificationSettings NotificationSettings { get; set; } = null!;
        
        [StringLength(50)]
        public string? PreferredLanguage { get; set; }
        
        public bool ShowOnlineStatus { get; set; } = true;
        
        // Navigation property
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}