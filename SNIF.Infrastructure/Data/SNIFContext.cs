using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.Entities;
using SNIF.Core.Models;

namespace SNIF.Infrastructure.Data
{
    public class SNIFContext : IdentityDbContext<User>
    {
        public SNIFContext(DbContextOptions<SNIFContext> options) : base(options) { }

        public DbSet<Pet> Pets => Set<Pet>();
        public DbSet<BreederVerification> BreederVerifications => Set<BreederVerification>();
        public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
        public DbSet<Location> Locations => Set<Location>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Location>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedOnAdd();
            });

            builder.Entity<User>()
                .HasOne(u => u.Location)
                .WithMany()
                .HasForeignKey("LocationId")
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<User>()
                .HasMany(u => u.Pets)
                .WithOne(p => p.Owner)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<User>()
                .HasOne(u => u.BreederVerification)
                .WithOne(b => b.User)
                .HasForeignKey<BreederVerification>(b => b.UserId);

            builder.Entity<User>()
                .HasOne(u => u.Preferences);
        }
    }
}