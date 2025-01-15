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
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<Message> Messages => Set<Message>();

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

            builder.Entity<Match>(b =>
            {
                b.HasKey(x => x.Id);

                // Configure InitiatorPet relationship
                b.HasOne(m => m.InitiatiorPet)
                 .WithMany(p => p.InitiatedMatches)
                 .HasForeignKey(m => m.InitiatiorPetId)
                 .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

                // Configure TargetPet relationship
                b.HasOne(m => m.TargetPet)
                 .WithMany(p => p.ReceivedMatches)
                 .HasForeignKey(m => m.TargetPetId)
                 .OnDelete(DeleteBehavior.Restrict);
                b.Property(m => m.Status).HasConversion<string>();

                b.Property(m => m.Purpose)
                 .HasConversion<string>();
            });

            builder.Entity<Message>(b =>
            {
                b.HasKey(x => x.Id);

                b.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(m => m.Receiver)
                    .WithMany()
                    .HasForeignKey(m => m.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(m => m.Match)
                    .WithMany()
                    .HasForeignKey(m => m.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}