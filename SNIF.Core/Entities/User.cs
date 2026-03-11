using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using SNIF.Core.Models;

namespace SNIF.Core.Entities
{
    public class User : IdentityUser
    {
        public User()
        {
            Pets = new HashSet<Pet>();
        }

        public required string Name { get; set; }
        public Location? Location { get; set; }
        public virtual ICollection<Pet> Pets { get; set; }
        public BreederVerification? BreederVerification { get; set; }
        public UserPreferences? Preferences { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? ProfilePicturePath { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }

        // Moderation fields
        public bool IsBanned { get; set; }
        public DateTime? SuspendedUntil { get; set; }
        public string? BanReason { get; set; }
        public int WarningCount { get; set; }

        // Google OAuth
        public string? GoogleSubjectId { get; set; }

        // Password Reset
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        // Email Confirmation
        public bool EmailConfirmed { get; set; }
        public string? EmailConfirmationToken { get; set; }

        // GDPR
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}