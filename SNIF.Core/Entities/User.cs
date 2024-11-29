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
    }
}